import Foundation
import Security
import OTPCore

@main struct VaultChecks {
    @MainActor static func settled(_ vault: Vault) async {
        for _ in 0..<200 {
            if !vault.isLoading { return }
            try? await Task.sleep(nanoseconds: 10_000_000)
        }
        fatalError("Keychain load did not settle")
    }
    @MainActor static func main() async throws {
        for status in [errSecUserCanceled, errSecAuthFailed, errSecInteractionNotAllowed, errSecInteractionRequired] {
            let vault = Vault(reader: { _ in (status, nil) })
            vault.load(); await settled(vault)
            precondition(vault.needsAuthorization && !vault.loaded && vault.error == nil)
        }
        let account = try OTPAccount(issuer: "Test", name: "test", secret: "JBSWY3DPEHPK3PXP")
        let data = try JSONEncoder().encode([account])
        let vault = Vault(reader: { interactive in interactive ? (errSecSuccess, data) : (errSecInteractionNotAllowed, nil) })
        vault.load(); await settled(vault)
        precondition(vault.needsAuthorization)
        vault.load(allowInteraction: true); await settled(vault)
        precondition(vault.loaded && !vault.needsAuthorization && vault.error == nil && vault.accounts == [account])
        let missing = Vault(reader: { _ in (errSecItemNotFound, nil) })
        missing.load(); await settled(missing)
        precondition(missing.loaded && missing.accounts.isEmpty && !missing.needsAuthorization)
        let corrupt = Vault(reader: { _ in (errSecSuccess, Data("broken".utf8)) })
        corrupt.load(); await settled(corrupt)
        precondition(!corrupt.loaded && corrupt.error != nil)
        let pending = Vault(reader: { _ in Thread.sleep(forTimeInterval: 0.1); return (errSecSuccess, data) })
        pending.load()
        var duplicateRejected = false
        pending.load { success in duplicateRejected = !success }
        precondition(duplicateRejected)
        pending.clear()
        try await Task.sleep(nanoseconds: 300_000_000)
        precondition(!pending.loaded && pending.accounts.isEmpty && !pending.isLoading)
        print("PASS: cancel/deny states, explicit authorization, empty/corrupt vault, duplicate and stale request protection")
    }
}
