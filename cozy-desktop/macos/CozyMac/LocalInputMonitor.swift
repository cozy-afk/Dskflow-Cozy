import CoreGraphics
import Foundation

/// Fires `onPhysicalActivity` when the user touches THIS Mac's own keyboard/mouse,
/// ignoring events that deskflow injected (so receiving the PC's cursor doesn't
/// count). macOS equivalent of the Windows injected-flag filter: hardware events
/// have source PID 0; events posted by a process (deskflow) carry that PID.
///
/// Requires Accessibility permission (already needed for deskflow injection).
final class LocalInputMonitor {
    var onPhysicalActivity: (() -> Void)?

    private var tap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    private var lastFire: TimeInterval = 0

    func start() {
        let mask: CGEventMask =
            (1 << CGEventType.keyDown.rawValue) |
            (1 << CGEventType.leftMouseDown.rawValue) |
            (1 << CGEventType.rightMouseDown.rawValue) |
            (1 << CGEventType.mouseMoved.rawValue) |
            (1 << CGEventType.scrollWheel.rawValue)

        let callback: CGEventTapCallBack = { _, _, event, refcon in
            let me = Unmanaged<LocalInputMonitor>.fromOpaque(refcon!).takeUnretainedValue()
            let pid = event.getIntegerValueField(.eventSourceUnixProcessID)
            if pid == 0 { me.fire() }   // 0 == real hardware input
            return Unmanaged.passUnretained(event)
        }

        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .listenOnly,
            eventsOfInterest: mask,
            callback: callback,
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else {
            print("Cozy: failed to create event tap — grant Accessibility permission.")
            return
        }
        self.tap = tap
        let src = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        self.runLoopSource = src
        CFRunLoopAddSource(CFRunLoopGetMain(), src, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
    }

    func stop() {
        if let tap = tap { CGEvent.tapEnable(tap: tap, enable: false) }
        if let src = runLoopSource { CFRunLoopRemoveSource(CFRunLoopGetMain(), src, .commonModes) }
        tap = nil
        runLoopSource = nil
    }

    private func fire() {
        let now = Date().timeIntervalSince1970
        if now - lastFire < 0.15 { return }   // throttle
        lastFire = now
        DispatchQueue.main.async { self.onPhysicalActivity?() }
    }
}
