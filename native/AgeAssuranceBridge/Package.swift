// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "AgeAssuranceBridge",
    // Deliberately the ecosystem's actual iOS floor, not DeclaredAgeRange's real iOS 26.0 requirement: the
    // `#available(iOS 26.x, *)` runtime checks in AgeAssuranceBridge.swift gate those APIs, so the compiled
    // library does not raise any consumer's deployment target.
    platforms: [.iOS(.v15)],
    products: [
        .library(name: "AgeAssuranceBridge", targets: ["AgeAssuranceBridge"]),
    ],
    targets: [
        .target(
            name: "AgeAssuranceBridge",
            swiftSettings: [
                // `import DeclaredAgeRange` would otherwise embed a strong autolink (-framework DeclaredAgeRange) in the
                // object file and crash the app at launch on iOS < 26. The framework is linked weakly by the binding
                // project's NativeReference (WeakFrameworks) instead.
                .unsafeFlags(["-Xfrontend", "-disable-autolink-framework", "-Xfrontend", "DeclaredAgeRange"]),
            ]
        ),
    ]
)
