import Foundation
import Security
import AppKit
import OTPCore

@MainActor final class Vault: ObservableObject {
    @Published var accounts: [OTPAccount] = []
    @Published var error: String?
    @Published var loaded = false
    private let service = "app.barotp.local.v1"
    private var query: [String: Any] { [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service, kSecAttrAccount as String: "accounts"] }
    @Published var isLoading = false
    @Published var needsAuthorization = false
    private var generation = 0
    typealias Reader = (Bool) -> (OSStatus, Data?)
    private let reader: Reader
    init(reader: Reader? = nil) {
        self.reader = reader ?? { interactive in
            var request: [String: Any] = [
                kSecClass as String: kSecClassGenericPassword,
                kSecAttrService as String: "app.barotp.local.v1",
                kSecAttrAccount as String: "accounts",
                kSecReturnData as String: true,
                kSecMatchLimit as String: kSecMatchLimitOne
            ]
            if !interactive { request[kSecUseAuthenticationUI as String] = kSecUseAuthenticationUIFail }
            var result: CFTypeRef?
            let status = SecItemCopyMatching(request as CFDictionary, &result)
            return (status, result as? Data)
        }
    }
    func clear() {
        generation += 1
        accounts = []; loaded = false; isLoading = false
        error = nil; needsAuthorization = false
    }
    // Automatic opens never display a system password dialog. Only an explicit
    // authorization button may request interactive Keychain access.
    func load(allowInteraction: Bool = false, completion: ((Bool) -> Void)? = nil) {
        guard !isLoading else { completion?(false); return }
        generation += 1
        let requestID = generation
        let read = reader
        loaded = false; accounts = []; error = nil
        needsAuthorization = false; isLoading = true
        DispatchQueue.global(qos: .userInitiated).async {
            let (status, data) = read(allowInteraction)
            DispatchQueue.main.async { [weak self] in
                guard let self else { completion?(false); return }
                guard self.generation == requestID else { completion?(false); return }
                self.isLoading = false
                if [errSecUserCanceled, errSecAuthFailed, errSecInteractionNotAllowed, errSecInteractionRequired].contains(status) {
                    self.needsAuthorization = true
                    completion?(false)
                    return
                }
                do {
                    if status == errSecItemNotFound { self.accounts = [] }
                    else {
                        guard status == errSecSuccess, let data else { throw self.failure(status) }
                        let decoded = try JSONDecoder().decode([OTPAccount].self, from: data)
                        for account in decoded { _ = try account.code() }
                        self.accounts = decoded
                    }
                    self.loaded = true
                    completion?(true)
                } catch {
                    self.accounts = []
                    self.error = error.localizedDescription
                    completion?(false)
                }
            }
        }
    }
    func add(_ account: OTPAccount) throws {
        guard loaded else { throw OTPError.invalid("키체인을 먼저 열어주세요.") }
        guard !accounts.contains(where: { $0.secret == account.secret && $0.name == account.name && $0.issuer == account.issuer }) else { throw OTPError.invalid("이미 등록한 계정입니다.") }
        try save(accounts + [account])
    }
    func remove(_ account: OTPAccount) {
        do { try save(accounts.filter { $0.id != account.id }) } catch { self.error = error.localizedDescription }
    }
    private func save(_ next: [OTPAccount]) throws {
        guard loaded else { throw OTPError.invalid("키체인을 열 수 없습니다.") }
        let data = try JSONEncoder().encode(next)
        var status = SecItemUpdate(query as CFDictionary, [kSecValueData as String: data] as CFDictionary)
        if status == errSecItemNotFound {
            var request = query
            request[kSecValueData as String] = data
            request[kSecAttrLabel as String] = "BarOTP 인증 계정"
            request[kSecAttrSynchronizable as String] = false
            status = SecItemAdd(request as CFDictionary, nil)
        }
        guard status == errSecSuccess else { throw failure(status) }
        accounts = next
    }
    private func failure(_ status: OSStatus) -> Error { OTPError.invalid("키체인 오류 (\(status)): \(SecCopyErrorMessageString(status, nil) as String? ?? "접근할 수 없습니다.")") }
}

@MainActor final class Clipboard {
    static let shared = Clipboard()
    private var job: DispatchWorkItem?
    private var ownedChange: Int?
    func copy(_ code: String) {
        job?.cancel()
        let board = NSPasteboard.general
        board.clearContents()
        let item = NSPasteboardItem()
        item.setString(code, forType: .string)
        item.setData(Data(), forType: NSPasteboard.PasteboardType("org.nspasteboard.ConcealedType"))
        item.setData(Data(), forType: NSPasteboard.PasteboardType("org.nspasteboard.TransientType"))
        board.writeObjects([item]); ownedChange = board.changeCount
        let task = DispatchWorkItem { [weak self] in self?.clear() }
        job = task; DispatchQueue.main.asyncAfter(deadline: .now() + 30, execute: task)
    }
    func clear() {
        if let ownedChange, NSPasteboard.general.changeCount == ownedChange { NSPasteboard.general.clearContents() }
        ownedChange = nil; job?.cancel(); job = nil
    }
}
