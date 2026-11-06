import SwiftUI
import Combine
import AppKit
import Vision
import UniformTypeIdentifiers
import ServiceManagement
import OTPCore

@main enum BarOTPEntry {
    @MainActor static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)
        let delegate = AppDelegate()
        app.delegate = delegate
        withExtendedLifetime(delegate) { app.run() }
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate, NSPopoverDelegate {
    private let vault = Vault()
    private let registration = RegistrationWindow()
    private let mascot = MenuMascot()
    private var statusItem: NSStatusItem?
    private var iconSizeObservation: AnyCancellable?
    private let popover = NSPopover()
    func applicationDidFinishLaunching(_ notification: Notification) {
        if let bundleID = Bundle.main.bundleIdentifier,
           let existing = NSRunningApplication.runningApplications(withBundleIdentifier: bundleID).first(where: { $0.processIdentifier < ProcessInfo.processInfo.processIdentifier }) {
            existing.activate(options: [.activateIgnoringOtherApps])
            NSApp.terminate(nil)
            return
        }
        // App-level ownership keeps the status item and registration window alive.
        let item = NSStatusBar.system.statusItem(withLength: CGFloat(mascot.iconSize + 6))
        item.autosaveName = "PadakKey.MainStatusItem"
        item.isVisible = true
        mascot.attach(to: item.button)
        item.button?.toolTip = "PadakKey 0.1.15"
        item.button?.target = self
        item.button?.action = #selector(togglePopover)
        statusItem = item
        iconSizeObservation = mascot.$iconSize.sink { [weak self] size in
            self?.statusItem?.length = CGFloat(size + 6)
        }
        popover.delegate = self
        popover.behavior = .transient
        popover.contentSize = NSSize(width: 370, height: 280)
        popover.contentViewController = NSHostingController(rootView: MainView(vault: vault, mascot: mascot, registration: registration, resize: { [weak self] height in
            self?.popover.contentSize = NSSize(width: 370, height: height)
        }, importIcon: { [weak self] in
            guard let self else { return }
            self.popover.performClose(nil)
            DispatchQueue.main.async { self.mascot.importIcon() }
        }, authorize: { [weak self] in self?.authorizeKeychain() }, closeAfterCopy: { [weak self] in
            self?.popover.performClose(nil)
        }, addAccount: { [weak self] in
            guard let self else { return }
            self.popover.performClose(nil)
            // Let the popover finish its close event before activating the window.
            DispatchQueue.main.async { self.registration.show(vault: self.vault) }
        }))
    }
    @objc private func togglePopover() {
        guard let button = statusItem?.button else { return }
        if popover.isShown { popover.performClose(nil) }
        else {
            NSApp.activate(ignoringOtherApps: true)
            vault.load()
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        }
    }
    private func authorizeKeychain() {
        guard !vault.isLoading else { return }
        // Keep the parent popover alive while SecurityAgent owns focus.
        popover.behavior = .applicationDefined
        vault.load(allowInteraction: true) { [weak self] _ in self?.popover.behavior = .transient }
    }
    func popoverDidClose(_ notification: Notification) { vault.clear() }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func applicationWillTerminate(_ notification: Notification) { Clipboard.shared.clear() }
}

struct MainView: View {
    @ObservedObject var vault: Vault
    @ObservedObject var mascot: MenuMascot
    @ObservedObject var registration: RegistrationWindow
    var resize: (CGFloat) -> Void
    var importIcon: () -> Void
    private var visibleAccounts: [OTPAccount] {
        vault.accounts.filter { search.isEmpty || ($0.issuer + " " + $0.name).localizedCaseInsensitiveContains(search) }
    }
    private var panelHeight: CGFloat {
        if vault.needsAuthorization || vault.error != nil { return 310 }
        if !vault.loaded { return 210 }
        if vault.accounts.isEmpty { return 350 }
        return 168 + CGFloat(min(3, max(1, visibleAccounts.count))) * 112
    }
    var authorize: () -> Void
    var closeAfterCopy: () -> Void
    var addAccount: () -> Void
    @State private var search = ""
    @State private var copied: UUID?
    @State private var deleting: OTPAccount?
    @State private var loginEnabled = SMAppService.mainApp.status == .enabled
    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 10) {
                Image(nsImage: mascot.preview).resizable().interpolation(.none).scaledToFit().frame(width: 32, height: 32)
                VStack(alignment: .leading, spacing: 2) {
                    Text("PadakKey").font(.headline)
                }
                Spacer()
                Button { addAccount() } label: { Image(systemName: "plus") }.buttonStyle(.bordered).help("계정 추가")
            }.padding(20).fixedSize(horizontal: false, vertical: true)
            Divider()
            VStack(spacing: 0) {
                if let error = vault.error {
                    Text(error).font(.caption).foregroundStyle(.red).padding()
                    Button("키체인 다시 열기") { vault.load() }.padding(.bottom)
                }
                if vault.needsAuthorization {
                    VStack(spacing: 10) {
                        Image(systemName: "lock.shield").font(.title).foregroundStyle(.teal)
                        Text("저장된 계정을 열려면 접근 허용이 필요합니다.")
                        Text("macOS 창에서 Mac 로그인 암호를 입력하고 ‘항상 허용’을 선택하세요.")
                            .font(.caption).foregroundStyle(.secondary).multilineTextAlignment(.center)
                        Button("접근 허용") { authorize() }.buttonStyle(.borderedProminent).tint(.teal)
                    }.padding(22)
                }
                if vault.isLoading {
                    ProgressView("인증 코드 불러오는 중…").padding(30)
                }
                if vault.loaded && vault.accounts.isEmpty {
                    VStack(spacing: 14) {
                        Image(systemName: "qrcode.viewfinder").font(.system(size: 42, weight: .light)).foregroundStyle(.teal)
                        Text("첫 번째 계정을 등록하세요").font(.headline)
                        Text("서비스의 2단계 인증 설정에서\nQR 코드 또는 인증 키를 가져오세요.").multilineTextAlignment(.center).foregroundStyle(.secondary)
                        Button("계정 추가") { addAccount() }.buttonStyle(.borderedProminent).tint(.teal)
                    }.frame(maxWidth: .infinity).padding(.vertical, 42)
                } else if vault.loaded {
                    TextField("서비스 또는 계정 검색", text: $search).textFieldStyle(.roundedBorder).padding(14)
                    ScrollView {
                        LazyVStack(spacing: 10) {
                            if visibleAccounts.isEmpty {
                                Text("검색 결과가 없습니다.").foregroundStyle(.secondary).padding(.vertical, 24)
                            }
                            ForEach(visibleAccounts) { account in
                                TimelineView(.periodic(from: .now, by: 1)) { context in
                                    accountRow(account, at: context.date)
                                }
                            }
                        }.padding(.horizontal, 14).padding(.bottom, 14)
                    }.frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
            Divider()
            HStack {
                Menu {
                    Toggle("애니메이션", isOn: $mascot.enabled)
                    Menu("아이콘 크기") {
                        Picker("크기", selection: $mascot.iconSize) {
                            ForEach([16, 20, 22, 24], id: \.self) { size in Text("\(size) pt").tag(size) }
                        }.pickerStyle(.inline)
                    }
                    Menu("아이콘 확대 (여백 줄이기)") {
                        Picker("확대", selection: $mascot.iconZoom) {
                            ForEach([100, 125, 150, 175, 200], id: \.self) { zoom in Text("\(zoom)%").tag(zoom) }
                        }.pickerStyle(.inline)
                    }
                    Menu("아이콘 선택") {
                        Picker("기본 아이콘", selection: $mascot.selected) {
                            ForEach(MenuMascot.builtins) { choice in Text(choice.name).tag(choice.id) }
                            ForEach(mascot.customs) { choice in Text(choice.name).tag(choice.id) }
                        }.pickerStyle(.inline)
                    }
                    Button("아이콘 추가…") { importIcon() }
                    Button("아이콘 폴더 열기") { mascot.openIconFolder() }
                    Divider()
                    Toggle("로그인 시 실행", isOn: Binding(get: { loginEnabled }, set: { enabled in
                        do {
                            if enabled { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
                            loginEnabled = SMAppService.mainApp.status == .enabled
                            if enabled && !loginEnabled { SMAppService.openSystemSettingsLoginItems() }
                        } catch { vault.error = error.localizedDescription }
                    }))
                    Divider()
                    Button("종료") { Clipboard.shared.clear(); NSApp.terminate(nil) }
                } label: { Image(systemName: "gearshape") }.menuStyle(.borderlessButton).frame(width: 30)
                Spacer()
                Text("로컬 키체인 저장 · 복사 후 30초 뒤 지움").font(.system(size: 10)).foregroundStyle(.secondary)
            }.padding(12).fixedSize(horizontal: false, vertical: true)
        }.frame(width: 370, height: panelHeight)
        .onAppear { mascot.refreshCustoms(); loginEnabled = SMAppService.mainApp.status == .enabled; resize(panelHeight) }
        .onChange(of: panelHeight) { height in resize(height) }
        .onChange(of: vault.loaded) { loaded in if loaded { search = ""; copied = nil } }
        .onDisappear { copied = nil }
        .onReceive(NSWorkspace.shared.notificationCenter.publisher(for: NSWorkspace.sessionDidResignActiveNotification)) { _ in clearSession() }
        .onReceive(NSWorkspace.shared.notificationCenter.publisher(for: NSWorkspace.willSleepNotification)) { _ in clearSession() }
        .onReceive(DistributedNotificationCenter.default().publisher(for: Notification.Name("com.apple.screenIsLocked"))) { _ in clearSession() }
        .alert("계정을 삭제할까요?", isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } })) {
            Button("취소", role: .cancel) { deleting = nil }
            Button("삭제", role: .destructive) { if let account = deleting { vault.remove(account) }; deleting = nil }
        } message: { Text("이 Mac에서만 삭제됩니다. 다른 인증 수단이나 복구 코드가 있는지 확인하세요.") }
    }
    private func clearSession() { registration.close(); vault.clear(); Clipboard.shared.clear() }
    private func accountRow(_ account: OTPAccount, at date: Date) -> some View {
        let code = try? account.code(at: date)
        let remaining = account.period - Int(date.timeIntervalSince1970) % account.period
        return VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text(account.issuer.isEmpty ? "인증 계정" : account.issuer).font(.system(size: 13, weight: .semibold)).lineLimit(1)
                    Text(account.name).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                }
                Spacer()
                Button(role: .destructive) { deleting = account } label: { Image(systemName: "trash").font(.caption) }.buttonStyle(.plain).foregroundStyle(.secondary).help("계정 삭제")
            }
            HStack {
                Button {
                    if let current = try? account.code() {
                        Clipboard.shared.copy(current)
                        closeAfterCopy()
                    }
                } label: {
                    HStack(spacing: 10) {
                        Text(code.map { String($0.prefix($0.count/2)) + " " + String($0.suffix($0.count/2)) } ?? "오류").font(.system(size: 29, weight: .medium, design: .monospaced))
                        Image(systemName: copied == account.id ? "checkmark" : "doc.on.doc").font(.caption)
                    }.foregroundStyle(.primary)
                }.buttonStyle(.plain).disabled(code == nil).help("OTP 복사")
                Spacer()
                ZStack {
                    Circle().stroke(.quaternary, lineWidth: 3)
                    Circle().trim(from: 0, to: Double(remaining)/Double(account.period)).stroke(remaining <= 5 ? Color.orange : Color.teal, style: StrokeStyle(lineWidth: 3, lineCap: .round)).rotationEffect(.degrees(-90))
                    Text("\(remaining)").font(.system(size: 10, weight: .medium, design: .monospaced))
                }.frame(width: 30, height: 30)
            }
        }.padding(14).background(.quaternary.opacity(0.4), in: RoundedRectangle(cornerRadius: 12))
        .onChange(of: code) { _ in if copied == account.id { copied = nil } }
    }
}
struct AddView: View {
    @ObservedObject var vault: Vault
    var done: () -> Void
    var chooseImage: (@escaping (URL?) -> Void) -> Void
    @State private var mode = 0
    @State private var uri = ""
    @State private var issuer = ""
    @State private var name = ""
    @State private var secret = ""
    @State private var algorithm = Algorithm.sha1
    @State private var digits = 6
    @State private var period = "30"
    @State private var error: String?
    @State private var importedName: String?
    @State private var isReading = false
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Picker("등록 방식", selection: $mode) { Text("QR / URI").tag(0); Text("수동 입력").tag(1) }.pickerStyle(.segmented)
            if mode == 0 {
                Button { importQR() } label: { Label("QR 코드 이미지 열기", systemImage: "qrcode") }.frame(maxWidth: .infinity).disabled(isReading)
                if isReading { ProgressView("QR 이미지 읽는 중…").controlSize(.small) }
                SecureField("otpauth://totp/…", text: $uri)
                if let importedName {
                    Label("QR 인식 완료: \(importedName)", systemImage: "checkmark.circle.fill")
                        .font(.caption).foregroundStyle(.teal)
                    Text("아래 ‘계정 저장’을 누르면 등록됩니다.").font(.caption).foregroundStyle(.secondary)
                }
                Text("QR 이미지 파일 또는 등록 URI를 사용하세요. Google Authenticator 일괄 이전 QR은 지원하지 않습니다.").font(.caption).foregroundStyle(.secondary)
            } else {
                TextField("서비스 이름 (예: GitHub)", text: $issuer)
                TextField("계정 (예: me@example.com)", text: $name)
                SecureField("인증 키 (Base32)", text: $secret)
                DisclosureGroup("고급 설정") {
                    Picker("알고리즘", selection: $algorithm) { ForEach(Algorithm.allCases, id: \.self) { Text($0.rawValue).tag($0) } }
                    Picker("자릿수", selection: $digits) { Text("6").tag(6); Text("8").tag(8) }
                    TextField("갱신 주기 (초)", text: $period)
                }
            }
            if let error { Text(error).font(.caption).foregroundStyle(.red).fixedSize(horizontal: false, vertical: true) }
            Button("계정 저장") {
                do {
                    let account: OTPAccount
                    if mode == 0 { account = try OTPAccount.parse(uri) }
                    else { guard let seconds = Int(period) else { throw OTPError.invalid("갱신 주기를 숫자로 입력하세요.") }; account = try OTPAccount(issuer: issuer, name: name, secret: secret, algorithm: algorithm, digits: digits, period: seconds) }
                    if vault.loaded { save(account) }
                    else {
                        vault.load(allowInteraction: true) { success in
                            if success { save(account) }
                            else { error = vault.error ?? "저장된 계정을 열 수 없습니다. 키체인 접근을 허용한 뒤 다시 저장하세요." }
                        }
                    }
                } catch { self.error = error.localizedDescription }
            }.buttonStyle(.borderedProminent).tint(.teal).disabled(isReading || vault.isLoading)
        }.textFieldStyle(.roundedBorder).padding(22).frame(width: 410)
    }
    private func save(_ account: OTPAccount) {
        do { try vault.add(account); uri = ""; secret = ""; done() }
        catch { self.error = error.localizedDescription }
    }
    private func importQR() {
        chooseImage { url in
            guard let url else { return }
            isReading = true; error = nil; importedName = nil; uri = ""
            DispatchQueue.global(qos: .userInitiated).async {
                let result = Result { try QRReader.read(url: url) }
                DispatchQueue.main.async {
                    isReading = false
                    do {
                        let value = try result.get()
                        let account = try OTPAccount.parse(value)
                        uri = value
                        importedName = account.issuer.isEmpty ? account.name : account.issuer + " · " + account.name
                    } catch { self.error = error.localizedDescription }
                }
            }
        }
    }
}

@MainActor final class RegistrationWindow: NSObject, ObservableObject, NSWindowDelegate {
    private var window: NSWindow?
    private var imagePanel: NSOpenPanel?
    func show(vault: Vault) {
        if let window { NSApp.activate(ignoringOtherApps: true); window.makeKeyAndOrderFront(nil); return }
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 454, height: 360), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "PadakKey · 계정 추가"
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.contentView = NSHostingView(rootView: AddView(vault: vault, done: { [weak self] in self?.close() }, chooseImage: { [weak self] completion in self?.chooseImage(completion) }))
        window.center()
        self.window = window
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
    private func chooseImage(_ completion: @escaping (URL?) -> Void) {
        guard let window, imagePanel == nil else { return }
        let panel = NSOpenPanel()
        imagePanel = panel
        panel.allowedContentTypes = [.image]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.prompt = "QR 코드 읽기"
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
        // Use the explicitly owned registration window, never NSApp.keyWindow.
        panel.beginSheetModal(for: window) { [weak self] response in
            let url = response == .OK ? panel.url : nil
            self?.imagePanel = nil
            completion(url)
        }
    }
    func close() {
        imagePanel?.cancel(nil)
        window?.close()
    }
    func windowWillClose(_ notification: Notification) {
        window?.contentView = nil
        window = nil
    }
}
