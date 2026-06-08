package com.cozy.kvm.protocol

import java.io.DataInputStream
import java.io.EOFException
import java.io.InputStream
import java.io.OutputStream

/**
 * Wire codec for the Deskflow / Synergy / Barrier protocol.
 *
 * Framing (see deskflow `PacketStreamFilter.cpp`): every packet is a 4-byte
 * big-endian length prefix followed by that many payload bytes. The payload
 * begins with a 4-byte ASCII message code (e.g. "DMMV").
 *
 * Field encoding (see deskflow `ProtocolUtil.cpp`):
 *   - `%Ni`  -> N-byte big-endian integer (N is 1, 2 or 4)
 *   - `%s`   -> 4-byte big-endian length prefix, then that many raw bytes
 *
 * Integers on the wire are unsigned in framing but signed in meaning for
 * coordinates; callers sign-extend where the protocol says "signed".
 */
object ProtocolCodec {

    /** Read exactly one framed packet's payload. Returns null on clean EOF. */
    fun readPacket(input: DataInputStream): ByteArray? {
        val length = try {
            input.readInt() // DataInputStream.readInt is big-endian — matches the wire
        } catch (e: EOFException) {
            return null
        }
        require(length in 0..PROTOCOL_MAX_MESSAGE_LENGTH) { "bad packet length $length" }
        val buf = ByteArray(length)
        input.readFully(buf)
        return buf
    }

    /** Write one framed packet (prepends the 4-byte big-endian length). */
    fun writePacket(output: OutputStream, payload: ByteArray) {
        output.write(intToBytes(payload.size, 4))
        output.write(payload)
        output.flush()
    }

    /** Big-endian encode [value] into [width] bytes (1, 2 or 4). */
    fun intToBytes(value: Int, width: Int): ByteArray {
        val out = ByteArray(width)
        for (i in 0 until width) {
            out[width - 1 - i] = ((value ushr (8 * i)) and 0xFF).toByte()
        }
        return out
    }

    private const val PROTOCOL_MAX_MESSAGE_LENGTH = 4 * 1024 * 1024
}

/**
 * Cursor over a packet payload that decodes protocol fields in order.
 * The 4-byte message code is expected to be consumed by the caller first.
 */
class PayloadReader(private val data: ByteArray) {
    var pos = 0
        private set

    fun code(): String {
        val s = String(data, 0, 4, Charsets.US_ASCII)
        pos = 4
        return s
    }

    /** Read an [width]-byte unsigned big-endian integer. */
    fun uint(width: Int): Int {
        var v = 0
        repeat(width) { v = (v shl 8) or (data[pos++].toInt() and 0xFF) }
        return v
    }

    /** Read an [width]-byte signed big-endian integer (sign-extended). */
    fun sint(width: Int): Int {
        val raw = uint(width)
        val signBit = 1 shl (width * 8 - 1)
        return if (raw and signBit != 0) raw - (1 shl (width * 8)) else raw
    }

    /** Read a `%s` field: 4-byte length prefix then that many raw bytes. */
    fun string(): ByteArray {
        val len = uint(4)
        val out = data.copyOfRange(pos, pos + len)
        pos += len
        return out
    }

    fun remaining(): Int = data.size - pos
}

/** Builds a packet payload using the same field encodings as the server. */
class PayloadWriter(code: String) {
    private val buf = ArrayList<Byte>(32)

    init {
        require(code.length == 4)
        buf.addAll(code.toByteArray(Charsets.US_ASCII).toList())
    }

    fun int(value: Int, width: Int): PayloadWriter {
        buf.addAll(ProtocolCodec.intToBytes(value, width).toList())
        return this
    }

    /** Append a fixed-length raw string with NO length prefix (e.g. the 7-byte protocol name). */
    fun rawFixed(text: String): PayloadWriter {
        buf.addAll(text.toByteArray(Charsets.US_ASCII).toList())
        return this
    }

    /** Append a `%s` field: 4-byte length prefix then bytes. */
    fun string(bytes: ByteArray): PayloadWriter {
        buf.addAll(ProtocolCodec.intToBytes(bytes.size, 4).toList())
        buf.addAll(bytes.toList())
        return this
    }

    fun build(): ByteArray = buf.toByteArray()
}
