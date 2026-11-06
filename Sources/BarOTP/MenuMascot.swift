import AppKit
import SwiftUI
import ImageIO
import UniformTypeIdentifiers

@MainActor final class MenuMascot: ObservableObject {
    struct Choice: Identifiable { let id: String; let name: String }
    struct Frame { let image: NSImage; let duration: TimeInterval }
    static let builtins = [Choice(id: "sparrow-flutter", name: "파닥이는 참새"), Choice(id: "sparrow-doze", name: "조는 참새")]
    @Published var enabled: Bool {
        didSet { defaults.set(enabled, forKey: "animationEnabled"); resetAnimation() }
    }
    @Published var iconZoom: Int {
        didSet { defaults.set(iconZoom, forKey: "iconZoom"); loadSelection() }
    }
    @Published var iconSize: Int {
        didSet { defaults.set(iconSize, forKey: "iconSize"); loadSelection() }
    }
    @Published var selected: String {
        didSet { defaults.set(selected, forKey: "selectedIcon"); loadSelection() }
    }
    @Published private(set) var customs: [Choice] = []
    @Published private(set) var preview = NSImage()
    private let defaults: UserDefaults
    private let directory: URL
    private weak var button: NSStatusBarButton?
    private var timer: Timer?
    private var generation = 0
    private var frames: [Frame] = []
    private var observers: [NSObjectProtocol] = []
    private var sleeping = false
    private var importPanel: NSOpenPanel?
    nonisolated static var customDirectory: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("PadakKey/Icons", isDirectory: true)
    }

    init(defaults: UserDefaults = .standard, directory: URL = MenuMascot.customDirectory) {
        self.defaults = defaults
        self.directory = directory
        enabled = defaults.object(forKey: "animationEnabled") as? Bool
            ?? defaults.object(forKey: "earAnimationEnabled") as? Bool ?? true
        let savedZoom = defaults.integer(forKey: "iconZoom")
        iconZoom = [100, 125, 150, 175, 200].contains(savedZoom) ? savedZoom : 100
        let savedSize = defaults.integer(forKey: "iconSize")
        iconSize = [16, 20, 22, 24].contains(savedSize) ? savedSize : 24
        selected = defaults.string(forKey: "selectedIcon") ?? "sparrow-flutter"
        refreshCustoms()
        if !Self.builtins.contains(where: { $0.id == selected }) && !customs.contains(where: { $0.id == selected }) {
            selected = "sparrow-flutter"
        }
        loadSelection()
    }
    static func asset(_ name: String) -> NSImage? {
        let url = Bundle.main.url(forResource: name, withExtension: "png")
            ?? Bundle.module.url(forResource: name, withExtension: "png")
        return url.flatMap { NSImage(contentsOf: $0) }
    }
    func refreshCustoms() {
        let files = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []
        customs = files.filter { ["png", "gif"].contains($0.pathExtension.lowercased()) }
            .sorted { $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending }
            .map { Choice(id: $0.lastPathComponent, name: $0.deletingPathExtension().lastPathComponent) }
    }
    func openIconFolder() {
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            NSWorkspace.shared.open(directory)
        } catch { showError(error.localizedDescription) }
    }
    func importIcon() {
        guard importPanel == nil else { importPanel?.makeKeyAndOrderFront(nil); return }
        let panel = NSOpenPanel()
        panel.title = "아이콘 추가"
        panel.message = "투명 PNG 또는 애니메이션 GIF를 선택하세요. GIF는 애니메이션을 켰을 때만 움직입니다."
        panel.allowedContentTypes = [.png, .gif]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        importPanel = panel
        NSApp.activate(ignoringOtherApps: true)
        // Retain an asynchronous panel independently of the transient popover.
        panel.begin { [weak self] response in
            guard let self else { return }
            defer { self.importPanel = nil }
            guard response == .OK, let url = panel.url else { return }
            do { try self.addIcon(from: url) }
            catch { self.showError(error.localizedDescription) }
        }
    }
    func addIcon(from url: URL) throws {
        let access = url.startAccessingSecurityScopedResource()
        defer { if access { url.stopAccessingSecurityScopedResource() } }
        _ = try Self.readCustom(url) // Validate before copying or selecting.
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let stem = String(url.deletingPathExtension().lastPathComponent.prefix(80))
        let ext = url.pathExtension.lowercased()
        var destination = directory.appendingPathComponent("\(stem).\(ext)")
        var suffix = 2
        while FileManager.default.fileExists(atPath: destination.path) {
            destination = directory.appendingPathComponent("\(stem)-\(suffix).\(ext)"); suffix += 1
        }
        try FileManager.default.copyItem(at: url, to: destination)
        refreshCustoms()
        selected = destination.lastPathComponent
    }
    private func showError(_ message: String) {
        let alert = NSAlert()
        alert.messageText = "아이콘을 불러올 수 없습니다"
        alert.informativeText = message
        alert.addButton(withTitle: "확인")
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
    private func loadSelection() {
        if selected == "sparrow-flutter" || selected == "sparrow-doze" {
            let names: [(String, TimeInterval)] = selected == "sparrow-flutter"
                ? [("SparrowIdle", 3), ("SparrowFlap", 0.14), ("SparrowIdle", 0.14), ("SparrowFlap", 0.14), ("SparrowIdle", 0.14), ("SparrowFlap", 0.14), ("SparrowIdle", 0.3)]
                : [("SparrowIdle", 3), ("SparrowClosed", 0.5), ("SparrowNod", 1), ("SparrowClosed", 0.4), ("SparrowNod", 0.8), ("SparrowClosed", 0.3)]
            frames = names.compactMap { name, duration in
                Self.asset(name).map { Frame(image: Self.render($0, builtin: true, size: iconSize, zoom: iconZoom), duration: duration) }
            }
        } else {
            do {
                guard customs.contains(where: { $0.id == selected }) else { throw IconError.invalid }
                frames = try Self.readCustom(directory.appendingPathComponent(selected), size: iconSize, zoom: iconZoom)
            } catch {
                showError(error.localizedDescription)
                selected = "sparrow-flutter"
                return
            }
        }
        preview = frames.first?.image ?? NSImage(systemSymbolName: "lock.shield", accessibilityDescription: "PadakKey") ?? NSImage()
        resetAnimation()
    }
    func attach(to button: NSStatusBarButton?) {
        self.button = button
        button?.imageScaling = .scaleProportionallyDown
        let center = NSWorkspace.shared.notificationCenter
        observers.append(center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.sleeping = true; self?.resetAnimation() }
        })
        observers.append(center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.sleeping = false; self?.resetAnimation() }
        })
        observers.append(center.addObserver(forName: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.resetAnimation() }
        })
        resetAnimation()
    }
    private func resetAnimation() {
        generation += 1
        timer?.invalidate(); timer = nil
        button?.image = preview
        guard enabled, !sleeping, frames.count > 1, !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion else { return }
        schedule(after: 0, generation: generation)
    }
    private func schedule(after index: Int, generation token: Int) {
        let timer = Timer(timeInterval: frames[index].duration, repeats: false) { [weak self] _ in
            Task { @MainActor in
                guard let self, self.enabled, self.generation == token else { return }
                let next = (index + 1) % self.frames.count
                self.button?.image = self.frames[next].image
                self.schedule(after: next, generation: token)
            }
        }
        timer.tolerance = 0.02
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }
    enum IconError: LocalizedError {
        case invalid, tooLarge
        var errorDescription: String? {
            switch self {
            case .invalid: return "올바른 PNG 또는 GIF 파일을 선택하세요."
            case .tooLarge: return "10MB 이하, 최대 4096px, 120프레임 이하의 작은 아이콘을 사용하세요. 애니메이션 전체 픽셀 수는 3,200만 이하여야 합니다."
            }
        }
    }
    static func readCustom(_ url: URL, size: Int = 22, zoom: Int = 100) throws -> [Frame] {
        guard ["png", "gif"].contains(url.pathExtension.lowercased()) else { throw IconError.invalid }
        let bytes = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        guard bytes > 0, bytes <= 10 * 1024 * 1024 else { throw IconError.tooLarge }
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              let type = CGImageSourceGetType(source), [UTType.png.identifier, UTType.gif.identifier].contains(type as String) else { throw IconError.invalid }
        let count = CGImageSourceGetCount(source)
        guard count > 0, count <= 120 else { throw IconError.tooLarge }
        var total = 0
        for index in 0..<count {
            guard let props = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any],
                  let width = props[kCGImagePropertyPixelWidth] as? Int,
                  let height = props[kCGImagePropertyPixelHeight] as? Int,
                  width > 0, height > 0, width <= 4096, height <= 4096 else { throw IconError.tooLarge }
            total += width * height
            guard total <= 32_000_000 else { throw IconError.tooLarge }
        }
        var result: [Frame] = []
        for index in 0..<count {
            guard let cg = CGImageSourceCreateImageAtIndex(source, index, nil) else { throw IconError.invalid }
            let props = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any]
            let gif = props?[kCGImagePropertyGIFDictionary] as? [CFString: Any]
            let delay = gif?[kCGImagePropertyGIFUnclampedDelayTime] as? Double ?? gif?[kCGImagePropertyGIFDelayTime] as? Double ?? 0.1
            // Rasterize each frame once: NSImage must never animate itself while OFF.
            let image = render(NSImage(cgImage: cg, size: NSSize(width: cg.width, height: cg.height)), builtin: false, size: size, zoom: zoom)
            result.append(Frame(image: image, duration: max(0.06, min(10, delay.isFinite ? delay : 0.1))))
        }
        return result
    }
    private static func render(_ source: NSImage, builtin: Bool, size pixels: Int, zoom: Int = 100) -> NSImage {
        let size = NSSize(width: pixels, height: pixels)
        let image = NSImage(size: size)
        image.lockFocus()
        NSGraphicsContext.current?.imageInterpolation = .none
        let crop = builtin
            ? NSRect(x: source.size.width * 0.13, y: source.size.height * 0.12, width: source.size.width * 0.75, height: source.size.height * 0.75)
            : NSRect(origin: .zero, size: source.size)
        let scale = min(size.width / crop.width, size.height / crop.height) * CGFloat(min(200, max(100, zoom))) / 100
        let target = NSRect(x: (size.width - crop.width * scale) / 2, y: (size.height - crop.height * scale) / 2, width: crop.width * scale, height: crop.height * scale)
        source.draw(in: target, from: crop, operation: .copy, fraction: 1)
        image.unlockFocus()
        image.isTemplate = false
        image.accessibilityDescription = "PadakKey"
        return image
    }
}
