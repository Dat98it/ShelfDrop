using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using ShelfDrop.App.Native;
using ShelfDrop.Core;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace ShelfDrop.App.Services
{
    /// <summary>
    /// What a drop carries, read from the drag-and-drop board. Nothing is read until the importer asks, because a program
    /// that offers a file as data only produces it when someone asks for it.
    /// </summary>
    internal sealed class WpfDropPayload : IDropPayload
    {
        public const string FileGroupDescriptorFormat = "FileGroupDescriptorW";
        public const string FileContentsFormat = "FileContents";
        public const string UnicodeUrlFormat = "UniformResourceLocatorW";
        public const string AnsiUrlFormat = "UniformResourceLocator";
        public const string PngFormat = "PNG";

        // Safety net, not a feature: a "file" of this size would be an attack or a mistake, not a drag.
        private const long MaxVirtualFileBytes = 512L * 1024 * 1024;

        private static readonly string[] LinkSchemes = { Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeFtp, Uri.UriSchemeMailto };

        private readonly System.Windows.IDataObject _data;

        public WpfDropPayload(System.Windows.IDataObject data)
        {
            _data = data;
        }

        public IReadOnlyList<string> FilePaths
        {
            get
            {
                try
                {
                    return _data.GetDataPresent(DataFormats.FileDrop) && _data.GetData(DataFormats.FileDrop) is string[] paths
                        ? paths
                        : Array.Empty<string>();
                }
                catch (Exception e) when (e is COMException || e is InvalidOperationException)
                {
                    Log.Error("could not read the dropped file list", e);
                    return Array.Empty<string>();
                }
            }
        }

        public IReadOnlyList<VirtualFile> VirtualFiles
        {
            get
            {
                try
                {
                    byte[]? descriptor = ReadBytes(FileGroupDescriptorFormat);
                    if (descriptor == null) return Array.Empty<VirtualFile>();

                    var files = new List<VirtualFile>();
                    IReadOnlyList<FileGroupDescriptor.Entry> entries = FileGroupDescriptor.Parse(descriptor);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].IsDirectory) continue;
                        int index = i;
                        files.Add(new VirtualFile(entries[i].Name, () => ReadFileContents(index)));
                    }
                    return files;
                }
                catch (Exception e) when (e is COMException || e is InvalidOperationException)
                {
                    Log.Error("could not read the dropped file descriptors", e);
                    return Array.Empty<VirtualFile>();
                }
            }
        }

        public byte[]? ReadImagePng()
        {
            try
            {
                byte[]? png = ReadBytes(PngFormat);
                if (png != null) return png;

                if (_data.GetDataPresent(DataFormats.Bitmap) && _data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = new MemoryStream())
                    {
                        encoder.Save(stream);
                        return stream.ToArray();
                    }
                }
            }
            catch (Exception e) when (e is COMException || e is InvalidOperationException || e is NotSupportedException || e is ArgumentException)
            {
                Log.Error("could not read the dropped image", e);
            }
            return null;
        }

        public IReadOnlyList<Uri> Links
        {
            get
            {
                try
                {
                    string? address = ReadText(UnicodeUrlFormat, Encoding.Unicode) ?? ReadText(AnsiUrlFormat, Encoding.Latin1);
                    if (address != null
                        && Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri? uri)
                        && LinkSchemes.Contains(uri.Scheme))
                    {
                        return new[] { uri };
                    }
                }
                catch (Exception e) when (e is COMException || e is InvalidOperationException)
                {
                    Log.Error("could not read the dropped link", e);
                }
                return Array.Empty<Uri>();
            }
        }

        public IReadOnlyList<string> Texts
        {
            get
            {
                try
                {
                    if (_data.GetDataPresent(DataFormats.UnicodeText) && _data.GetData(DataFormats.UnicodeText) is string text && text.Length > 0)
                        return new[] { text };
                }
                catch (Exception e) when (e is COMException || e is InvalidOperationException)
                {
                    Log.Error("could not read the dropped text", e);
                }
                return Array.Empty<string>();
            }
        }

        private byte[]? ReadBytes(string format)
        {
            if (!_data.GetDataPresent(format)) return null;
            if (!(_data.GetData(format) is MemoryStream stream)) return null;
            return stream.ToArray();
        }

        private string? ReadText(string format, Encoding encoding)
        {
            byte[]? bytes = ReadBytes(format);
            if (bytes == null) return null;
            string text = encoding.GetString(bytes);
            int end = text.IndexOf('\0');
            return end < 0 ? text : text.Substring(0, end);
        }

        /// <summary>
        /// The bytes of the <paramref name="index"/>-th offered file. Several files on one board are told apart by an index in the
        /// request, which WPF's own accessors cannot express, so this asks the underlying COM object directly.
        /// </summary>
        private byte[]? ReadFileContents(int index)
        {
            if (!(_data is ComTypes.IDataObject com))
            {
                // Not backed by a COM object: only a single file can be had.
                return index == 0 ? ReadBytes(FileContentsFormat) : null;
            }

            var request = new ComTypes.FORMATETC
            {
                cfFormat = unchecked((short)NativeMethods.RegisterClipboardFormat(FileContentsFormat)),
                dwAspect = ComTypes.DVASPECT.DVASPECT_CONTENT,
                lindex = index,
                ptd = IntPtr.Zero,
                tymed = ComTypes.TYMED.TYMED_ISTREAM | ComTypes.TYMED.TYMED_HGLOBAL,
            };

            ComTypes.STGMEDIUM medium;
            try
            {
                com.GetData(ref request, out medium);
            }
            catch (COMException e)
            {
                Log.Error("the sender would not give file " + index, e);
                return null;
            }

            try
            {
                if (medium.tymed == ComTypes.TYMED.TYMED_ISTREAM)
                    return ReadStream((ComTypes.IStream)Marshal.GetObjectForIUnknown(medium.unionmember));
                if (medium.tymed == ComTypes.TYMED.TYMED_HGLOBAL)
                    return ReadGlobal(medium.unionmember);
                return null;
            }
            finally
            {
                NativeMethods.ReleaseStgMedium(ref medium);
            }
        }

        private static byte[]? ReadStream(ComTypes.IStream stream)
        {
            IntPtr readCount = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                stream.Seek(0, 0, IntPtr.Zero);   // STREAM_SEEK_SET: some senders hand the stream over positioned at its end
                var buffer = new byte[81920];
                using (var result = new MemoryStream())
                {
                    while (true)
                    {
                        stream.Read(buffer, buffer.Length, readCount);
                        int read = Marshal.ReadInt32(readCount);
                        if (read <= 0) break;
                        result.Write(buffer, 0, read);
                        if (result.Length > MaxVirtualFileBytes) return null;
                    }
                    return result.ToArray();
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(readCount);
                Marshal.ReleaseComObject(stream);
            }
        }

        private static byte[]? ReadGlobal(IntPtr handle)
        {
            long size = (long)NativeMethods.GlobalSize(handle);
            if (size <= 0 || size > MaxVirtualFileBytes) return null;

            IntPtr pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                var bytes = new byte[size];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
    }
}
