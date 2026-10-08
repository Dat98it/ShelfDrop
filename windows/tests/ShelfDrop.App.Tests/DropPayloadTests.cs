using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ShelfDrop.App.Services;
using ShelfDrop.Core;
using ShelfDrop.Core.Tests;
using Xunit;

namespace ShelfDrop.App.Tests
{
    [Collection("wpf")]
    public class DropPayloadTests
    {
        private readonly WpfFixture _wpf;

        public DropPayloadTests(WpfFixture wpf) => _wpf = wpf;

        private static MemoryStream Utf16(string text) => new MemoryStream(Encoding.Unicode.GetBytes(text + "\0"));

        [Fact]
        public void ReadsDroppedFiles() => _wpf.Run(() =>
        {
            using (var scratch = new TempDir())
            {
                string a = scratch.File("a.txt");
                string b = scratch.File("b.txt");
                var data = new DataObject();
                data.SetFileDropList(new StringCollection { a, b });

                Assert.Equal(new[] { a, b }, new WpfDropPayload(data).FilePaths);
            }
        });

        [Fact]
        public void ReadsDroppedText() => _wpf.Run(() =>
        {
            var data = new DataObject();
            data.SetText("hello there");

            Assert.Equal(new[] { "hello there" }, new WpfDropPayload(data).Texts);
        });

        [Fact]
        public void ReadsADroppedLinkInEitherFormat() => _wpf.Run(() =>
        {
            var unicode = new DataObject();
            unicode.SetData(WpfDropPayload.UnicodeUrlFormat, Utf16("https://example.com/a?b=1"));
            var ansi = new DataObject();
            ansi.SetData(WpfDropPayload.AnsiUrlFormat, new MemoryStream(Encoding.ASCII.GetBytes("https://example.org/z\0")));

            Assert.Equal(new[] { new Uri("https://example.com/a?b=1") }, new WpfDropPayload(unicode).Links);
            Assert.Equal(new[] { new Uri("https://example.org/z") }, new WpfDropPayload(ansi).Links);
        });

        [Fact]
        public void IgnoresALinkThatIsNotAWebAddress() => _wpf.Run(() =>
        {
            var data = new DataObject();
            data.SetData(WpfDropPayload.UnicodeUrlFormat, Utf16("javascript:alert(1)"));

            Assert.Empty(new WpfDropPayload(data).Links);
        });

        [Fact]
        public void ReadsADroppedPng() => _wpf.Run(() =>
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
            var data = new DataObject();
            data.SetData(WpfDropPayload.PngFormat, new MemoryStream(png));

            Assert.Equal(png, new WpfDropPayload(data).ReadImagePng());
        });

        [Fact]
        public void NothingToReadGivesNothing() => _wpf.Run(() =>
        {
            var payload = new WpfDropPayload(new DataObject());

            Assert.Empty(payload.FilePaths);
            Assert.Empty(payload.VirtualFiles);
            Assert.Empty(payload.Links);
            Assert.Empty(payload.Texts);
            Assert.Null(payload.ReadImagePng());
        });

        [Fact]
        public void ReadsAFileThatTheSenderOnlyOffersAsData() => _wpf.Run(() =>
        {
            byte[] descriptor = Descriptor("invoice.pdf");
            var data = new DataObject();
            data.SetData(WpfDropPayload.FileGroupDescriptorFormat, new MemoryStream(descriptor));
            data.SetData(WpfDropPayload.FileContentsFormat, new MemoryStream(Encoding.ASCII.GetBytes("PDF-DATA")));

            VirtualFile file = Assert.Single(new WpfDropPayload(data).VirtualFiles);

            Assert.Equal("invoice.pdf", file.Name);
            Assert.Equal("PDF-DATA", Encoding.ASCII.GetString(file.ReadContents()!));
        });

        [Fact]
        public void ADropOfVirtualFilesBecomesTempFilesOnTheShelf() => _wpf.Run(() =>
        {
            using (var scratch = new TempDir())
            {
                var temp = new TempStorage(Path.Combine(scratch.Path, "ShelfDrop"));
                var data = new DataObject();
                data.SetData(WpfDropPayload.FileGroupDescriptorFormat, new MemoryStream(Descriptor("photo.jpg")));
                data.SetData(WpfDropPayload.FileContentsFormat, new MemoryStream(new byte[] { 1, 2, 3, 4 }));

                var items = new PayloadImporter(temp).Import(new WpfDropPayload(data));

                ShelfItem item = Assert.Single(items);
                Assert.Equal("photo.jpg", item.Title);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(item.Path!));
            }
        });

        // What leaves the shelf can be read back as what arrives at another shelf.

        [Fact]
        public void FilesLeaveAsAFileDrop() => _wpf.Run(() =>
        {
            DataObject data = Services.DragOutService.BuildData(new DragOutPayload(new[] { @"C:\a.txt", @"C:\b.txt" }, null, null));

            Assert.True(data.GetDataPresent(DataFormats.FileDrop));
            Assert.Equal(new[] { @"C:\a.txt", @"C:\b.txt" }, new WpfDropPayload(data).FilePaths);
            Assert.False(data.GetDataPresent(DataFormats.UnicodeText));
        });

        [Fact]
        public void ALinkLeavesAsALinkAndAsText() => _wpf.Run(() =>
        {
            var url = new Uri("https://example.com/page");
            DataObject data = Services.DragOutService.BuildData(new DragOutPayload(Array.Empty<string>(), url.OriginalString, url));
            var payload = new WpfDropPayload(data);

            Assert.Equal(new[] { url }, payload.Links);
            Assert.Equal(new[] { "https://example.com/page" }, payload.Texts);
            Assert.Empty(payload.FilePaths);
        });

        [Fact]
        public void TextLeavesAsText() => _wpf.Run(() =>
        {
            DataObject data = Services.DragOutService.BuildData(new DragOutPayload(Array.Empty<string>(), "some words", null));

            Assert.Equal(new[] { "some words" }, new WpfDropPayload(data).Texts);
            Assert.Empty(new WpfDropPayload(data).Links);
        });

        [Fact]
        public void AMixedGroupLeavesAsFilesIncludingTheOnesMadeForLinksAndText() => _wpf.Run(() =>
        {
            using (var scratch = new TempDir())
            {
                var temp = new TempStorage(Path.Combine(scratch.Path, "ShelfDrop"));
                string real = scratch.File("real.txt");
                var items = new[] { ShelfItem.ForFile(real), ShelfItem.ForLink(new Uri("https://example.com")), ShelfItem.ForText("a note") };

                DragOutPayload payload = new DragOutPlanner(temp).Plan(items);
                var paths = new WpfDropPayload(Services.DragOutService.BuildData(payload)).FilePaths;

                Assert.Equal(3, paths.Count);
                Assert.Equal(real, paths[0]);
                Assert.All(paths, p => Assert.True(File.Exists(p)));
            }
        });

        private static byte[] Descriptor(string name)
        {
            var bytes = new byte[4 + 592];
            BitConverter.GetBytes(1u).CopyTo(bytes, 0);
            BitConverter.GetBytes(0x80u).CopyTo(bytes, 4 + 36);
            Encoding.Unicode.GetBytes(name).CopyTo(bytes, 4 + 72);
            return bytes;
        }
    }
}
