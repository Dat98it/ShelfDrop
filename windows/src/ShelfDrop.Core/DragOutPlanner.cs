using System.Text;

namespace ShelfDrop.Core;

/// <summary>What a drag out of the shelf puts on the system's drag-and-drop board.</summary>
/// <param name="FilePaths">Files to hand over (the CF_HDROP format). Empty for a lone text or link.</param>
/// <param name="Text">Plain text, for a lone text item or a lone link.</param>
/// <param name="Url">A web link, for a lone link.</param>
public sealed record DragOutPayload(IReadOnlyList<string> FilePaths, string? Text, Uri? Url)
{
    public bool IsEmpty => FilePaths.Count == 0 && Text is null && Url is null;
}

/// <summary>
/// Decides what to hand a drop target when items leave the shelf.
///
/// A single link or text goes out as a link or text, so a browser or editor takes it as such. Anything else goes out as files:
/// a file stays the file, and a link or text among others is written out as a small file (.url, .txt) so that
/// "Drag all" really takes every item, whatever mix is on the shelf.
/// </summary>
public sealed class DragOutPlanner
{
    private readonly TempStorage _temp;
    private readonly List<string> _madeFolders = new();

    public DragOutPlanner(TempStorage temp)
    {
        _temp = temp;
    }

    public DragOutPayload Plan(IReadOnlyList<ShelfItem> items)
    {
        List<ShelfItem> usable = items.Where(i => !i.IsMissing).ToList();

        if (usable.Count == 1)
        {
            ShelfItem only = usable[0];
            if (only.Kind == ShelfItemKind.Text) return new DragOutPayload(Array.Empty<string>(), only.Text, null);
            if (only.Kind == ShelfItemKind.Link) return new DragOutPayload(Array.Empty<string>(), only.Url!.OriginalString, only.Url);
        }

        var paths = new List<string>();
        string? folder = null;
        foreach (ShelfItem item in usable)
        {
            switch (item.Kind)
            {
                case ShelfItemKind.File:
                    paths.Add(item.Path!);
                    break;
                case ShelfItemKind.Link:
                    paths.Add(Write(ref folder, FileNames.Sanitize(item.Title, "Link") + ".url",
                        $"[InternetShortcut]\r\nURL={item.Url!.OriginalString}\r\n"));
                    break;
                case ShelfItemKind.Text:
                    paths.Add(Write(ref folder, FileNames.Sanitize(item.Title, "Text") + ".txt", item.Text!));
                    break;
            }
        }
        return new DragOutPayload(paths, null, null);
    }

    /// <summary>Deletes the small files made for earlier drags. Call when the shelf is closed.</summary>
    public void CleanUp()
    {
        foreach (string folder in _madeFolders) _temp.DiscardDirectory(folder);
        _madeFolders.Clear();
    }

    private string Write(ref string? folder, string name, string contents)
    {
        if (folder is null)
        {
            folder = _temp.MakeDirectory();
            _madeFolders.Add(folder);
        }
        string directory = folder;  // a ref parameter cannot be used inside the lambda below
        string unique = FileNames.MakeUnique(name, n => File.Exists(System.IO.Path.Combine(directory, n)));
        string path = System.IO.Path.Combine(directory, unique);
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }
}
