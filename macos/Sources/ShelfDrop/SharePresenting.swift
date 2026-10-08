import AppKit

/// Shows the system share sheet. A protocol so the controller can be tested without
/// popping up a real menu.
@MainActor
protocol SharePresenting: AnyObject {
    /// Presents the share menu for `items`, anchored to `rect` in `view`.
    /// `onChoose` runs once the user picks a service (`true`) or dismisses the menu (`false`).
    func present(items: [Any], relativeTo rect: NSRect, of view: NSView, onChoose: @escaping (Bool) -> Void)
}

/// The macOS share menu: AirDrop, Mail, Messages, Notes and whatever other share extensions
/// the user has installed.
@MainActor
final class SystemSharePresenter: NSObject, SharePresenting, NSSharingServicePickerDelegate {
    // The picker only weakly references its delegate, so both must be kept alive while it is open.
    private var picker: NSSharingServicePicker?
    private var onChoose: ((Bool) -> Void)?

    func present(items: [Any], relativeTo rect: NSRect, of view: NSView, onChoose: @escaping (Bool) -> Void) {
        let picker = NSSharingServicePicker(items: items)
        picker.delegate = self
        self.picker = picker
        self.onChoose = onChoose
        picker.show(relativeTo: rect, of: view, preferredEdge: .minY)
    }

    nonisolated func sharingServicePicker(_ picker: NSSharingServicePicker, didChoose service: NSSharingService?) {
        MainActor.assumeIsolated {
            let callback = onChoose
            onChoose = nil
            self.picker = nil
            callback?(service != nil)
        }
    }
}
