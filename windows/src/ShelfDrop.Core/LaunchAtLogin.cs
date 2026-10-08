namespace ShelfDrop.Core;

public enum LoginItemStatus
{
    Enabled,

    /// <summary>Registered, but switched off in Settings → Apps → Startup (or Task Manager's Startup tab). Only the user can switch it back on.</summary>
    RequiresApproval,

    Disabled,

    /// <summary>The system cannot be asked (for example the registry is not reachable).</summary>
    Unavailable,
}

/// <summary>What the login-item logic needs from Windows. An interface so tests never touch the real startup entries of whoever runs them.</summary>
public interface ILoginItemService
{
    LoginItemStatus Status { get; }
    void Register();
    void Unregister();
    void OpenSystemSettings();
}

public enum MenuCheckState
{
    Off,
    On,
    Mixed,
}

public sealed record LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind Kind, string? Error = null);

public enum LaunchAtLoginOutcomeKind
{
    Enabled,
    Disabled,

    /// <summary>The user has to switch it on in Settings; that page was opened for them.</summary>
    NeedsApproval,

    Failed,
}

/// <summary>The logic behind the "Launch at login" menu item.</summary>
public sealed class LaunchAtLogin
{
    private readonly ILoginItemService _service;

    public LaunchAtLogin(ILoginItemService service)
    {
        _service = service;
    }

    /// <summary>Check mark for the menu item: on when the app will launch at login, a dash while it waits for the user's approval, off otherwise.</summary>
    public MenuCheckState MenuState => _service.Status switch
    {
        LoginItemStatus.Enabled => MenuCheckState.On,
        LoginItemStatus.RequiresApproval => MenuCheckState.Mixed,
        _ => MenuCheckState.Off,
    };

    public LaunchAtLoginOutcome Toggle()
    {
        switch (_service.Status)
        {
            case LoginItemStatus.Enabled:
                try
                {
                    _service.Unregister();
                    return new LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind.Disabled);
                }
                catch (Exception e)
                {
                    return new LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind.Failed, e.Message);
                }

            case LoginItemStatus.RequiresApproval:
                // Already registered; only the user can approve it, so take them there.
                _service.OpenSystemSettings();
                return new LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind.NeedsApproval);

            default:
                try
                {
                    _service.Register();
                }
                catch (Exception e)
                {
                    return new LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind.Failed, e.Message);
                }
                if (_service.Status == LoginItemStatus.RequiresApproval)
                {
                    _service.OpenSystemSettings();
                    return new LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind.NeedsApproval);
                }
                return new LaunchAtLoginOutcome(LaunchAtLoginOutcomeKind.Enabled);
        }
    }
}

/// <summary>Reads the two places Windows keeps a startup entry and says whether it will really run.</summary>
public static class LoginItemStatusResolver
{
    /// <param name="runCommand">The value under <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>, or null if there is none.</param>
    /// <param name="expectedCommand">What this copy of the app would register.</param>
    /// <param name="startupApproved">
    /// The value under <c>...\Explorer\StartupApproved\Run</c>, or null. Settings and Task Manager write here when the user flips the switch:
    /// an odd first byte means "disabled by the user".
    /// </param>
    public static LoginItemStatus Resolve(string? runCommand, string expectedCommand, byte[]? startupApproved)
    {
        if (string.IsNullOrWhiteSpace(runCommand)) return LoginItemStatus.Disabled;

        // Another copy of the app is the one registered. This copy is not, so it counts as off,
        // and turning it on points the entry here.
        if (!string.Equals(runCommand.Trim(), expectedCommand.Trim(), StringComparison.OrdinalIgnoreCase))
            return LoginItemStatus.Disabled;

        return startupApproved is { Length: > 0 } && (startupApproved[0] & 1) == 1
            ? LoginItemStatus.RequiresApproval
            : LoginItemStatus.Enabled;
    }
}
