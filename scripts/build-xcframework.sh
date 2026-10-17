#!/bin/bash
# Builds native/AgeAssuranceBridge (a Swift package) for iOS device + simulator and assembles
# them into an XCFramework at native/AgeAssuranceBridge/build/AgeAssuranceBridge.xcframework,
# which src/SyntaxCircus.Maui.AgeAssurance.iOS.Binding/*.csproj references via
# <NativeReference>. Must run on macOS with Xcode command line tools installed - there is no
# cross-platform way to compile Swift/Xcode-native code.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PKG_DIR="$ROOT/native/AgeAssuranceBridge"
BUILD_DIR="$PKG_DIR/build"

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/xcframework-inputs/sim/Headers" "$BUILD_DIR/xcframework-inputs/device/Headers"

cd "$PKG_DIR"

echo "==> Archiving for iOS Simulator"
xcodebuild archive \
    -scheme AgeAssuranceBridge \
    -destination "generic/platform=iOS Simulator" \
    -archivePath "$BUILD_DIR/ios-sim.xcarchive" \
    SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES

echo "==> Archiving for iOS device"
xcodebuild archive \
    -scheme AgeAssuranceBridge \
    -destination "generic/platform=iOS" \
    -archivePath "$BUILD_DIR/ios-device.xcarchive" \
    SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES

echo "==> Linking static libraries"
libtool -static -o "$BUILD_DIR/xcframework-inputs/sim/libAgeAssuranceBridge.a" \
    "$BUILD_DIR/ios-sim.xcarchive/Products/Users/$(whoami)/Objects/AgeAssuranceBridge.o"
libtool -static -o "$BUILD_DIR/xcframework-inputs/device/libAgeAssuranceBridge.a" \
    "$BUILD_DIR/ios-device.xcarchive/Products/Users/$(whoami)/Objects/AgeAssuranceBridge.o"

echo "==> Locating generated Objective-C header"
# The header's text is identical across platform/arch (it reflects the Swift source's @objc
# surface, not per-arch codegen), so one copy covers both slices.
GENERATED_HEADER=$(find "$HOME/Library/Developer/Xcode/DerivedData" -path "*ArchiveIntermediates/AgeAssuranceBridge*" -name "AgeAssuranceBridge-Swift.h" | head -n 1)
if [ -z "$GENERATED_HEADER" ]; then
    echo "Could not locate the generated AgeAssuranceBridge-Swift.h header" >&2
    exit 1
fi
cp "$GENERATED_HEADER" "$BUILD_DIR/xcframework-inputs/sim/Headers/AgeAssuranceBridge.h"
cp "$GENERATED_HEADER" "$BUILD_DIR/xcframework-inputs/device/Headers/AgeAssuranceBridge.h"

cat > "$BUILD_DIR/xcframework-inputs/sim/Headers/module.modulemap" <<'EOF'
module AgeAssuranceBridge {
    header "AgeAssuranceBridge.h"
    export *
}
EOF
cp "$BUILD_DIR/xcframework-inputs/sim/Headers/module.modulemap" "$BUILD_DIR/xcframework-inputs/device/Headers/module.modulemap"

echo "==> Creating XCFramework"
rm -rf "$BUILD_DIR/AgeAssuranceBridge.xcframework"
xcodebuild -create-xcframework \
    -library "$BUILD_DIR/xcframework-inputs/device/libAgeAssuranceBridge.a" -headers "$BUILD_DIR/xcframework-inputs/device/Headers" \
    -library "$BUILD_DIR/xcframework-inputs/sim/libAgeAssuranceBridge.a" -headers "$BUILD_DIR/xcframework-inputs/sim/Headers" \
    -output "$BUILD_DIR/AgeAssuranceBridge.xcframework"

echo "==> Done: $BUILD_DIR/AgeAssuranceBridge.xcframework"
