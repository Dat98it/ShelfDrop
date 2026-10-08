using System.Buffers.Binary;
using System.Text;

namespace ShelfDrop.Core;

/// <summary>
/// Reads the "FileGroupDescriptorW" a program puts on the drag-and-drop board when it offers files as data
/// (a mail attachment, an image dragged out of a browser): a count followed by that many FILEDESCRIPTORW records.
/// </summary>
public static class FileGroupDescriptor
{
    public readonly record struct Entry(string Name, bool IsDirectory);

    // FILEDESCRIPTORW: flags (4), clsid (16), size (8), point (8), attributes (4), three times (24),
    // file size (8), then the 260-character name.
    private const int RecordSize = 592;
    private const int AttributesOffset = 36;
    private const int NameOffset = 72;
    private const int NameBytes = 260 * 2;
    private const uint DirectoryAttribute = 0x10;

    public static IReadOnlyList<Entry> Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) return Array.Empty<Entry>();

        // Never trust the count: take only as many records as the data really holds.
        uint declared = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        long available = (bytes.Length - 4) / RecordSize;
        int count = (int)Math.Min(declared, available);

        var entries = new List<Entry>(count);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> record = bytes.Slice(4 + i * RecordSize, RecordSize);
            uint attributes = BinaryPrimitives.ReadUInt32LittleEndian(record[AttributesOffset..]);
            string name = Encoding.Unicode.GetString(record.Slice(NameOffset, NameBytes));
            int end = name.IndexOf('\0');
            entries.Add(new Entry(end < 0 ? name : name[..end], (attributes & DirectoryAttribute) != 0));
        }
        return entries;
    }
}
