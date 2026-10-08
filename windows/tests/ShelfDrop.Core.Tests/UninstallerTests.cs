using Xunit;

namespace ShelfDrop.Core.Tests;

public class UninstallerTests
{
    private const string Folder = @"C:\Users\me\AppData\Local\Programs\ShelfDrop";
    private const string Exe = Folder + @"\ShelfDrop.exe";
    private const string UninstallerExe = Folder + @"\unins000.exe";

    /// <summary>Records what the uninstaller does instead of doing it.</summary>
    private sealed class EffectLog
    {
        public List<string> Steps { get; } = new();
        public Exception? LaunchError { get; set; }

        /// <summary>How many times the startup entry had been turned off at the moment the uninstaller was started.</summary>
        public int? LoginItemTurnedOffBeforeLaunch { get; private set; }

        public FakeLoginItem? Login { get; set; }

        public UninstallEffects Effects => new()
        {
            LaunchUninstaller = path =>
            {
                Steps.Add("launch:" + path);
                LoginItemTurnedOffBeforeLaunch = Login?.UnregisterCalls;
                if (LaunchError is not null) throw LaunchError;
            },
            RemoveTemporaryFiles = () => Steps.Add("temp"),
            RemoveSettings = () => Steps.Add("settings"),
        };
    }

    private static Uninstaller Make(EffectLog log, FakeLoginItem login, string exe = Exe, bool uninstallerPresent = true)
    {
        log.Login = login;
        return new Uninstaller(exe, login, log.Effects, fileExists: _ => uninstallerPresent);
    }

    // When it is allowed

    [Fact]
    public void IsAvailableForAnInstalledCopy()
    {
        var uninstaller = Make(new EffectLog(), new FakeLoginItem(LoginItemStatus.Disabled));
        Assert.True(uninstaller.Availability.IsAvailable);
    }

    [Fact]
    public void RefusesACopyThatHasNoUninstallerBesideIt()
    {
        // A portable copy, or a build folder: there is nothing that could remove it cleanly.
        var uninstaller = Make(new EffectLog(), new FakeLoginItem(LoginItemStatus.Disabled), uninstallerPresent: false);

        UninstallAvailability availability = uninstaller.Availability;

        Assert.False(availability.IsAvailable);
        Assert.Contains("portable or development build", availability.Reason);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData(@"C:\Tools\dotnet.exe")]
    [InlineData(@"C:\Users\me\Downloads\ShelfDrop-Setup.exe")]
    public void RefusesAnyOtherProgram(string exe)
    {
        var uninstaller = Make(new EffectLog(), new FakeLoginItem(LoginItemStatus.Disabled), exe);
        Assert.False(uninstaller.Availability.IsAvailable);
    }

    [Fact]
    public void AnUnavailableUninstallDoesNothingAtAll()
    {
        var log = new EffectLog();
        var login = new FakeLoginItem(LoginItemStatus.Enabled);

        UninstallOutcome outcome = Make(log, login, @"C:\Other\App.exe").Uninstall();

        Assert.False(outcome.Succeeded);
        Assert.Empty(log.Steps);
        Assert.Equal(0, login.UnregisterCalls);
    }

    [Fact]
    public void LooksForTheUninstallerNextToTheProgram()
    {
        string? asked = null;
        var uninstaller = new Uninstaller(Exe, new FakeLoginItem(LoginItemStatus.Disabled), new EffectLog().Effects,
            fileExists: path => { asked = path; return true; });

        _ = uninstaller.Availability;

        Assert.Equal(UninstallerExe, asked);
    }

    // Doing it

    [Fact]
    public void StartsTheUninstallerThenRemovesWhatItKeepsOutsideItsFolder()
    {
        var log = new EffectLog();

        UninstallOutcome outcome = Make(log, new FakeLoginItem(LoginItemStatus.Enabled)).Uninstall();

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Warnings);
        Assert.Equal(new[] { "launch:" + UninstallerExe, "temp", "settings" }, log.Steps);
    }

    [Fact]
    public void TurnsOffLaunchAtLoginBeforeTheUninstallerStarts()
    {
        var log = new EffectLog();
        var login = new FakeLoginItem(LoginItemStatus.Enabled);

        Make(log, login).Uninstall();

        Assert.Equal(1, login.UnregisterCalls);
        Assert.Equal(1, log.LoginItemTurnedOffBeforeLaunch);
    }

    [Fact]
    public void AlsoTurnsOffAnEntryThatTheUserHadSwitchedOff()
    {
        var login = new FakeLoginItem(LoginItemStatus.RequiresApproval);

        Make(new EffectLog(), login).Uninstall();

        Assert.Equal(1, login.UnregisterCalls);
    }

    [Fact]
    public void LeavesStartupEntriesAloneWhenItWasNeverOn()
    {
        var login = new FakeLoginItem(LoginItemStatus.Disabled);

        UninstallOutcome outcome = Make(new EffectLog(), login).Uninstall();

        Assert.True(outcome.Succeeded);
        Assert.Equal(0, login.UnregisterCalls);
        Assert.Equal(0, login.RegisterCalls);
    }

    // When something goes wrong

    [Fact]
    public void IfTheUninstallerCannotStartNothingElseIsRemovedAndLaunchAtLoginIsRestored()
    {
        var log = new EffectLog { LaunchError = new InvalidOperationException("blocked by policy") };
        var login = new FakeLoginItem(LoginItemStatus.Enabled);

        UninstallOutcome outcome = Make(log, login).Uninstall();

        Assert.False(outcome.Succeeded);
        Assert.Contains("uninstaller", outcome.Error);
        Assert.Contains("blocked by policy", outcome.Error);
        // The app stays, so its settings and temp files must stay too.
        Assert.Single(log.Steps);
        // And it must keep launching at login, as before.
        Assert.Equal(1, login.RegisterCalls);
        Assert.Equal(LoginItemStatus.Enabled, login.Status);
    }

    [Fact]
    public void IfTheUninstallerCannotStartAndLaunchAtLoginWasOffItStaysOff()
    {
        var log = new EffectLog { LaunchError = new InvalidOperationException("nope") };
        var login = new FakeLoginItem(LoginItemStatus.Disabled);

        Make(log, login).Uninstall();

        Assert.Equal(0, login.RegisterCalls);
        Assert.Equal(LoginItemStatus.Disabled, login.Status);
    }

    [Fact]
    public void AStartupEntryThatWillNotTurnOffIsAWarningNotAStopper()
    {
        var log = new EffectLog();
        var login = new FakeLoginItem(LoginItemStatus.Enabled) { UnregisterError = new FakeLoginItem.Failure() };

        UninstallOutcome outcome = Make(log, login).Uninstall();

        Assert.True(outcome.Succeeded);
        string warning = Assert.Single(outcome.Warnings);
        Assert.Contains("Launch at login", warning);
        Assert.Contains("Startup", warning);
        Assert.Equal(3, log.Steps.Count);
    }

    [Fact]
    public void IfTheStartupEntryNeverTurnedOffItIsNotRestoredEither()
    {
        var log = new EffectLog { LaunchError = new InvalidOperationException("nope") };
        var login = new FakeLoginItem(LoginItemStatus.Enabled) { UnregisterError = new FakeLoginItem.Failure() };

        Make(log, login).Uninstall();

        Assert.Equal(0, login.RegisterCalls);
    }

    // What the user is told

    [Fact]
    public void TheConfirmationSaysEverythingThatWillHappen()
    {
        string message = Make(new EffectLog(), new FakeLoginItem(LoginItemStatus.Disabled)).Confirmation().Message;

        Assert.Contains("uninstaller", message);
        Assert.Contains("Launch at login", message);
        Assert.Contains("settings", message);
        Assert.Contains("temporary files", message);
        Assert.Contains(Folder, message);
        Assert.Contains("Your own files are not touched", message);
    }

    [Fact]
    public void TheConfirmationOffersCancelAndUninstall()
    {
        UninstallConfirmation confirmation = Make(new EffectLog(), new FakeLoginItem(LoginItemStatus.Disabled)).Confirmation();

        Assert.Equal("Cancel", confirmation.CancelButton);
        Assert.Equal("Uninstall", confirmation.ConfirmButton);
        Assert.Equal("Uninstall ShelfDrop?", confirmation.Title);
    }
}
