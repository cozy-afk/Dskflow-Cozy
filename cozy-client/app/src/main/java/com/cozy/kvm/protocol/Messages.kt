package com.cozy.kvm.protocol

/**
 * Protocol message codes, mirrored from deskflow `ProtocolTypes.h`.
 * Only the messages the client (secondary screen) must understand or send
 * are listed; unknown codes are ignored to stay forward-compatible.
 */
object Msg {
    // Handshake
    const val HELLO = "Synergy" // 7-byte fixed protocol name field (server may also send "Barrier")
    const val HELLO_BARRIER = "Barrier"

    // Commands (server -> client)
    const val C_NOOP = "CNOP"
    const val C_CLOSE = "CBYE"
    const val C_ENTER = "CINN"
    const val C_LEAVE = "COUT"
    const val C_CLIPBOARD = "CCLP"
    const val C_SCREENSAVER = "CSEC"
    const val C_RESET_OPTIONS = "CROP"
    const val C_INFO_ACK = "CIAK"
    const val C_KEEP_ALIVE = "CALV"

    // Data (server -> client)
    const val D_KEY_DOWN = "DKDN"
    const val D_KEY_DOWN_LANG = "DKDL"
    const val D_KEY_REPEAT = "DKRP"
    const val D_KEY_UP = "DKUP"
    const val D_MOUSE_DOWN = "DMDN"
    const val D_MOUSE_UP = "DMUP"
    const val D_MOUSE_MOVE = "DMMV"
    const val D_MOUSE_REL_MOVE = "DMRM"
    const val D_MOUSE_WHEEL = "DMWM"
    const val D_CLIPBOARD = "DCLP"
    const val D_INFO = "DINF"
    const val D_SET_OPTIONS = "DSOP"
    const val D_SECURE_INPUT = "SECN"
    const val D_LANG_SYNC = "LSYN"

    // Queries / errors (server -> client)
    const val Q_INFO = "QINF"
    const val E_INCOMPATIBLE = "EICV"
    const val E_BUSY = "EBSY"
    const val E_UNKNOWN = "EUNK"
    const val E_BAD = "EBAD"

    const val MAJOR_VERSION = 1
    const val MINOR_VERSION = 6   // we implement through 1.6; server negotiates down as needed
    const val DEFAULT_PORT = 24800
}
