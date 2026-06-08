package com.cozy.kvm.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import com.cozy.kvm.R
import com.cozy.kvm.protocol.ConnState
import com.cozy.kvm.protocol.DeskflowClient
import com.cozy.kvm.protocol.InputEvent
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlin.concurrent.thread

/**
 * Foreground service that owns the TCP connection to the Deskflow server and
 * pumps decoded events into [CozyAccessibilityService] for injection.
 *
 * It deliberately does NOT inject anything itself — separation keeps the
 * network code testable and the injection code in the one process component
 * Android allows to perform gestures.
 */
class ConnectionService : Service() {

    private var client: DeskflowClient? = null
    private var worker: Thread? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> { stopSelf(); return START_NOT_STICKY }
        }
        val host = intent?.getStringExtra(EXTRA_HOST) ?: return START_NOT_STICKY
        val port = intent.getIntExtra(EXTRA_PORT, 24800)
        val name = intent.getStringExtra(EXTRA_NAME) ?: "android-tablet"
        val tls = intent.getBooleanExtra(EXTRA_TLS, true)

        startForeground(NOTIF_ID, buildNotification("Connecting to $host…"))
        connect(host, port, name, tls)
        return START_STICKY
    }

    private fun connect(host: String, port: Int, name: String, tls: Boolean) {
        disconnect()
        val a11y = CozyAccessibilityService.instance
        val (w, h) = a11y?.displaySize() ?: run {
            val m = resources.displayMetrics
            m.widthPixels to m.heightPixels
        }
        Log.i(TAG, "connecting to $host:$port as '$name' screen ${w}x$h")
        val c = DeskflowClient(host, port, name, w, h, tls) { event ->
            if (event is InputEvent.Status) {
                _status.value = event
                updateNotification(event)
            } else {
                CozyAccessibilityService.instance?.handle(event)
            }
        }
        client = c
        worker = thread(name = "deskflow-net") { c.run() }
    }

    private fun disconnect() {
        client?.stop()
        client = null
        worker = null
    }

    override fun onDestroy() {
        disconnect()
        _status.value = InputEvent.Status(ConnState.DISCONNECTED)
        super.onDestroy()
    }

    // ---------------- notification ----------------

    private fun buildNotification(text: String): Notification {
        ensureChannel()
        val stopPi = android.app.PendingIntent.getService(
            this, 1,
            Intent(this, ConnectionService::class.java).setAction(ACTION_STOP),
            android.app.PendingIntent.FLAG_UPDATE_CURRENT or android.app.PendingIntent.FLAG_IMMUTABLE
        )
        return Notification.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_menu_send)
            .addAction(Notification.Action.Builder(null, "Disconnect", stopPi).build())
            .setOngoing(true)
            .build()
    }

    private fun updateNotification(status: InputEvent.Status) {
        val text = when (status.state) {
            ConnState.CONNECTING -> "Connecting… ${status.detail}"
            ConnState.HANDSHAKING -> "Handshaking ${status.detail}"
            ConnState.CONNECTED -> "Connected — move your mouse onto this screen"
            ConnState.DISCONNECTED -> "Disconnected"
            ConnState.ERROR -> "Error: ${status.detail}"
        }
        (getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager)
            .notify(NOTIF_ID, buildNotification(text))
    }

    private fun ensureChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val mgr = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            if (mgr.getNotificationChannel(CHANNEL_ID) == null) {
                mgr.createNotificationChannel(
                    NotificationChannel(CHANNEL_ID, "Cozy connection", NotificationManager.IMPORTANCE_LOW)
                )
            }
        }
    }

    companion object {
        private const val TAG = "ConnectionService"
        private const val CHANNEL_ID = "cozy_conn"
        private const val NOTIF_ID = 42
        const val ACTION_STOP = "com.cozy.kvm.STOP"
        const val EXTRA_HOST = "host"
        const val EXTRA_PORT = "port"
        const val EXTRA_NAME = "name"
        const val EXTRA_TLS = "tls"

        private val _status = MutableStateFlow<InputEvent.Status>(InputEvent.Status(ConnState.DISCONNECTED))
        val status: StateFlow<InputEvent.Status> = _status

        fun start(ctx: Context, host: String, port: Int, name: String, tls: Boolean) {
            val i = Intent(ctx, ConnectionService::class.java)
                .putExtra(EXTRA_HOST, host)
                .putExtra(EXTRA_PORT, port)
                .putExtra(EXTRA_NAME, name)
                .putExtra(EXTRA_TLS, tls)
            ctx.startForegroundService(i)
        }

        fun stop(ctx: Context) {
            ctx.startService(Intent(ctx, ConnectionService::class.java).setAction(ACTION_STOP))
        }
    }
}
