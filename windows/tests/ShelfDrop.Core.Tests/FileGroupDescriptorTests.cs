using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace ShelfDrop.Core.Tests;

public class FileGroupDescriptorTests
{
    /// <summary>Builds the bytes the way Windows programs do: a count, then 592-byte records.</summary>
    private static byte[] Build(params (string Name, bool Directory)[] entries)
    {
        var bytes = new byte[4 + entries.Length * 592];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)entries.Length);
        for (int i = 0; i < entries.Length; i++)
        {
            int record = 4 + i * 592;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(record), 0x04 | 0x40);   // flags: attributes and size are valid
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(record + 36), entries[i].Directory ? 0x10u : 0x80u);
            Encoding.Unicode.GetBytes(entries[i].Name).CopyTo(bytes, record + 72);
        }
        return bytes;
    }

    [Fact]
    public void ReadsTheNamesInOrder()
    {
        IReadOnlyList<FileGroupDescriptor.Entry> entries = FileGroupDescriptor.Parse(Build(("invoice.pdf", false), ("photo.jpg", false)));

        Assert.Equal(new[] { "invoice.pdf", "photo.jpg" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.False(e.IsDirectory));
    }

    [Fact]
    public void TellsFoldersFromFiles()
    {
        IReadOnlyList<FileGroupDescriptor.Entry> entries = FileGroupDescriptor.Parse(Build(("Photos", true), ("Photos\\a.jpg", false)));

        Assert.True(entries[0].IsDirectory);
        Assert.False(entries[1].IsDirectory);
        Assert.Equal("Photos\\a.jpg", entries[1].Name);
    }

    [Fact]
    public void ReadsNamesWithAccentsAndOtherScripts()
    {
        Assert.Equal("Báo cáo quý 3 — 報告.docx", FileGroupDescriptor.Parse(Build(("Báo cáo quý 3 — 報告.docx", false)))[0].Name);
    }

    [Fact]
    public void AFullLengthNameWithoutATerminatorIsStillRead()
    {
        string name = new('a', 260);

        Assert.Equal(name, FileGroupDescriptor.Parse(Build((name, false)))[0].Name);
    }

    [Fact]
    public void NothingOrTooLittleGivesNothing()
    {
        Assert.Empty(FileGroupDescriptor.Parse(ReadOnlySpan<byte>.Empty));
        Assert.Empty(FileGroupDescriptor.Parse(new byte[] { 1, 0 }));
        Assert.Empty(FileGroupDescriptor.Parse(new byte[4]));
    }

    [Fact]
    public void ACountLargerThanTheDataIsNotBelieved()
    {
        byte[] bytes = Build(("only.txt", false));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1_000_000);

        IReadOnlyList<FileGroupDescriptor.Entry> entries = FileGroupDescriptor.Parse(bytes);

        Assert.Equal(new[] { "only.txt" }, entries.Select(e => e.Name));
    }

    [Fact]
    public void ACutOffRecordIsDropped()
    {
        byte[] bytes = Build(("a.txt", false), ("b.txt", false));

        IReadOnlyList<FileGroupDescriptor.Entry> entries = FileGroupDescriptor.Parse(bytes.AsSpan(0, bytes.Length - 100));

        Assert.Equal(new[] { "a.txt" }, entries.Select(e => e.Name));
    }
}
