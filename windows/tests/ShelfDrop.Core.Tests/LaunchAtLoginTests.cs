using Xunit;

namespace ShelfDrop.Core.Tests;

public class LaunchAtLoginTests
{
    [Theory]
    [InlineData(LoginItemStatus.Enabled, MenuCheckState.On)]
    [InlineData(LoginItemStatus.RequiresApproval, MenuCheckState.Mixed)]
    [InlineData(LoginItemStatus.Disabled, MenuCheckState.Off)]
    [InlineData(LoginItemStatus.Unavailable, MenuCheckState.Off)]
    public void TheMenuStateFollowsTheStatus(LoginItemStatus status, MenuCheckState expected)
    {
        Assert.Equal(expected, new LaunchAtLogin(new FakeLoginItem(status)).MenuState);
    }

    [Fact]
    public void TurnsItOnWhenItIsOff()
    {
        var service = new FakeLoginItem(LoginItemStatus.Disabled);

        LaunchAtLoginOutcome outcome = new LaunchAtLogin(service).Toggle();

        Assert.Equal(LaunchAtLoginOutcomeKind.Enabled, outcome.Kind);
        Assert.Equal(1, service.RegisterCalls);
        Assert.Equal(LoginItemStatus.Enabled, service.Status);
    }

    [Fact]
    public void TurnsItOffWhenItIsOn()
    {
        var service = new FakeLoginItem(LoginItemStatus.Enabled);

        LaunchAtLoginOutcome outcome = new LaunchAtLogin(service).Toggle();

        Assert.Equal(LaunchAtLoginOutcomeKind.Disabled, outcome.Kind);
        Assert.Equal(1, service.UnregisterCalls);
        Assert.Equal(LoginItemStatus.Disabled, service.Status);
    }

    [Fact]
    public void WhenItIsSwitchedOffInSettingsTheUserIsSentThereInsteadOfOverridingThem()
    {
        var service = new FakeLoginItem(LoginItemStatus.RequiresApproval);

        LaunchAtLoginOutcome outcome = new LaunchAtLogin(service).Toggle();

        Assert.Equal(LaunchAtLoginOutcomeKind.NeedsApproval, outcome.Kind);
        Assert.Equal(1, service.OpenSettingsCalls);
        Assert.Equal(0, service.RegisterCalls);
        Assert.Equal(0, service.UnregisterCalls);
    }

    [Fact]
    public void RegisteringCanLandInNeedsApproval()
    {
        var service = new FakeLoginItem(LoginItemStatus.Disabled) { StatusAfterRegister = LoginItemStatus.RequiresApproval };

        LaunchAtLoginOutcome outcome = new LaunchAtLogin(service).Toggle();

        Assert.Equal(LaunchAtLoginOutcomeKind.NeedsApproval, outcome.Kind);
        Assert.Equal(1, service.OpenSettingsCalls);
    }

    [Fact]
    public void AFailureToRegisterIsReportedWithItsReason()
    {
        var service = new FakeLoginItem(LoginItemStatus.Disabled) { RegisterError = new FakeLoginItem.Failure() };

        LaunchAtLoginOutcome outcome = new LaunchAtLogin(service).Toggle();

        Assert.Equal(LaunchAtLoginOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("denied", outcome.Error);
    }

    [Fact]
    public void AFailureToUnregisterIsReportedAndLeavesItOn()
    {
        var service = new FakeLoginItem(LoginItemStatus.Enabled) { UnregisterError = new FakeLoginItem.Failure() };

        LaunchAtLoginOutcome outcome = new LaunchAtLogin(service).Toggle();

        Assert.Equal(LaunchAtLoginOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(LoginItemStatus.Enabled, service.Status);
    }

    [Fact]
    public void AnUnavailableServiceIsStillTriedWhenTheUserAsks()
    {
        var service = new FakeLoginItem(LoginItemStatus.Unavailable);

        Assert.Equal(LaunchAtLoginOutcomeKind.Enabled, new LaunchAtLogin(service).Toggle().Kind);
        Assert.Equal(1, service.RegisterCalls);
    }
}

public class LoginItemStatusResolverTests
{
    private const string Command = "\"C:\\Users\\me\\AppData\\Local\\Programs\\ShelfDrop\\ShelfDrop.exe\"";

    [Fact]
    public void NoEntryMeansOff()
    {
        Assert.Equal(LoginItemStatus.Disabled, LoginItemStatusResolver.Resolve(null, Command, null));
        Assert.Equal(LoginItemStatus.Disabled, LoginItemStatusResolver.Resolve("  ", Command, null));
    }

    [Fact]
    public void AnEntryForThisCopyMeansOn()
    {
        Assert.Equal(LoginItemStatus.Enabled, LoginItemStatusResolver.Resolve(Command, Command, null));
    }

    [Fact]
    public void ThePathIsComparedWithoutRegardToCaseOrSurroundingSpaces()
    {
        Assert.Equal(LoginItemStatus.Enabled, LoginItemStatusResolver.Resolve("  " + Command.ToUpperInvariant() + " ", Command, null));
    }

    [Fact]
    public void AnEntryForAnotherCopyCountsAsOffForThisOne()
    {
        string other = "\"D:\\Portable\\ShelfDrop.exe\"";
        Assert.Equal(LoginItemStatus.Disabled, LoginItemStatusResolver.Resolve(other, Command, null));
    }

    [Theory]
    [InlineData(0x02)]
    [InlineData(0x06)]
    public void EvenFirstByteInStartupApprovedMeansTheUserLeftItOn(byte first)
    {
        var approved = new byte[12];
        approved[0] = first;
        Assert.Equal(LoginItemStatus.Enabled, LoginItemStatusResolver.Resolve(Command, Command, approved));
    }

    [Theory]
    [InlineData(0x03)]
    [InlineData(0x07)]
    public void OddFirstByteInStartupApprovedMeansTheUserSwitchedItOff(byte first)
    {
        var approved = new byte[12];
        approved[0] = first;
        Assert.Equal(LoginItemStatus.RequiresApproval, LoginItemStatusResolver.Resolve(Command, Command, approved));
    }

    [Fact]
    public void AnEmptyStartupApprovedValueChangesNothing()
    {
        Assert.Equal(LoginItemStatus.Enabled, LoginItemStatusResolver.Resolve(Command, Command, Array.Empty<byte>()));
    }

    [Fact]
    public void StartupApprovedAloneDoesNotMakeAnEntry()
    {
        Assert.Equal(LoginItemStatus.Disabled, LoginItemStatusResolver.Resolve(null, Command, new byte[] { 2 }));
    }
}
