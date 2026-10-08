import SwiftUI

/// Shared measurements for the shelf's look, so the pieces stay consistent.
enum ShelfStyle {
    static let panelRadius: CGFloat = 20
    static let cardRadius: CGFloat = 12
    static let padding: CGFloat = 14
    static let controlHeight: CGFloat = 24
}

/// One look for every control in the header: a soft rounded square that lightens on hover and
/// darkens while pressed. Icon buttons set their own width; text buttons pad their label.
struct ShelfButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        StyledLabel(configuration: configuration)
    }

    // Not named `Body`: that would collide with ButtonStyle's associated type.
    private struct StyledLabel: View {
        let configuration: ButtonStyleConfiguration
        @Environment(\.isEnabled) private var isEnabled
        @StateObject private var hover = HoverState()

        var body: some View {
            configuration.label
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(hover.isHovering && isEnabled ? Color.primary : Color.secondary)
                .frame(height: ShelfStyle.controlHeight)
                .background(
                    RoundedRectangle(cornerRadius: 7, style: .continuous)
                        .fill(Color.primary.opacity(fillOpacity))
                )
                .background(HoverReader { hover.isHovering = $0 })
                .opacity(isEnabled ? 1 : 0.4)
                .contentShape(Rectangle())
        }

        private var fillOpacity: Double {
            guard isEnabled else { return 0.05 }
            if configuration.isPressed { return 0.2 }
            return hover.isHovering ? 0.13 : 0.07
        }
    }
}
