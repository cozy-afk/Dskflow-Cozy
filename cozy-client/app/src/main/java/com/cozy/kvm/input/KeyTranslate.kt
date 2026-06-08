package com.cozy.kvm.input

import android.accessibilityservice.AccessibilityService
import android.view.KeyEvent

/**
 * Maps Deskflow KeyIDs (X11-style keysyms, see deskflow `KeyTypes.h`) to Android.
 *
 * Three buckets:
 *  - Printable characters (keysym == Unicode for Latin-1) -> commit as text via IME.
 *  - Editing/navigation keys (0xFF00 range) -> Android [KeyEvent] codes.
 *  - A couple of keys also have global accessibility actions as a fallback.
 */
object KeyTranslate {

    // X11 keysyms Deskflow uses for special keys.
    private const val XK_BackSpace = 0xFF08
    private const val XK_Tab = 0xFF09
    private const val XK_Return = 0xFF0D
    private const val XK_Escape = 0xFF1B
    private const val XK_Home = 0xFF50
    private const val XK_Left = 0xFF51
    private const val XK_Up = 0xFF52
    private const val XK_Right = 0xFF53
    private const val XK_Down = 0xFF54
    private const val XK_End = 0xFF57
    private const val XK_Delete = 0xFFFF
    private const val XK_Space = 0x20

    /** Printable text for this key, or null if it's not a simple character. */
    fun toChar(keyId: Int): Char? {
        // Deskflow encodes Latin-1 printables as their code point directly.
        return if (keyId in 0x20..0x7E || keyId in 0xA0..0xFF) keyId.toChar() else null
    }

    /** Android KeyEvent keycode for editing/navigation keys, else null. */
    fun toAndroidKeyCode(keyId: Int): Int? = when (keyId) {
        XK_BackSpace -> KeyEvent.KEYCODE_DEL
        XK_Delete -> KeyEvent.KEYCODE_FORWARD_DEL
        XK_Return -> KeyEvent.KEYCODE_ENTER
        XK_Tab -> KeyEvent.KEYCODE_TAB
        XK_Escape -> KeyEvent.KEYCODE_ESCAPE
        XK_Left -> KeyEvent.KEYCODE_DPAD_LEFT
        XK_Right -> KeyEvent.KEYCODE_DPAD_RIGHT
        XK_Up -> KeyEvent.KEYCODE_DPAD_UP
        XK_Down -> KeyEvent.KEYCODE_DPAD_DOWN
        XK_Home -> KeyEvent.KEYCODE_MOVE_HOME
        XK_End -> KeyEvent.KEYCODE_MOVE_END
        XK_Space -> KeyEvent.KEYCODE_SPACE
        else -> null
    }

    /** Fallback global actions for when no IME/input field is focused. */
    fun toGlobalAction(keyId: Int): Int? = when (keyId) {
        XK_Escape -> AccessibilityService.GLOBAL_ACTION_BACK
        else -> null
    }
}
