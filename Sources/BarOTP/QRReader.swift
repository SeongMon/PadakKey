import Foundation
import Vision
import ImageIO
#if !STANDALONE
import OTPCore
#endif

// Decoding is independent from the popover/window lifetime.
enum QRReader {
    static func read(url: URL) throws -> String {
        let scoped = url.startAccessingSecurityScopedResource()
        defer { if scoped { url.stopAccessingSecurityScopedResource() } }
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
            throw OTPError.invalid("이미지를 열 수 없습니다. PNG 또는 JPEG 이미지로 다시 저장해 선택하세요.")
        }
        let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any]
        let orientation = CGImagePropertyOrientation(rawValue: (properties?[kCGImagePropertyOrientation] as? UInt32) ?? 1) ?? .up
        let request = VNDetectBarcodesRequest()
        request.symbologies = [.qr]
        try VNImageRequestHandler(cgImage: image, orientation: orientation).perform([request])
        let values = Array(Set((request.results ?? []).compactMap(\.payloadStringValue)))
        guard !values.isEmpty else { throw OTPError.invalid("이미지에서 QR 코드를 찾지 못했습니다. QR 전체가 선명하게 보이는 이미지를 선택하세요.") }
        let otpValues = values.filter { $0.lowercased().hasPrefix("otpauth://") }
        if otpValues.count == 1 { return otpValues[0] }
        if otpValues.count > 1 { throw OTPError.invalid("인증 QR이 여러 개 있습니다. 등록할 QR 하나만 잘라서 선택하세요.") }
        if values.contains(where: { $0.lowercased().hasPrefix("otpauth-migration://") }) {
            throw OTPError.invalid("Google Authenticator 일괄 이전 QR입니다. 서비스의 2단계 인증 설정에서 발급한 QR 또는 수동 키를 사용하세요.")
        }
        throw OTPError.invalid("인증 계정용 QR이 아닙니다. otpauth://totp 형식의 QR을 선택하세요.")
    }
}
