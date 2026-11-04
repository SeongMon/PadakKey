// swift-tools-version: 5.9
import PackageDescription
let package = Package(name: "BarOTP", platforms: [.macOS(.v13)], products: [.executable(name: "BarOTP", targets: ["BarOTP"])], targets: [.target(name: "OTPCore"), .executableTarget(name: "BarOTP", dependencies: ["OTPCore"]), .testTarget(name: "OTPCoreTests", dependencies: ["OTPCore"])])
