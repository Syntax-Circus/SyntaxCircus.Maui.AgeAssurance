#nullable enable
using System;
using Foundation;
using ObjCRuntime;

namespace SyntaxCircus.Maui.AgeAssurance.IOS.Binding;

// Binds native/AgeAssuranceBridge's Objective-C-visible surface (see scripts/build-xcframework.sh). Internal:
// AppleAgeAssuranceProvider is the package's actual surface - this raw native binding is an implementation detail.
[Internal]
[BaseType(typeof(NSObject), Name = "SCAgeAssuranceBridge")]
interface SCAgeAssuranceBridge
{
    // Completion receives the versioned wire JSON (NativeAgeSignal.FromJson). It is not invoked after Cancel.
    [Static]
    [Export("requestWithToken:minimum:allowPrompt:completion:")]
    void Request(nint token, nint minimum, bool allowPrompt, Action<NSString> completion);

    [Static]
    [Export("cancelWithToken:")]
    void Cancel(nint token);
}
