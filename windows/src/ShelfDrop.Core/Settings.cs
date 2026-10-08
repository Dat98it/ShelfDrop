using System.Diagnostics;
using System.Text.Json;

namespace ShelfDrop.Core;

/// <summary>Where the app remembers numbers between runs. A small interface so tests never touch a real settings file.</summary>
public interface IKeyValueStore
{
    double? GetDouble(string key);

    /// <summary>Writes all values together, so a pair such as width and height lands in one write.</summary>
    void SetDoubles(params (string Key, double Value)[] values);
}

public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, double> _values = new();

    public int WriteCount { get; private set; }

    public double? GetDouble(string key) => _values.TryGetValue(key, out double v) ? v : null;

    public void SetDoubles(params (string Key, double Value)[] values)
    {
        WriteCount++;
        foreach ((string key, double value) in values) _values[key] = value;
    }
}

/// <summary>
/// The settings file, <c>%AppData%\ShelfDrop\settings.json</c>. It is only a convenience, so a missing,
/// unreadable or half-written file is treated as "nothing saved yet" rather than as an error.
/// </summary>
public sealed class JsonFileKeyValueStore : IKeyValueStore
{
    private readonly string _path;
    private Dictionary<string, double>? _cache;
    private bool _retired;

    public JsonFileKeyValueStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShelfDrop", "settings.json");

    public double? GetDouble(string key) => Load().TryGetValue(key, out double v) && double.IsFinite(v) ? v : null;

    public void SetDoubles(params (string Key, double Value)[] values)
    {
        if (_retired) return;

        Dictionary<string, double> data = Load();
        foreach ((string key, double value) in values)
        {
            if (double.IsFinite(value)) data[key] = value;
        }

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            // Write beside the file and swap, so a crash mid-write cannot leave half a file behind.
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Debug.WriteLine($"[ShelfDrop] could not save settings: {e.Message}");
        }
    }

    /// <summary>
    /// Deletes the settings file and its folder (when nothing else is in it), and makes every later write a no-op,
    /// so an app that is uninstalling cannot write its settings back as it quits.
    /// </summary>
    public void Erase()
    {
        _retired = true;
        _cache = new Dictionary<string, double>();
        try
        {
            File.Delete(_path);
            File.Delete(_path + ".tmp");
            string? folder = System.IO.Path.GetDirectoryName(_path);
            if (folder is not null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Debug.WriteLine($"[ShelfDrop] could not erase settings: {e.Message}");
        }
    }

    private Dictionary<string, double> Load()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(_path))
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(_path));
                if (parsed is not null) return _cache = parsed;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Debug.WriteLine($"[ShelfDrop] ignoring unreadable settings: {e.Message}");
        }
        return _cache = new Dictionary<string, double>();
    }
}

public static class ShelfLimits
{
    /// <summary>
    /// Transparent room around the panel, on each side, where its shadow falls. The window is that much bigger than
    /// the panel the user sees, and the sizes below are the window's.
    /// </summary>
    public const double ShadowMargin = 12;

    /// <summary>Size the shelf opens with (a 360 x 300 panel); the user can resize it from there.</summary>
    public static readonly SizeDips DefaultSize = new(360 + 2 * ShadowMargin, 300 + 2 * ShadowMargin);

    /// <summary>The smallest the shelf can be made (a 340 x 190 panel), so the header always fits.</summary>
    public static readonly SizeDips MinimumSize = new(340 + 2 * ShadowMargin, 190 + 2 * ShadowMargin);
}

/// <summary>Remembers the size the user gave the shelf, across launches.</summary>
public sealed class ShelfSizeStore
{
    /// <summary>Guards against a corrupted setting producing an absurd window.</summary>
    public const double MaximumStoredLength = 10_000;

    private const string WidthKey = "shelfWidth";
    private const string HeightKey = "shelfHeight";
    private readonly IKeyValueStore _store;

    public ShelfSizeStore(IKeyValueStore store)
    {
        _store = store;
    }

    /// <summary>The stored size, or null if the user has never resized the shelf (or the value is unusable).</summary>
    public SizeDips? Load()
    {
        if (_store.GetDouble(WidthKey) is not { } width || _store.GetDouble(HeightKey) is not { } height) return null;
        if (!double.IsFinite(width) || !double.IsFinite(height)) return null;

        SizeDips minimum = ShelfLimits.MinimumSize;
        return new SizeDips(
            Math.Min(Math.Max(width, minimum.Width), MaximumStoredLength),
            Math.Min(Math.Max(height, minimum.Height), MaximumStoredLength));
    }

    public void Save(SizeDips size) => _store.SetDoubles((WidthKey, size.Width), (HeightKey, size.Height));
}

/// <summary>
/// Remembers where the user last put the shelf, across launches. The anchor is the top-left corner in
/// physical pixels on the virtual desktop, because that is the corner that stays put when the shelf is resized.
/// </summary>
public sealed class ShelfPositionStore
{
    private const string XKey = "shelfTopLeftX";
    private const string YKey = "shelfTopLeftY";
    private readonly IKeyValueStore _store;

    public ShelfPositionStore(IKeyValueStore store)
    {
        _store = store;
    }

    /// <summary>The stored top-left corner, or null if the user never moved the shelf (or the value is unusable).</summary>
    public PixelPoint? Load()
    {
        if (_store.GetDouble(XKey) is not { } x || _store.GetDouble(YKey) is not { } y) return null;
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) >= 1_000_000 || Math.Abs(y) >= 1_000_000) return null;
        return new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
    }

    public void Save(PixelPoint topLeft) => _store.SetDoubles((XKey, topLeft.X), (YKey, topLeft.Y));
}
