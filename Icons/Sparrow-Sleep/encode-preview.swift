import Foundation
import ImageIO
import UniformTypeIdentifiers

// Package the generated frames without redrawing or retouching them.
let directory = URL(fileURLWithPath: CommandLine.arguments[1])
let frames = ["rest.png", "inhale.png"]
let delays = [1.2, 1.0]
let output = directory.appendingPathComponent("sparrow-sleep.gif")
guard let destination = CGImageDestinationCreateWithURL(output as CFURL, UTType.gif.identifier as CFString, frames.count, nil) else { fatalError("Cannot create GIF") }
CGImageDestinationSetProperties(destination, [kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0]] as CFDictionary)
for (index, name) in frames.enumerated() {
    guard let source = CGImageSourceCreateWithURL(directory.appendingPathComponent(name) as CFURL, nil),
          let frame = CGImageSourceCreateImageAtIndex(source, 0, nil) else { fatalError("Missing frame: \(name)") }
    print("\(name): \(frame.width)x\(frame.height)")
    CGImageDestinationAddImage(destination, frame, [kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFDelayTime: delays[index]]] as CFDictionary)
}
guard CGImageDestinationFinalize(destination) else { fatalError("GIF encoding failed") }
print(output.path)
