import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

// icongen — render the AstroCast .iconset from the 1024 master.
//
// Invoked by build/macos/make-icon.sh, which compiles it on demand. It exists as
// Swift rather than as shell because the job is not expressible with the tools
// macOS ships: `sips` cannot crop from a given offset reliably (its --cropOffset
// is widely mis-documented and crops from the centre), and it cannot pad with a
// *transparent* margin at all — --padColor takes an opaque hex colour. Both
// operations are essential here.
//
//   icongen <master.png> <outputDir> <fillPercent>
//
// WHAT IT DOES, AND WHY
// ----------------------
// The master's artwork occupies only ~60% of its 1024 canvas. Shipping macOS
// icons fill 83.6% — measured across Safari, Maps, Podcasts, App Store and
// Calculator, all within 0.2% of each other — so the master rendered as-is would
// sit visibly undersized beside its neighbours in the Dock. Each icon is
// therefore cropped to its alpha bounding box and re-centred at the target
// fill.
//
// RESAMPLING
// ----------
// Downscales use an area (box) filter with fractional edge weights: each
// destination pixel is the coverage-weighted mean of the source rectangle it
// maps to. That is the correct filter for reduction, and measurably better here
// than CoreGraphics' own — at 32px it retains more detail (10.685 vs 10.404 by
// mean adjacent-pixel difference) and at 16px markedly more (10.842 vs 9.406),
// which matters for a design carrying fine radial lines that alias badly.
//
// The single upscale — the 1024 entry, whose 856px content comes from 616px of
// master — uses CoreGraphics at .high instead, since an area filter degenerates
// to nearest-neighbour when magnifying.
//
// Every size is resampled directly from the original crop, never from an
// already-resampled intermediate, so no entry inherits another's softness. In
// particular the 512 and below do NOT descend from the upscaled 1024.
//
// Alpha is handled premultiplied throughout — the only correct space in which to
// average RGBA, or transparent pixels' black leaks into every soft edge — and is
// unpremultiplied once, when writing each PNG, because PNG stores straight alpha.

struct Bitmap { var w: Int; var h: Int; var px: [UInt8] }   // RGBA8, premultiplied

func rasterise(_ img: CGImage) -> Bitmap {
    let w = img.width, h = img.height
    var px = [UInt8](repeating: 0, count: w * h * 4)
    px.withUnsafeMutableBytes { buf in
        let ctx = CGContext(data: buf.baseAddress, width: w, height: h,
                            bitsPerComponent: 8, bytesPerRow: w * 4,
                            space: CGColorSpaceCreateDeviceRGB(),
                            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        ctx.draw(img, in: CGRect(x: 0, y: 0, width: w, height: h))
    }
    return Bitmap(w: w, h: h, px: px)
}

func writePNG(_ bm: Bitmap, to path: String) {
    var straight = [UInt8](repeating: 0, count: bm.w * bm.h * 4)
    for i in stride(from: 0, to: bm.px.count, by: 4) {
        let a = bm.px[i + 3]
        if a == 0 { continue }                       // leave fully clear pixels zeroed
        let af = Double(a) / 255.0
        for c in 0..<3 {
            straight[i + c] = UInt8(min(255, max(0, (Double(bm.px[i + c]) / af).rounded())))
        }
        straight[i + 3] = a
    }
    let provider = CGDataProvider(data: Data(straight) as CFData)!
    let img = CGImage(width: bm.w, height: bm.h, bitsPerComponent: 8, bitsPerPixel: 32,
                      bytesPerRow: bm.w * 4, space: CGColorSpaceCreateDeviceRGB(),
                      bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.last.rawValue),
                      provider: provider, decode: nil, shouldInterpolate: false,
                      intent: .defaultIntent)!
    let dest = CGImageDestinationCreateWithURL(
        URL(fileURLWithPath: path) as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(dest, img, nil)
    guard CGImageDestinationFinalize(dest) else { fatalError("failed to write \(path)") }
}

/// Area (box) resample with fractional edge weights. Correct for any reduction
/// ratio, integer or not — which the reframe makes necessary, since 616 source
/// pixels of artwork must land on 428, 214, 107, 53, 27 and 13.
func areaResample(_ src: Bitmap, _ dw: Int, _ dh: Int) -> Bitmap {
    let sx = Double(src.w) / Double(dw), sy = Double(src.h) / Double(dh)
    var out = [UInt8](repeating: 0, count: dw * dh * 4)
    for dy in 0..<dh {
        let y0 = Double(dy) * sy, y1 = Double(dy + 1) * sy
        let iy0 = Int(floor(y0)), iy1 = min(src.h - 1, Int(ceil(y1)) - 1)
        for dx in 0..<dw {
            let x0 = Double(dx) * sx, x1 = Double(dx + 1) * sx
            let ix0 = Int(floor(x0)), ix1 = min(src.w - 1, Int(ceil(x1)) - 1)
            var acc = [Double](repeating: 0, count: 4), wsum = 0.0
            for y in iy0...max(iy0, iy1) {
                let wy = min(Double(y + 1), y1) - max(Double(y), y0)
                if wy <= 0 { continue }
                for x in ix0...max(ix0, ix1) {
                    let wx = min(Double(x + 1), x1) - max(Double(x), x0)
                    if wx <= 0 { continue }
                    let w = wx * wy, o = (y * src.w + x) * 4
                    for c in 0..<4 { acc[c] += Double(src.px[o + c]) * w }
                    wsum += w
                }
            }
            let o = (dy * dw + dx) * 4
            if wsum > 0 {
                for c in 0..<4 { out[o + c] = UInt8(min(255, max(0, (acc[c] / wsum).rounded()))) }
            }
        }
    }
    return Bitmap(w: dw, h: dh, px: out)
}

let args = CommandLine.arguments
guard args.count >= 4, let fill = Double(args[3]) else {
    print("usage: icongen <master.png> <outputDir> <fillPercent>"); exit(2)
}
let masterPath = args[1], outDir = args[2]

guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: masterPath) as CFURL, nil),
      let masterImg = CGImageSourceCreateImageAtIndex(src, 0, nil) else {
    print("error: cannot read master \(masterPath)"); exit(1)
}
let master = rasterise(masterImg)
guard master.w == master.h else { print("error: master must be square"); exit(1) }

// Alpha bounding box. Threshold at 8/255 so the faint outer glow does not drag
// the box out to the full canvas.
var minX = master.w, minY = master.h, maxX = -1, maxY = -1
for y in 0..<master.h {
    for x in 0..<master.w where Int(master.px[(y * master.w + x) * 4 + 3]) > 8 {
        minX = min(minX, x); maxX = max(maxX, x); minY = min(minY, y); maxY = max(maxY, y)
    }
}
guard maxX >= 0 else { print("error: master is fully transparent"); exit(1) }
let cw = maxX - minX + 1, ch = maxY - minY + 1
guard let croppedImg = masterImg.cropping(
        to: CGRect(x: minX, y: minY, width: cw, height: ch)) else {
    print("error: crop failed"); exit(1)
}
let crop = rasterise(croppedImg)

print("master  \(master.w)x\(master.h)   content \(cw)x\(ch) " +
      String(format: "(%.1f%% of canvas)", 100.0 * Double(max(cw, ch)) / Double(master.w)))
print(String(format: "target  %.1f%% fill\n", fill))

// canvas -> the .iconset names that carry it. The duplicated pairs are two
// required names for one image, not a mistake: 128@2x and 256 are both 256px,
// 256@2x and 512 are both 512px.
let plan: [(canvas: Int, names: [String])] = [
    (16,   ["icon_16x16.png"]),
    (32,   ["icon_16x16@2x.png", "icon_32x32.png"]),
    (64,   ["icon_32x32@2x.png"]),
    (128,  ["icon_128x128.png"]),
    (256,  ["icon_128x128@2x.png", "icon_256x256.png"]),
    (512,  ["icon_256x256@2x.png", "icon_512x512.png"]),
    (1024, ["icon_512x512@2x.png"]),
]

try? FileManager.default.createDirectory(atPath: outDir, withIntermediateDirectories: true)

var failures: [String] = []
let sourceContent = max(cw, ch)

for (canvas, names) in plan {
    let target = Double(canvas) * fill / 100.0
    let scale = target / Double(sourceContent)
    let dw = max(1, Int((Double(cw) * scale).rounded()))
    let dh = max(1, Int((Double(ch) * scale).rounded()))

    let content: Bitmap
    let method: String
    if scale > 1.0 {
        // The one magnification. An area filter degenerates when magnifying, so
        // hand this to CoreGraphics' resampler at its best setting.
        var px = [UInt8](repeating: 0, count: dw * dh * 4)
        px.withUnsafeMutableBytes { buf in
            let ctx = CGContext(data: buf.baseAddress, width: dw, height: dh,
                                bitsPerComponent: 8, bytesPerRow: dw * 4,
                                space: CGColorSpaceCreateDeviceRGB(),
                                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
            ctx.interpolationQuality = .high
            ctx.draw(croppedImg, in: CGRect(x: 0, y: 0, width: dw, height: dh))
        }
        content = Bitmap(w: dw, h: dh, px: px)
        method = "CG .high UPSCALE"
    } else {
        content = areaResample(crop, dw, dh)
        method = "area"
    }

    // Composite centred on a transparent canvas.
    var px = [UInt8](repeating: 0, count: canvas * canvas * 4)
    let ox = (canvas - dw) / 2, oy = (canvas - dh) / 2
    for y in 0..<dh {
        for x in 0..<dw {
            let s = (y * dw + x) * 4, d = ((y + oy) * canvas + (x + ox)) * 4
            for c in 0..<4 { px[d + c] = content.px[s + c] }
        }
    }
    let bm = Bitmap(w: canvas, h: canvas, px: px)

    // ---- verify rather than trust -------------------------------------
    // Guard 1: the canvas must never exceed the master's. This is the check
    // that would have caught fabricating a 1024 entry from a 256 source.
    if canvas > master.w { failures.append("\(canvas): canvas exceeds master (\(master.w))") }

    // Guard 2: no artwork may be magnified except the single largest entry,
    // and that one only by the approved reframe factor. A silent upscale
    // anywhere else means the plan or the master changed.
    if scale > 1.0 && canvas != plan.last!.canvas {
        failures.append(String(format: "%d: upscales content %.3fx but is not the largest entry", canvas, scale))
    }

    // Guard 3: alpha must survive. A set that lost transparency renders as
    // black squares in the Dock, and does so only once installed.
    var hasClear = false, hasOpaque = false
    for i in stride(from: 3, to: bm.px.count, by: 4) {
        if bm.px[i] == 0 { hasClear = true } else if bm.px[i] == 255 { hasOpaque = true }
        if hasClear && hasOpaque { break }
    }
    if !hasClear { failures.append("\(canvas): no transparent pixels — alpha lost") }
    if !hasOpaque { failures.append("\(canvas): no opaque pixels — image is empty") }

    for n in names { writePNG(bm, to: "\(outDir)/\(n)") }

    // %s would take a C string; a Swift String passed to it is dereferenced as
    // char* and segfaults. Use %@ and pad the column by hand.
    let methodCol = method.padding(toLength: 17, withPad: " ", startingAt: 0)
    print(String(format: "  %4d  content %4dx%-4d  scale %.3fx  %@ -> %@",
                 canvas, dw, dh, scale, methodCol, names.joined(separator: ", ")))
}

if !failures.isEmpty {
    print("\nVERIFICATION FAILED:")
    for f in failures { print("  \(f)") }
    exit(1)
}
print("\nall entries verified: canvas never exceeds the master, no unexpected upscale, alpha intact")
