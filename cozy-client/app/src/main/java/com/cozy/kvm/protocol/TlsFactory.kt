package com.cozy.kvm.protocol

import android.util.Log
import java.net.Socket
import java.security.SecureRandom
import java.security.cert.X509Certificate
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket
import javax.net.ssl.X509TrustManager

/**
 * Deskflow encrypts the protocol with TLS using a *self-signed* certificate and
 * trust-on-first-use fingerprint pinning (there is no CA chain to validate).
 * On a LAN the pragmatic equivalent is to accept the server's certificate and
 * rely on the network being trusted — so we use a permissive trust manager.
 *
 * SECURITY NOTE: this trusts any certificate. That matches Barrier/Synergy
 * Android clients and is acceptable for a home LAN, but it is NOT safe over an
 * untrusted network. A future version should pin the server fingerprint like
 * deskflow's own client does (see net/FingerprintDatabase.cpp).
 */
object TlsFactory {

    private val trustAll = object : X509TrustManager {
        override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) {}
        override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {}
        override fun getAcceptedIssuers(): Array<X509Certificate> = arrayOf()
    }

    /** Wrap an already-connected plain socket in a TLS session and handshake. */
    fun wrap(plain: Socket, host: String, port: Int): SSLSocket {
        val ctx = SSLContext.getInstance("TLS")
        ctx.init(null, arrayOf(trustAll), SecureRandom())
        val ssl = ctx.socketFactory.createSocket(plain, host, port, true) as SSLSocket
        ssl.useClientMode = true
        ssl.startHandshake()
        Log.i("TlsFactory", "TLS established with ${ssl.session.cipherSuite}")
        return ssl
    }
}
