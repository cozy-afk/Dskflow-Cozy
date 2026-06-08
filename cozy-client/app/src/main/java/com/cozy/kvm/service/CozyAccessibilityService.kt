package com.cozy.kvm.service

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.PixelFormat
import android.graphics.Path
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.view.Gravity
import android.view.View
import android.view.WindowManager
import android.view.accessibility.AccessibilityEvent
import com.cozy.kvm.overlay.CursorView
import com.cozy.kvm.protocol.InputEvent
import kotlin.math.abs
import kotlin.math.max

/**
 * The injection engine. The network layer ([ConnectionService]) decodes the
 * protocol and forwards [InputEvent]s here; this service turns them into a
 * visible cursor and into real Android input via [dispatchGesture].
 *
 * Coordinate model: we report our screen size to the server as the real
 * display size, so absolute mouse coordinates map 1:1 to screen pixels.
 *
 * Accessibility ceiling (by design, not a bug): taps cannot reach windows
 * flagged FLAG_SECURE (password fields, DRM video, many games), and there is
 * no true "hover" — we synthesize discrete taps/swipes.
 */
class CozyAccessibilityService : AccessibilityService() {

    private lateinit var windowManager: WindowManager
    private var cursor: CursorView? = null
    private val main = Handler(Looper.getMainLooper())

    // Cursor position in screen pixels.
    private var cursorX = 0f
    private var cursorY = 0f
    private var screenW = 0
    private var screenH = 0

    // Press bookkeeping for tap vs. long-press vs. drag.
    private var buttonDownAtMs = 0L
    private var buttonDownX = 0f
    private var buttonDownY = 0f
    private var buttonHeld = false

    override fun onServiceConnected() {
        super.onServiceConnected()
        instance = this
        windowManager = getSystemService(WINDOW_SERVICE) as WindowManager
        val metrics = resources.displayMetrics
        screenW = metrics.widthPixels
        screenH = metrics.heightPixels
        cursorX = screenW / 2f
        cursorY = screenH / 2f
        Log.i(TAG, "accessibility connected, screen ${screenW}x${screenH}")
    }

    override fun onUnbind(intent: android.content.Intent?): Boolean {
        removeCursor()
        instance = null
        return super.onUnbind(intent)
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) { /* we don't observe UI */ }
    override fun onInterrupt() {}

    /** The real display size, reported to the server so coordinates line up. */
    fun displaySize(): Pair<Int, Int> = screenW to screenH

    /** Entry point called by [ConnectionService] for every decoded event. */
    fun handle(event: InputEvent) {
        when (event) {
            is InputEvent.Enter -> main.post { showCursor(); moveCursor(event.x.toFloat(), event.y.toFloat()) }
            is InputEvent.Leave -> main.post { hideCursor() }
            is InputEvent.MouseMove -> main.post { moveCursor(event.x.toFloat(), event.y.toFloat()) }
            is InputEvent.MouseRelMove -> main.post { moveCursor(cursorX + event.dx, cursorY + event.dy) }
            is InputEvent.MouseButton -> main.post { onButton(event.button, event.pressed) }
            is InputEvent.MouseWheel -> main.post { onWheel(event.xDelta, event.yDelta) }
            is InputEvent.Key -> onKey(event)
            is InputEvent.Status -> { /* surfaced elsewhere */ }
        }
    }

    // ---------------- cursor overlay ----------------

    private fun showCursor() {
        if (cursor != null) return
        val view = CursorView(this)
        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE or
                WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
            PixelFormat.TRANSLUCENT
        ).apply { gravity = Gravity.TOP or Gravity.START }
        try {
            windowManager.addView(view, params)
            cursor = view
            positionCursorView()
        } catch (e: Exception) {
            Log.e(TAG, "failed to add cursor overlay", e)
        }
    }

    private fun hideCursor() { cursor?.visibility = View.GONE }
    private fun removeCursor() {
        cursor?.let { runCatching { windowManager.removeView(it) } }
        cursor = null
    }

    private fun moveCursor(x: Float, y: Float) {
        cursorX = x.coerceIn(0f, max(0, screenW - 1).toFloat())
        cursorY = y.coerceIn(0f, max(0, screenH - 1).toFloat())
        if (cursor?.visibility != View.VISIBLE) cursor?.visibility = View.VISIBLE
        positionCursorView()
        // If a button is held and the pointer moved appreciably, this is a drag.
        if (buttonHeld && (abs(cursorX - buttonDownX) > DRAG_SLOP || abs(cursorY - buttonDownY) > DRAG_SLOP)) {
            // Drag is emitted on release as a single stroke from down->up point.
        }
    }

    private fun positionCursorView() {
        val view = cursor ?: return
        val lp = view.layoutParams as WindowManager.LayoutParams
        lp.x = cursorX.toInt()
        lp.y = cursorY.toInt()
        runCatching { windowManager.updateViewLayout(view, lp) }
    }

    // ---------------- injection ----------------

    private fun onButton(button: Int, pressed: Boolean) {
        if (button != 1) {
            // Right/middle: map right-button press to a "back" affordance for now.
            if (button == 2 && pressed) performGlobalAction(GLOBAL_ACTION_BACK)
            return
        }
        if (pressed) {
            buttonHeld = true
            buttonDownAtMs = android.os.SystemClock.uptimeMillis()
            buttonDownX = cursorX
            buttonDownY = cursorY
        } else if (buttonHeld) {
            buttonHeld = false
            val heldMs = android.os.SystemClock.uptimeMillis() - buttonDownAtMs
            val moved = abs(cursorX - buttonDownX) > DRAG_SLOP || abs(cursorY - buttonDownY) > DRAG_SLOP
            when {
                moved -> dragGesture(buttonDownX, buttonDownY, cursorX, cursorY, max(heldMs, 120))
                heldMs > LONG_PRESS_MS -> tapGesture(cursorX, cursorY, LONG_PRESS_MS)
                else -> tapGesture(cursorX, cursorY, 60)
            }
        }
    }

    private fun onWheel(xDelta: Int, yDelta: Int) {
        // 120 units = one notch. Swipe opposite to wheel direction to scroll content.
        val dist = (resources.displayMetrics.density * 120).toInt()
        val dy = if (yDelta > 0) -dist else if (yDelta < 0) dist else 0
        val dx = if (xDelta > 0) -dist else if (xDelta < 0) dist else 0
        if (dx == 0 && dy == 0) return
        dragGesture(cursorX, cursorY, cursorX + dx, cursorY + dy, 80)
    }

    private fun tapGesture(x: Float, y: Float, durationMs: Long) {
        val path = Path().apply { moveTo(x, y) }
        val stroke = GestureDescription.StrokeDescription(path, 0, durationMs)
        dispatchGesture(GestureDescription.Builder().addStroke(stroke).build(), null, null)
    }

    private fun dragGesture(x1: Float, y1: Float, x2: Float, y2: Float, durationMs: Long) {
        val path = Path().apply { moveTo(x1, y1); lineTo(x2, y2) }
        val stroke = GestureDescription.StrokeDescription(path, 0, durationMs)
        dispatchGesture(GestureDescription.Builder().addStroke(stroke).build(), null, null)
    }

    private fun onKey(event: InputEvent.Key) {
        // Prefer the companion IME (real text/key injection into focused fields).
        val handled = CozyInputMethodService.instance?.injectKey(event.keyId, event.pressed) ?: false
        if (handled) return
        // Fallback: only act on press, for keys that map to a global action.
        if (!event.pressed) return
        com.cozy.kvm.input.KeyTranslate.toGlobalAction(event.keyId)?.let { performGlobalAction(it) }
    }

    companion object {
        private const val TAG = "CozyA11y"
        private const val LONG_PRESS_MS = 500L
        private const val DRAG_SLOP = 16f

        /** Set while the service is bound; null otherwise. */
        @Volatile var instance: CozyAccessibilityService? = null
            private set
    }
}
