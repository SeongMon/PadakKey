import Foundation
import CoreImage
import ImageIO
import UniformTypeIdentifiers

@main struct QRImportChecks {
    static func main() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("barotp-qr-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let uri = "otpauth://totp/Example:demo?secret=JBSWY3DPEHPK3PXP&issuer=Example"
        let context = CIContext(options: [.useSoftwareRenderer: true])
        func write(_ payload: String, _ type: UTType) throws -> URL {
            let filter = CIFilter(name: "CIQRCodeGenerator")!
            filter.setValue(Data(payload.utf8), forKey: "inputMessage")
            let qr = filter.outputImage!.transformed(by: CGAffineTransform(scaleX: 8, y: 8))
            let bounds = qr.extent.insetBy(dx: -32, dy: -32)
            let padded = qr.composited(over: CIImage(color: CIColor.white).cropped(to: bounds))
            let image = context.createCGImage(padded, from: bounds)!
            let url = directory.appendingPathComponent(UUID().uuidString + "." + type.preferredFilenameExtension!)
            let dest = CGImageDestinationCreateWithURL(url as CFURL, type.identifier as CFString, 1, nil)!
            CGImageDestinationAddImage(dest, image, nil)
            precondition(CGImageDestinationFinalize(dest))
            return url
        }
        for type in [UTType.png, .jpeg, .tiff] {
            let decoded = try QRReader.read(url: write(uri, type))
            precondition(decoded == uri)
            let account = try OTPAccount.parse(decoded)
            precondition(account.name == "demo")
        }
        for payload in ["https://example.com", "otpauth-migration://offline?data=example"] {
            let url = try write(payload, .png)
            do { _ = try QRReader.read(url: url); fatalError("Unsupported payload accepted") } catch is OTPError { }
        }
        let invalid = directory.appendingPathComponent("broken.png")
        try Data("not an image".utf8).write(to: invalid)
        do { _ = try QRReader.read(url: invalid); fatalError("Broken image accepted") } catch is OTPError { }
        print("PASS: PNG, JPEG, TIFF QR import; non-authenticator QR, migration QR, invalid image errors")
    }
}
