using System.Diagnostics;

namespace ShelfDrop.Core;

/// <summary>
/// The folder where the app keeps the files it had to create itself (a dragged-in image, a file a browser
/// only offered as data). Everything else on a shelf is a reference to a file the user owns, and is never touched.
/// </summary>
public sealed class TempStorage
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public TempStorage(string? root = null)
    {
        Root = System.IO.Path.GetFullPath(root ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShelfDrop"));
    }

    public string Root { get; }

    /// <summary>Makes a fresh folder for one drop.</summary>
    public string MakeDirectory()
    {
        string directory = System.IO.Path.Combine(Root, Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>The shelf is not kept between runs, so leftovers from a previous run are garbage.</summary>
    public void CleanUp()
    {
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[ShelfDrop] could not clean temp folder: {e.Message}");
        }
    }

    /// <summary>
    /// Deletes the temp copies behind <paramref name="items"/>. Only files inside <see cref="Root"/> are ever removed:
    /// everything else on a shelf is a reference to a file the user owns.
    /// </summary>
    public void Discard(IEnumerable<ShelfItem> items)
    {
        foreach (ShelfItem item in items)
        {
            if (item.Kind == ShelfItemKind.File && item.Path is { } path) DiscardFile(path);
        }
    }

    /// <summary>Deletes a whole folder made by <see cref="MakeDirectory"/>. Refuses anything outside <see cref="Root"/>.</summary>
    public void DiscardDirectory(string directory)
    {
        string full = System.IO.Path.GetFullPath(directory);
        if (!IsInsideRoot(full) || HasLinkOnTheWay(full)) return;
        try
        {
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[ShelfDrop] could not delete {full}: {e.Message}");
        }
    }

    private void DiscardFile(string path)
    {
        string full = System.IO.Path.GetFullPath(path);
        if (!IsInsideRoot(full) || HasLinkOnTheWay(full)) return;

        try
        {
            if (File.Exists(full)) File.Delete(full);
            else if (Directory.Exists(full)) Directory.Delete(full, recursive: true);

            // Each drop gets its own folder; remove it once its last file is gone.
            string? folder = System.IO.Path.GetDirectoryName(full);
            if (folder is not null && IsInsideRoot(folder) && Directory.Exists(folder)
                && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[ShelfDrop] could not delete {full}: {e.Message}");
        }
    }

    /// <summary>Strictly inside: <see cref="Root"/> itself does not count, and a sibling such as "ShelfDropOther" does not match by prefix.</summary>
    private bool IsInsideRoot(string fullPath)
    {
        string prefix = Root.EndsWith(System.IO.Path.DirectorySeparatorChar) ? Root : Root + System.IO.Path.DirectorySeparatorChar;
        return fullPath.Length > prefix.Length && fullPath.StartsWith(prefix, PathComparison);
    }

    /// <summary>
    /// True if the path, or any folder between it and the root, is a link. A link inside the temp folder
    /// could lead anywhere, and deleting through it would delete the user's own files.
    /// </summary>
    private bool HasLinkOnTheWay(string fullPath)
    {
        string? current = fullPath;
        while (current is not null && current.Length > Root.Length)
        {
            try
            {
                var info = new FileInfo(current);
                if (info.Exists || Directory.Exists(current))
                {
                    if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0) return true;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return true;
            }
            current = System.IO.Path.GetDirectoryName(current);
        }
        return false;
    }
}
