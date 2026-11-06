import AppKit
import ImageIO
import UniformTypeIdentifiers

extension Bundle {
    static var module: Bundle { Bundle(path: CommandLine.arguments[1])! }
}
@main enum MascotChecks {
    @MainActor static func main() throws {
        _ = NSApplication.shared
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let suite = "PadakKeyChecks-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        defer { try? FileManager.default.removeItem(at: root); defaults.removePersistentDomain(forName: suite) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defaults.set(false, forKey: "earAnimationEnabled")
        let folder = root.appendingPathComponent("Icons")
        let mascot = MenuMascot(defaults: defaults, directory: folder)
        assert(!mascot.enabled, "Legacy OFF must migrate")
        assert(mascot.preview.size.width == 22)
        func timer(_ object: MenuMascot) -> Timer? {
            Mirror(reflecting: object).children.first(where: { $0.label == "timer" })?.value as? Timer
        }
        assert(timer(mascot) == nil)
        mascot.enabled = true
        if !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion { assert(timer(mascot)?.isValid == true) }
        let pending = timer(mascot)
        mascot.enabled = false
        assert(timer(mascot) == nil && pending?.isValid != true, "OFF cancels pending frames")
        mascot.selected = "sparrow-doze"
        assert(timer(mascot) == nil, "Selection must not enable animation")
        assert(mascot.preview.size.width == 22)
        let png = Bundle.module.url(forResource: "SparrowIdle", withExtension: "png")!
        try mascot.addIcon(from: png)
        assert(mascot.customs.count == 1)
        let imported = try MenuMascot.readCustom(folder.appendingPathComponent(mascot.selected))
        assert(imported.count == 1)
        try mascot.addIcon(from: png)
        assert(mascot.customs.count == 2, "Duplicate import must not overwrite")
        let gif = root.appendingPathComponent("animated.gif")
        let source = CGImageSourceCreateWithURL(png as CFURL, nil)!
        let frame = CGImageSourceCreateImageAtIndex(source, 0, nil)!
        let destination = CGImageDestinationCreateWithURL(gif as CFURL, UTType.gif.identifier as CFString, 2, nil)!
        for _ in 0..<2 { CGImageDestinationAddImage(destination, frame, [kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFDelayTime: 0.2]] as CFDictionary) }
        assert(CGImageDestinationFinalize(destination))
        try mascot.addIcon(from: gif)
        assert(timer(mascot) == nil, "GIF stays static while OFF")
        let decoded = try MenuMascot.readCustom(gif)
        assert(decoded.count == 2 && abs(decoded[0].duration - 0.2) < 0.01)
        mascot.enabled = true
        if !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion { assert(timer(mascot)?.isValid == true) }
        mascot.enabled = false
        let restored = MenuMascot(defaults: defaults, directory: folder)
        assert(restored.selected == mascot.selected && !restored.enabled)
        let invalid = root.appendingPathComponent("invalid.png")
        try Data("not an image".utf8).write(to: invalid)
        do { _ = try MenuMascot.readCustom(invalid); fatalError("Accepted invalid image") } catch {}
        print("PASS: builtin choices, OFF migration, timer cancellation, PNG/GIF import, duplicate preservation, selection persistence, invalid image rejection")
    }
}
