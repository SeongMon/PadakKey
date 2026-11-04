#if STANDALONE
import Foundation
#else
import XCTest
@testable import OTPCore
#endif
final class OTPTests: XCTestCase {
    func testRFC6238AllAlgorithms() throws {
        let times: [Double] = [59, 1111111109, 1111111111, 1234567890, 2000000000, 20000000000]
        let vectors: [(Algorithm, String, [String])] = [
            (.sha1, "12345678901234567890", ["94287082","07081804","14050471","89005924","69279037","65353130"]),
            (.sha256, "12345678901234567890123456789012", ["46119246","68084774","67062674","91819424","90698825","77737706"]),
            (.sha512, "1234567890123456789012345678901234567890123456789012345678901234", ["90693936","25091201","99943326","93441116","38618901","47863826"])
        ]
        for (algorithm, secret, expected) in vectors {
            let account = try OTPAccount(issuer: "RFC", name: "test", secret: encode(secret), algorithm: algorithm, digits: 8)
            for (time, code) in zip(times, expected) { XCTAssertEqual(try account.code(at: Date(timeIntervalSince1970: time)), code) }
        }
    }
    func testURI() throws {
        let account = try OTPAccount.parse("otpauth://totp/GitHub:me%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub")
        XCTAssertEqual(account.name, "me@example.com"); XCTAssertEqual(account.issuer, "GitHub"); XCTAssertEqual(account.period, 30)
        for uri in ["otpauth://hotp/test?secret=MY======", "otpauth://totp/test?secret=MY======&period=0", "otpauth://totp/test?secret=MY======&digits=7", "otpauth://totp/test?secret=MY======&secret=MY======", "otpauth://totp/X:test?secret=MY======&issuer=Y", "otpauth-migration://offline?data=test"] { XCTAssertThrowsError(try OTPAccount.parse(uri)) }
    }
    func testBase32() throws {
        XCTAssertEqual(try Base32.decode("m y======"), Data("f".utf8))
        XCTAssertEqual(try Base32.decode("MZXW6YTB"), Data("fooba".utf8))
        for bad in ["", "A", "M1", "MZ", "MY=", "MY======A", "0O"] { XCTAssertThrowsError(try Base32.decode(bad)) }
    }
    func testPeriodBoundaryAndSixDigits() throws {
        let account = try OTPAccount(issuer: "", name: "test", secret: encode("12345678901234567890"))
        XCTAssertEqual(try account.code(at: Date(timeIntervalSince1970: 59)), "287082")
        XCTAssertEqual(try account.code(at: Date(timeIntervalSince1970: 30)), try account.code(at: Date(timeIntervalSince1970: 59)))
        XCTAssertNotEqual(try account.code(at: Date(timeIntervalSince1970: 59)), try account.code(at: Date(timeIntervalSince1970: 60)))
        XCTAssertThrowsError(try account.code(at: Date(timeIntervalSince1970: -1)))
    }
    private func encode(_ text: String) -> String {
        let alphabet = Array("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567")
        var buffer = 0; var bits = 0; var output = ""
        for byte in text.utf8 { buffer = (buffer << 8) | Int(byte); bits += 8; while bits >= 5 { bits -= 5; output.append(alphabet[(buffer >> bits) & 31]); buffer &= (1 << bits) - 1 } }
        if bits > 0 { output.append(alphabet[(buffer << (5 - bits)) & 31]) }; return output
    }
}

#if STANDALONE
class XCTestCase {}
func XCTAssertEqual<T: Equatable>(_ lhs: T, _ rhs: T) { precondition(lhs == rhs, "Equality failed") }
func XCTAssertNotEqual<T: Equatable>(_ lhs: T, _ rhs: T) { precondition(lhs != rhs, "Inequality failed") }
func XCTAssertThrowsError<T>(_ expression: @autoclosure () throws -> T) { do { _ = try expression(); fatalError("Expected error") } catch {} }
@main struct Verify {
 static func main() throws {
  let suite = OTPTests()
  try suite.testRFC6238AllAlgorithms()
  try suite.testURI()
  try suite.testBase32()
  try suite.testPeriodBoundaryAndSixDigits()
  print("PASS: RFC 6238 (18 vectors), URI validation, Base32, period boundaries, six digits")
 }
}

#endif
