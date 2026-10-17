import Foundation
#if canImport(UIKit)
import UIKit
#endif
#if canImport(DeclaredAgeRange)
import DeclaredAgeRange
#endif

/// Bridges Apple's Swift-only, `async` DeclaredAgeRange API to an Objective-C completion-handler surface that
/// .NET-for-iOS can bind. Port of the Godot library's `ios/AgeAssurance.swift`: it returns the versioned wire JSON
/// (`status`, `lower`, `upper_exclusive`, `mandatory`, `requirements`, `reuse_session`), which the managed side parses
/// with `NativeAgeSignal.FromJson`. Nothing runs until `request` is called. Age bounds are only ever placed in that
/// in-memory payload: never logged, stored or sent.
// The explicit @objc(SCAgeAssuranceBridge) name is required, not decorative: Swift would otherwise expose a mangled
// runtime name and the binding's [BaseType] / `_OBJC_CLASS_$_SCAgeAssuranceBridge` reference would fail to link.
@objc(SCAgeAssuranceBridge)
public final class SCAgeAssuranceBridge: NSObject {
    private static let lock = NSLock()
    private static var tasks: [Int: Task<Void, Never>] = [:]

    /// Starts a check. `completion` is invoked at most once, on an arbitrary thread, unless `cancel(withToken:)` ran first
    /// (then it is never invoked). `token` correlates the request with its cancellation.
    @objc(requestWithToken:minimum:allowPrompt:completion:)
    public static func request(withToken token: Int, minimum: Int, allowPrompt: Bool, completion: @escaping (String) -> Void) {
        let task = Task { @MainActor in
            let wire = await evaluate(minimum: minimum, allowPrompt: allowPrompt)
            let data = (try? JSONSerialization.data(withJSONObject: wire)) ?? Data("{}".utf8)
            let json = String(decoding: data, as: UTF8.self)
            lock.lock()
            let live = tasks.removeValue(forKey: token) != nil
            lock.unlock()
            if live && !Task.isCancelled {
                completion(json)
            }
        }
        lock.lock()
        tasks[token] = task
        lock.unlock()
    }

    /// Abandons a request: its completion handler will not be invoked. Safe to call for unknown or finished tokens.
    @objc(cancelWithToken:)
    public static func cancel(withToken token: Int) {
        lock.lock()
        let task = tasks.removeValue(forKey: token)
        lock.unlock()
        task?.cancel()
    }

    @MainActor
    private static func evaluate(minimum: Int, allowPrompt: Bool) async -> [String: Any] {
        var wire: [String: Any] = ["status": "unavailable"]
        #if canImport(DeclaredAgeRange) && canImport(UIKit)
        if #available(iOS 26.0, *) {
            var mandatory = false
            do {
                if #available(iOS 26.4, *) {
                    let features = try await AgeRangeService.shared.requiredRegulatoryFeatures
                    mandatory = features.contains(.declaredAgeRangeRequired)
                    var requirements: [String] = []
                    if features.contains(.significantAppChangeRequiresParentalConsent) { requirements.append("parental_consent") }
                    if features.contains(.significantAppChangeRequiresAdultNotification) { requirements.append("adult_notification") }
                    wire["requirements"] = requirements
                } else if #available(iOS 26.2, *) {
                    mandatory = try await AgeRangeService.shared.isEligibleForAgeFeatures
                }
                wire["mandatory"] = mandatory
                if !allowPrompt {
                    // There is no silent range API on Apple. Report that the last in-process range should be reused
                    // and re-evaluated against the fresh requirements; never show unsolicited sheets.
                    wire["reuse_session"] = true
                } else {
                    let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
                    let root = scenes.filter { $0.activationState == .foregroundActive }
                        .flatMap { $0.windows }.first { $0.isKeyWindow }?.rootViewController
                    if let root {
                        var presenter = root
                        while let presented = presenter.presentedViewController { presenter = presented }
                        let response = try await AgeRangeService.shared.requestAgeRange(ageGates: minimum, in: presenter)
                        switch response {
                        case .sharing(let range):
                            wire["status"] = "shared"
                            if let lower = range.lowerBound { wire["lower"] = lower }
                            // Apple's sandbox table documents inclusive upper bounds (12/15/17).
                            // https://developer.apple.com/documentation/storekit/testing-age-assurance-in-sandbox
                            if let upper = range.upperBound { wire["upper_exclusive"] = upper + 1 }
                        case .declinedSharing: wire["status"] = "declined"
                        @unknown default: wire["status"] = "unavailable"
                        }
                    }
                }
            } catch {
                wire["status"] = "unavailable"
                wire["mandatory"] = mandatory
            }
        }
        #endif
        return wire
    }
}
