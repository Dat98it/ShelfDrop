using ShelfDrop.Core;

namespace ShelfDrop.Core.Tests;

/// <summary>A scratch folder that is deleted afterwards. Tests never write anywhere else.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShelfDropTests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name, string contents = "x")
    {
        string path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, contents);
        return path;
    }

    public string Folder(string name)
    {
        string path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* a leftover temp folder is not a test failure */ }
    }
}

public sealed class FakeWindow : IShelfWindow
{
    public bool IsVisible { get; private set; }
    public PixelRect Frame { get; private set; }
    public List<PixelRect> Shown { get; } = new();
    public int HideCalls { get; private set; }

    public void ShowWithoutActivating(PixelRect frame)
    {
        IsVisible = true;
        Frame = frame;
        Shown.Add(frame);
    }

    public void Hide()
    {
        IsVisible = false;
        HideCalls++;
    }
}

public sealed class FakeScreens : IScreenProvider
{
    public IReadOnlyList<ScreenInfo> Screens { get; set; } = new[] { Screen(0, 0, 1920, 1080) };
    public PixelPoint CursorPosition { get; set; } = new(960, 540);

    /// <summary>A monitor whose work area is the whole monitor minus a 40 px taskbar at the bottom.</summary>
    public static ScreenInfo Screen(int x, int y, int width, int height, double scale = 1.0, bool primary = true) =>
        new(new PixelRect(x, y, width, height), new PixelRect(x, y, width, height - 40), scale, primary);
}

public sealed class FakeShare : ISharePresenter
{
    public List<IReadOnlyList<ShelfItem>> Presented { get; } = new();
    public Action<bool>? Completed { get; private set; }

    public void Present(IReadOnlyList<ShelfItem> items, Action<bool> completed)
    {
        Presented.Add(items);
        Completed = completed;
    }
}

/// <summary>Runs scheduled actions only when the test says time has passed.</summary>
public sealed class ManualDelays : IDelayedActions
{
    private readonly List<(TimeSpan Delay, Action Action, Handle Handle)> _pending = new();

    public int PendingCount => _pending.Count(p => !p.Handle.Cancelled);

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var handle = new Handle();
        _pending.Add((delay, action, handle));
        return handle;
    }

    public void RunAll()
    {
        var due = _pending.ToList();
        _pending.Clear();
        foreach ((_, Action action, Handle handle) in due)
        {
            if (!handle.Cancelled) action();
        }
    }

    private sealed class Handle : IDisposable
    {
        public bool Cancelled { get; private set; }
        public void Dispose() => Cancelled = true;
    }
}

public sealed class FakeLoginItem : ILoginItemService
{
    public FakeLoginItem(LoginItemStatus status) => Status = status;

    public sealed class Failure : Exception
    {
        public Failure() : base("denied") { }
    }

    public LoginItemStatus Status { get; set; }
    public int RegisterCalls { get; private set; }
    public int UnregisterCalls { get; private set; }
    public int OpenSettingsCalls { get; private set; }
    public Exception? RegisterError { get; set; }
    public Exception? UnregisterError { get; set; }

    /// <summary>What <see cref="Status"/> becomes after a successful Register (Windows can answer "needs approval" at once).</summary>
    public LoginItemStatus StatusAfterRegister { get; set; } = LoginItemStatus.Enabled;

    public void Register()
    {
        RegisterCalls++;
        if (RegisterError is not null) throw RegisterError;
        Status = StatusAfterRegister;
    }

    public void Unregister()
    {
        UnregisterCalls++;
        if (UnregisterError is not null) throw UnregisterError;
        Status = LoginItemStatus.Disabled;
    }

    public void OpenSystemSettings() => OpenSettingsCalls++;
}

public sealed class FakePayload : IDropPayload
{
    public IReadOnlyList<string> FilePaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<VirtualFile> VirtualFiles { get; init; } = Array.Empty<VirtualFile>();
    public byte[]? ImagePng { get; init; }
    public IReadOnlyList<Uri> Links { get; init; } = Array.Empty<Uri>();
    public IReadOnlyList<string> Texts { get; init; } = Array.Empty<string>();

    public int ImageReads { get; private set; }

    public byte[]? ReadImagePng()
    {
        ImageReads++;
        return ImagePng;
    }
}
