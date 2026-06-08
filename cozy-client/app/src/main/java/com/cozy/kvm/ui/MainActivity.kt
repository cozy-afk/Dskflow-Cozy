package com.cozy.kvm.ui

import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.lifecycle.lifecycleScope
import com.cozy.kvm.R
import com.cozy.kvm.databinding.ActivityMainBinding
import com.cozy.kvm.protocol.ConnState
import com.cozy.kvm.service.ConnectionService
import com.cozy.kvm.service.CozyAccessibilityService
import kotlinx.coroutines.launch

/**
 * Minimal control panel: enter the server address, enable the accessibility
 * service, connect. Real configuration of *where* this screen sits relative to
 * the PC happens in the deskflow server's own drag-to-arrange GUI.
 */
class MainActivity : ComponentActivity() {

    private lateinit var binding: ActivityMainBinding

    private val notifPermission = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { /* result ignored; notification is best-effort */ }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val prefs = getSharedPreferences("cozy", MODE_PRIVATE)
        binding.host.setText(prefs.getString("host", ""))
        binding.port.setText(prefs.getInt("port", 24800).toString())
        binding.name.setText(prefs.getString("name", "android-tablet"))
        binding.useTls.isChecked = prefs.getBoolean("tls", true)

        binding.enableA11y.setOnClickListener {
            startActivity(Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS))
            Toast.makeText(this, R.string.enable_a11y_hint, Toast.LENGTH_LONG).show()
        }

        binding.enableKeyboard.setOnClickListener {
            // Opens the system keyboard list so the user can enable/switch to "Cozy keyboard".
            startActivity(Intent(Settings.ACTION_INPUT_METHOD_SETTINGS))
            Toast.makeText(this, "Enable \"Cozy keyboard\", then switch to it when you want to type.", Toast.LENGTH_LONG).show()
        }

        binding.connect.setOnClickListener {
            val host = binding.host.text.toString().trim()
            if (host.isEmpty()) { toast("Enter the PC's IP address"); return@setOnClickListener }
            if (CozyAccessibilityService.instance == null) {
                toast(getString(R.string.enable_a11y_hint)); return@setOnClickListener
            }
            val port = binding.port.text.toString().toIntOrNull() ?: 24800
            val name = binding.name.text.toString().ifBlank { "android-tablet" }
            val tls = binding.useTls.isChecked
            prefs.edit().putString("host", host).putInt("port", port).putString("name", name).putBoolean("tls", tls).apply()
            ensureNotificationPermission()
            ConnectionService.start(this, host, port, name, tls)
        }

        binding.disconnect.setOnClickListener { ConnectionService.stop(this) }

        lifecycleScope.launch {
            ConnectionService.status.collect { status ->
                binding.status.text = when (status.state) {
                    ConnState.CONNECTING -> "Connecting… ${status.detail}"
                    ConnState.HANDSHAKING -> "Handshaking ${status.detail}"
                    ConnState.CONNECTED -> "Connected ✓  (move your PC mouse onto this tablet)"
                    ConnState.DISCONNECTED -> "Disconnected"
                    ConnState.ERROR -> "Error: ${status.detail}"
                }
            }
        }
    }

    override fun onResume() {
        super.onResume()
        binding.a11yStatus.text = if (CozyAccessibilityService.instance != null)
            "Accessibility: enabled ✓" else "Accessibility: NOT enabled — tap the button above"
    }

    private fun ensureNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            notifPermission.launch(android.Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    private fun toast(msg: String) = Toast.makeText(this, msg, Toast.LENGTH_SHORT).show()
}
