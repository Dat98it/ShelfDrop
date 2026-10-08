namespace ShelfDrop.Core;

/// <summary>
/// Everything that really changes the machine, as separate steps, so tests can swap them out
/// and never remove a real app, setting or file.
/// </summary>
public sealed class UninstallEffects
{
    /// <summary>Starts the uninstaller that the setup program put next to the app, and returns without waiting for it. Throws if it cannot start.</summary>
    public required Action<string> LaunchUninstaller { get; init; }

    public required Action RemoveTemporaryFiles { get; init; }
    public required Action RemoveSettings { get; init; }
}

public sealed record UninstallAvailability(bool IsAvailable, string? Reason = null);

public sealed class UninstallOutcome
{
    private UninstallOutcome(bool succeeded, IReadOnlyList<string> warnings, string? error)
    {
        Succeeded = succeeded;
        Warnings = warnings;
        Error = error;
    }

    public bool Succeeded { get; }

    /// <summary>Steps that did not work but did not stop the rest.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Why nothing was removed (or what was done has been put back).</summary>
    public string? Error { get; }

    public static UninstallOutcome Removed(IReadOnlyList<string>? warnings = null) =>
        new(true, warnings ?? Array.Empty<string>(), null);

    public static UninstallOutcome Failed(string error) => new(false, Array.Empty<string>(), error);
}

public sealed record UninstallConfirmation(string Title, string Message, string ConfirmButton, string CancelButton);

/// <summary>
/// The "Uninstall ShelfDrop…" menu item. The app cannot delete itself while it runs, so it hands the job to the uninstaller
/// the setup program installed, and removes what it keeps outside the install folder (settings, temporary files, the startup entry).
/// </summary>
public sealed class Uninstaller
{
    public const string ExpectedExecutableName = "ShelfDrop.exe";
    public const string UninstallerFileName = "unins000.exe";

    private readonly string _executablePath;
    private readonly ILoginItemService _loginItem;
    private readonly UninstallEffects _effects;
    private readonly Func<string, bool> _fileExists;

    public Uninstaller(string executablePath, ILoginItemService loginItem, UninstallEffects effects, Func<string, bool>? fileExists = null)
    {
        _executablePath = executablePath;
        _loginItem = loginItem;
        _effects = effects;
        _fileExists = fileExists ?? File.Exists;
    }

    // Split by hand on both kinds of slash rather than with System.IO.Path, so a Windows path means the same thing
    // wherever the tests run.
    private int LastSeparator => _executablePath.LastIndexOfAny(new[] { '\\', '/' });

    private string ExecutableName => _executablePath[(LastSeparator + 1)..];

    public string InstallFolder => LastSeparator < 0 ? "" : _executablePath[..LastSeparator];

    public string UninstallerPath =>
        LastSeparator < 0 ? UninstallerFileName : _executablePath[..(LastSeparator + 1)] + UninstallerFileName;

    /// <summary>Whether uninstalling from here is safe and possible. Checked before anything is touched.</summary>
    public UninstallAvailability Availability
    {
        get
        {
            // The guard that keeps this from ever running some other program's uninstaller.
            if (!string.Equals(ExecutableName, ExpectedExecutableName, StringComparison.OrdinalIgnoreCase))
                return new UninstallAvailability(false, "This does not look like ShelfDrop, so it will not be removed.");

            if (!_fileExists(UninstallerPath))
                return new UninstallAvailability(false,
                    "This copy of ShelfDrop was not put here by its setup program (it looks like a portable or development build), "
                    + "so there is no uninstaller to run. Delete its folder to remove it.");

            return new UninstallAvailability(true);
        }
    }

    /// <summary>
    /// What the confirmation dialog says. Written by hand, so a test checks that it mentions everything
    /// <see cref="Uninstall"/> does: what the user agrees to must be what happens.
    /// </summary>
    public UninstallConfirmation Confirmation() => new(
        Title: "Uninstall ShelfDrop?",
        Message:
            "ShelfDrop will quit and:\n\n"
            + $"• remove itself ({InstallFolder}) with its uninstaller\n"
            + "• turn off Launch at login\n"
            + "• delete its settings (shelf size and position) and temporary files\n\n"
            + "Your own files are not touched: the shelf only holds references to them.",
        ConfirmButton: "Uninstall",
        CancelButton: "Cancel");

    public UninstallOutcome Uninstall()
    {
        UninstallAvailability availability = Availability;
        if (!availability.IsAvailable) return UninstallOutcome.Failed(availability.Reason ?? "Uninstall is not available.");

        var warnings = new List<string>();

        // 1. Stop it launching at login, before anything else changes.
        bool loginItemWasOn = _loginItem.Status is LoginItemStatus.Enabled or LoginItemStatus.RequiresApproval;
        bool loginItemTurnedOff = false;
        if (loginItemWasOn)
        {
            try
            {
                _loginItem.Unregister();
                loginItemTurnedOff = true;
            }
            catch (Exception e)
            {
                warnings.Add($"Couldn't turn off Launch at login ({e.Message}). Switch it off in Settings → Apps → Startup.");
            }
        }

        // 2. The program itself. The one step that can reasonably fail; if it does, undo step 1 and stop,
        //    leaving the settings alone so the app that stays behind keeps working as before.
        try
        {
            _effects.LaunchUninstaller(UninstallerPath);
        }
        catch (Exception e)
        {
            if (loginItemTurnedOff)
            {
                try { _loginItem.Register(); } catch (Exception) { /* nothing more to do: the message below tells the user what failed */ }
            }
            return UninstallOutcome.Failed($"Couldn't start the uninstaller: {e.Message}");
        }

        // 3. What it left behind.
        _effects.RemoveTemporaryFiles();
        _effects.RemoveSettings();
        return UninstallOutcome.Removed(warnings);
    }
}
