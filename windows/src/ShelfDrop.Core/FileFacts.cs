namespace ShelfDrop.Core;

/// <summary>Kind and size of a file, for the second line of its tile.</summary>
public static class FileFacts
{
    public readonly record struct Facts(string KindLabel, long? ByteCount, bool IsImage);

    private static readonly Dictionary<string, string> LabelsByExtension = BuildLabels();

    public static Facts Describe(string path)
    {
        bool isDirectory = Directory.Exists(path);
        string extension = isDirectory ? "" : System.IO.Path.GetExtension(path);
        string label = KindLabel(extension, isDirectory);

        long? bytes = null;
        if (!isDirectory)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists) bytes = info.Length;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Unreadable: show the tile without a size rather than failing the drop.
            }
        }
        return new Facts(label, bytes, !isDirectory && label == "Image");
    }

    /// <param name="extension">With or without the leading dot, any case.</param>
    public static string KindLabel(string extension, bool isDirectory)
    {
        if (isDirectory) return "Folder";
        string key = extension.TrimStart('.').ToLowerInvariant();
        if (LabelsByExtension.TryGetValue(key, out string? label)) return label;
        return key.Length == 0 ? "File" : key.ToUpperInvariant();
    }

    private static Dictionary<string, string> BuildLabels()
    {
        var map = new Dictionary<string, string>();
        void Add(string label, string extensions)
        {
            foreach (string extension in extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                map[extension] = label;
        }

        Add("Image", "png jpg jpeg jpe jfif gif bmp tif tiff webp heic heif avif ico svg raw cr2 nef dng psd");
        Add("PDF", "pdf");
        Add("Video", "mp4 m4v mov mkv avi wmv webm flv mpg mpeg 3gp");
        Add("Audio", "mp3 wav flac aac m4a ogg oga opus wma aiff");
        Add("Archive", "zip rar 7z tar gz tgz bz2 xz zst cab iso");
        Add("Sheet", "xls xlsx xlsm csv ods tsv");
        Add("Slides", "ppt pptx pps ppsx odp");
        Add("Code", "c h cpp hpp cc cs csx java kt swift js jsx mjs ts tsx py rb go rs php sh ps1 bat cmd sql html htm css scss json xml yaml yml toml ini");
        Add("Document", "doc docx odt");
        Add("Text", "txt md markdown rtf log");
        Add("App", "exe msix appx");
        Add("Installer", "msi");
        return map;
    }
}
