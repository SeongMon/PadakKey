import AppKit
import SwiftUI

@MainActor final class MenuMascot: ObservableObject {
    @Published var enabled: Bool {
        didSet {
            UserDefaults.standard.set(enabled, forKey: "earAnimationEnabled")
            resetAnimation()
        }
    }
    private weak var button: NSStatusBarButton?
    private var timer: Timer?
    private var generation = 0
    private var observers: [NSObjectProtocol] = []
    private var sleeping = false
    private lazy var idle = Self.render(flapping: false)
    private lazy var flap = Self.render(flapping: true)

    init() {
        enabled = UserDefaults.standard.object(forKey: "earAnimationEnabled") as? Bool ?? true
    }
    static func asset(_ name: String) -> NSImage? {
        let url = Bundle.main.url(forResource: name, withExtension: "png")
            ?? Bundle.module.url(forResource: name, withExtension: "png")
        return url.flatMap { NSImage(contentsOf: $0) }
    }
    func attach(to button: NSStatusBarButton?) {
        self.button = button
        button?.image = idle
        button?.imageScaling = .scaleNone
        let center = NSWorkspace.shared.notificationCenter
        observers.append(center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.sleeping = true; self?.resetAnimation() }
        })
        observers.append(center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.sleeping = false; self?.resetAnimation() }
        })
        observers.append(center.addObserver(forName: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification, object: nil, queue: .main) { [weak self] _ in Task { @MainActor in self?.resetAnimation() } })
        resetAnimation()
    }
    private func resetAnimation() {
        generation += 1
        timer?.invalidate(); timer = nil
        button?.image = idle
        guard enabled, !sleeping, !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion else { return }
        let timer = Timer(timeInterval: 5, repeats: true) { [weak self] _ in Task { @MainActor in self?.flutter() } }
        timer.tolerance = 0.5
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }
    func flutter() {
        guard enabled, !sleeping, !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion else { return }
        generation += 1
        let current = generation
        for step in 0..<6 {
            DispatchQueue.main.asyncAfter(deadline: .now() + Double(step) * 0.14) { [weak self] in
                guard let self, self.generation == current else { return }
                self.button?.image = step.isMultiple(of: 2) ? self.flap : self.idle
            }
        }
    }
    // Only the upper (ear) region changes. The body always uses the approved art.
    private static func render(flapping: Bool) -> NSImage? {
        guard let base = asset("Patamon") else { return NSImage(systemSymbolName: "lock.shield", accessibilityDescription: "파닥키") }
        let alternate = flapping ? asset("PatamonFlap") : nil
        let size = NSSize(width: 22, height: 22)
        let image = NSImage(size: size, flipped: false) { rect in
            NSGraphicsContext.current?.imageInterpolation = .none
            func draw(_ source: NSImage) {
                let crop = NSRect(x: source.size.width * 0.16, y: source.size.height * 0.14, width: source.size.width * 0.72, height: source.size.height * 0.72)
                source.draw(in: rect, from: crop, operation: .sourceOver, fraction: 1, respectFlipped: false, hints: [.interpolation: NSImageInterpolation.none.rawValue])
            }
            draw(base)
            if let alternate {
                NSGraphicsContext.saveGraphicsState()
                let ears = NSRect(x: rect.minX, y: rect.minY + rect.height * 0.5, width: rect.width, height: rect.height * 0.5)
                NSBezierPath(rect: ears).addClip()
                NSGraphicsContext.current?.cgContext.clear(ears)
                draw(alternate)
                NSGraphicsContext.restoreGraphicsState()
            }
            return true
        }
        image.isTemplate = false
        image.accessibilityDescription = "파닥키"
        return image
    }
}
