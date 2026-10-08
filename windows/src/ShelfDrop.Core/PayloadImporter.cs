using System.Diagnostics;
using System.Globalization;

namespace ShelfDrop.Core;

/// <summary>A file a program offered as data rather than as a path (an attachment in a mail, an image dragged out of a browser).</summary>
public sealed record VirtualFile(string Name, Func<byte[]?> ReadContents);

/// <summary>Everything a drop carried, read lazily: only what the importer asks for is fetched from the sending program.</summary>
public interface IDropPayload
{
    /// <summary>Real files and folders (the CF_HDROP format).</summary>
    IReadOnlyList<string> FilePaths { get; }

    /// <summary>Files offered as data (FileGroupDescriptor and FileContents).</summary>
    IReadOnlyList<VirtualFile> VirtualFiles { get; }

    /// <summary>A picture as PNG bytes, if the drop is a picture and not a file.</summary>
    byte[]? ReadImagePng();

    /// <summary>Web links.</summary>
    IReadOnlyList<Uri> Links { get; }

    /// <summary>Plain text.</summary>
    IReadOnlyList<string> Texts { get; }
}

/// <summary>
/// Turns whatever a drop carried into shelf items. The first kind that yields something wins, in order of fidelity:
/// real files, virtual files, raw images, web links, text.
/// </summary>
public sealed class PayloadImporter
{
    private readonly TempStorage _temp;
    private readonly Func<DateTime> _now;

    public PayloadImporter(TempStorage temp, Func<DateTime>? now = null)
    {
        _temp = temp;
        _now = now ?? (() => DateTime.Now);
    }

    public IReadOnlyList<ShelfItem> Import(IDropPayload payload)
    {
        List<string> paths = payload.FilePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count > 0) return paths.Select(ShelfItem.ForFile).ToList();

        IReadOnlyList<ShelfItem> virtualFiles = WriteVirtualFiles(payload.VirtualFiles);
        if (virtualFiles.Count > 0) return virtualFiles;

        if (payload.ReadImagePng() is { Length: > 0 } png && WriteImage(png) is { } imagePath)
            return new[] { ShelfItem.ForFile(imagePath) };

        if (payload.Links.Count > 0) return payload.Links.Select(ShelfItem.ForLink).ToList();

        return payload.Texts.Where(t => !string.IsNullOrEmpty(t)).Select(ShelfItem.ForText).ToList();
    }

    private IReadOnlyList<ShelfItem> WriteVirtualFiles(IReadOnlyList<VirtualFile> files)
    {
        if (files.Count == 0) return Array.Empty<ShelfItem>();

        string directory = _temp.MakeDirectory();
        var items = new List<ShelfItem>();
        foreach (VirtualFile file in files)
        {
            try
            {
                byte[]? contents = file.ReadContents();
                if (contents is null) continue;

                string name = FileNames.MakeUnique(FileNames.Sanitize(file.Name), n => File.Exists(System.IO.Path.Combine(directory, n)));
                string path = System.IO.Path.Combine(directory, name);
                File.WriteAllBytes(path, contents);
                items.Add(ShelfItem.ForFile(path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                // One bad attachment does not stop the others.
                Debug.WriteLine($"[ShelfDrop] virtual file '{file.Name}' failed: {e.Message}");
            }
        }

        if (items.Count == 0) _temp.DiscardDirectory(directory);
        return items;
    }

    private string? WriteImage(byte[] png)
    {
        string name = $"Image {_now().ToString("yyyy-MM-dd 'at' HH.mm.ss", CultureInfo.InvariantCulture)}.png";
        string directory = _temp.MakeDirectory();
        string path = System.IO.Path.Combine(directory, name);
        try
        {
            File.WriteAllBytes(path, png);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[ShelfDrop] could not write image: {e.Message}");
            _temp.DiscardDirectory(directory);
            return null;
        }
    }
}
