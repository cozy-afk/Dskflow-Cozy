package com.cozy.kvm.service

import android.inputmethodservice.InputMethodService
import android.view.KeyEvent
import android.view.View
import android.widget.TextView
import com.cozy.kvm.input.KeyTranslate

/**
 * Companion keyboard. An AccessibilityService cannot reliably inject text into
 * arbitrary apps, but an InputMethodService can: when the user has selected
 * "Cozy keyboard" and a text field is focused, [commitText]/[sendKeyEvent]
 * deliver real input.
 *
 * It shows a tiny status bar instead of keys, because the keystrokes come from
 * the PC, not from on-screen taps.
 */
class CozyInputMethodService : InputMethodService() {

    override fun onCreate() {
        super.onCreate()
        instance = this
    }

    override fun onDestroy() {
        if (instance === this) instance = null
        super.onDestroy()
    }

    override fun onCreateInputView(): View {
        // Minimal, non-interactive status strip.
        return TextView(this).apply {
            text = "  Cozy keyboard active — typing comes from your PC"
            setPadding(24, 24, 24, 24)
            textSize = 14f
        }
    }

    /**
     * Inject one key from the network. Returns true if it was handled here.
     * Printable characters are committed as text; editing/nav keys are sent as
     * key events; everything else is left for the accessibility fallback.
     */
    fun injectKey(keyId: Int, pressed: Boolean): Boolean {
        val ic = currentInputConnection ?: return false
        KeyTranslate.toChar(keyId)?.let { ch ->
            if (pressed) ic.commitText(ch.toString(), 1)
            return true
        }
        KeyTranslate.toAndroidKeyCode(keyId)?.let { code ->
            val action = if (pressed) KeyEvent.ACTION_DOWN else KeyEvent.ACTION_UP
            ic.sendKeyEvent(KeyEvent(action, code))
            return true
        }
        return false
    }

    companion object {
        @Volatile var instance: CozyInputMethodService? = null
            private set
    }
}
