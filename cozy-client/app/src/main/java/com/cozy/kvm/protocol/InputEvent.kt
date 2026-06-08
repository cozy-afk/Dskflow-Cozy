package com.cozy.kvm.protocol

/**
 * Decoded, platform-neutral events the protocol layer hands to the injector.
 * The Accessibility/IME layer turns these into Android input.
 */
sealed interface InputEvent {
    /** Cursor entered this screen at an absolute point (from CINN). */
    data class Enter(val x: Int, val y: Int, val seq: Int, val modifiers: Int) : InputEvent

    /** Cursor left this screen (from COUT). */
    data object Leave : InputEvent

    /** Absolute mouse move within our reported screen size (from DMMV). */
    data class MouseMove(val x: Int, val y: Int) : InputEvent

    /** Relative mouse move (from DMRM). */
    data class MouseRelMove(val dx: Int, val dy: Int) : InputEvent

    /** Mouse button down/up (from DMDN/DMUP). button 1=left 2=right 3=middle. */
    data class MouseButton(val button: Int, val pressed: Boolean) : InputEvent

    /** Wheel scroll, units of 120 per tick (from DMWM). */
    data class MouseWheel(val xDelta: Int, val yDelta: Int) : InputEvent

    /** Key event (from DKDN/DKUP/DKRP). keyId is a platform keysym; see KeyTranslate. */
    data class Key(val keyId: Int, val modifiers: Int, val button: Int, val pressed: Boolean, val repeat: Int = 0) : InputEvent

    /** Connection state changes, surfaced for the UI. */
    data class Status(val state: ConnState, val detail: String = "") : InputEvent
}

enum class ConnState { CONNECTING, HANDSHAKING, CONNECTED, DISCONNECTED, ERROR }
