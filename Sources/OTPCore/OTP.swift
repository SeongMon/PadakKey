import Foundation
import CryptoKit

public enum OTPError: LocalizedError {
    case invalid(String)
    public var errorDescription: String? { if case .invalid(let message) = self { return message }; return nil }
}
public enum Algorithm: String, Codable, CaseIterable { case sha1 = "SHA1", sha256 = "SHA256", sha512 = "SHA512" }
public struct OTPAccount: Identifiable, Codable, Equatable {
    public var id: UUID
    public var issuer: String
    public var name: String
    public var secret: String
    public var algorithm: Algorithm
    public var digits: Int
    public var period: Int
    public init(issuer: String, name: String, secret: String, algorithm: Algorithm = .sha1, digits: Int = 6, period: Int = 30) throws {
        guard !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw OTPError.invalid("계정 이름을 입력하세요.") }
        _ = try Base32.decode(secret)
        guard [6, 8].contains(digits), (1...300).contains(period) else { throw OTPError.invalid("지원하지 않는 자릿수 또는 갱신 주기입니다.") }
        self.id = UUID(); self.issuer = issuer; self.name = name; self.secret = Base32.normalize(secret); self.algorithm = algorithm; self.digits = digits; self.period = period
    }
    public static func parse(_ text: String) throws -> OTPAccount {
        guard let url = URLComponents(string: text.trimmingCharacters(in: .whitespacesAndNewlines)), url.scheme?.lowercased() == "otpauth", url.host?.lowercased() == "totp" else { throw OTPError.invalid("otpauth://totp 형식만 지원합니다. HOTP 및 Google 일괄 이전 QR은 지원하지 않습니다.") }
        var fields = [String: String]()
        for item in url.queryItems ?? [] {
            guard fields[item.name] == nil, let value = item.value else { throw OTPError.invalid("중복되거나 비어 있는 등록 항목입니다.") }
            fields[item.name] = value
        }
        guard let secret = fields["secret"] else { throw OTPError.invalid("인증 키가 없습니다.") }
        let label = String(url.path.dropFirst())
        let parts = label.split(separator: ":", maxSplits: 1).map(String.init)
        let prefix = parts.count == 2 ? parts[0] : ""
        let issuer = fields["issuer"] ?? prefix
        if !prefix.isEmpty, !issuer.isEmpty, prefix != issuer { throw OTPError.invalid("서비스 이름이 일치하지 않습니다.") }
        guard let algorithm = Algorithm(rawValue: (fields["algorithm"] ?? "SHA1").uppercased()), let digits = Int(fields["digits"] ?? "6"), let period = Int(fields["period"] ?? "30") else { throw OTPError.invalid("등록 옵션을 확인하세요.") }
        return try OTPAccount(issuer: issuer, name: parts.last ?? "", secret: secret, algorithm: algorithm, digits: digits, period: period)
    }
    public func code(at date: Date = Date()) throws -> String {
        guard [6,8].contains(digits), (1...300).contains(period), date.timeIntervalSince1970 >= 0, date.timeIntervalSince1970 < Double(Int64.max) else { throw OTPError.invalid("잘못된 OTP 설정 또는 시간입니다.") }
        let key = SymmetricKey(data: try Base32.decode(secret))
        var counter = UInt64(date.timeIntervalSince1970 / Double(period)).bigEndian
        let message = withUnsafeBytes(of: &counter) { Data($0) }
        let hash: [UInt8]
        switch algorithm {
        case .sha1: hash = Array(HMAC<Insecure.SHA1>.authenticationCode(for: message, using: key))
        case .sha256: hash = Array(HMAC<SHA256>.authenticationCode(for: message, using: key))
        case .sha512: hash = Array(HMAC<SHA512>.authenticationCode(for: message, using: key))
        }
        let offset = Int(hash.last! & 15)
        let number = hash[offset..<offset+4].reduce(UInt32(0)) { ($0 << 8) | UInt32($1) } & 0x7fffffff
        return String(format: "%0*u", digits, number % (digits == 6 ? 1_000_000 : 100_000_000))
    }
}
public enum Base32 {
    public static func normalize(_ value: String) -> String { value.uppercased().filter { !$0.isWhitespace && $0 != "-" } }
    public static func decode(_ value: String) throws -> Data {
        let text = normalize(value)
        let body = String(text.prefix { $0 != "=" })
        let suffix = text.dropFirst(body.count)
        let allowedPadding = [0:0, 2:6, 4:4, 5:3, 7:1]
        guard !body.isEmpty, let pad = allowedPadding[body.count % 8], suffix.allSatisfy({ $0 == "=" }), suffix.isEmpty || suffix.count == pad else { throw OTPError.invalid("Base32 인증 키 형식을 확인하세요.") }
        let alphabet = Array("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567")
        var accumulator: UInt32 = 0; var bits = 0; var data = Data()
        for character in body {
            guard let value = alphabet.firstIndex(of: character) else { throw OTPError.invalid("인증 키에는 A–Z와 2–7만 사용할 수 있습니다.") }
            accumulator = (accumulator << 5) | UInt32(value); bits += 5
            if bits >= 8 { bits -= 8; data.append(UInt8((accumulator >> bits) & 255)); accumulator &= (1 << bits) - 1 }
        }
        guard accumulator == 0, !data.isEmpty else { throw OTPError.invalid("인증 키의 마지막 문자가 올바르지 않습니다.") }
        return data
    }
}
