using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShelfDrop.App.Native;

namespace ShelfDrop.App.Services
{
    /// <summary>
    /// The picture on a tile: a real thumbnail for photos, videos and documents, the file's own icon for the rest.
    /// Both come from the same shell service Explorer uses, so tiles look like what the user sees in a folder.
    /// </summary>
    internal static class ThumbnailService
    {
        /// <summary>Loads on a worker thread. Returns null when Windows has nothing for this path (a file that is gone).</summary>
        public static Task<ImageSource?> LoadAsync(string path, int pixels)
        {
            var completion = new TaskCompletionSource<ImageSource?>();

            // Thumbnail handlers are COM objects that expect a single-threaded apartment, which the thread pool is not.
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(Load(path, pixels));
                }
                catch (Exception e)
                {
                    Log.Error("thumbnail failed", e);
                    completion.SetResult(null);
                }
            })
            {
                IsBackground = true,
                Name = "ShelfDrop thumbnail",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        // What WPF can open by itself. HEIC and the like need a codec that not every machine has, so those are left to the shell.
        private static readonly string[] PictureExtensions = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".gif", ".bmp", ".tif", ".tiff", ".ico" };

        private static ImageSource? Load(string path, int pixels)
        {
            // In order of preference: the shell's real thumbnail (it knows about photo orientation and video frames),
            // the picture itself where the shell has no thumbnail to give (a stripped-down Windows, a thumbnail cache turned off),
            // and last the file's icon.
            return FromShell(path, pixels, NativeMethods.SIIGBF_THUMBNAILONLY)
                ?? FromPictureFile(path, pixels)
                ?? FromShell(path, pixels, NativeMethods.SIIGBF_BIGGERSIZEOK)
                ?? FromAssociatedIcon(path);
        }

        /// <summary>Opens a picture file directly, decoded down to the size wanted so a huge photo does not fill the memory.</summary>
        internal static ImageSource? FromPictureFile(string path, int pixels)
        {
            if (Array.IndexOf(PictureExtensions, Path.GetExtension(path).ToLowerInvariant()) < 0) return null;
            try
            {
                int naturalWidth = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;

                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path);
                image.CacheOption = BitmapCacheOption.OnLoad;   // read it now, so the file is not left open
                if (naturalWidth > pixels) image.DecodePixelWidth = pixels;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // A file that is not really a picture, or is damaged or unreadable: fall through to its icon.
                return null;
            }
        }

        private static ImageSource? FromShell(string path, int pixels, uint flags)
        {
            Guid iid = NativeMethods.IID_IShellItemImageFactory;
            NativeMethods.IShellItemImageFactory? factory = null;
            try
            {
                NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                int hr = factory.GetImage(new NativeMethods.SIZE { cx = pixels, cy = pixels }, flags, out IntPtr bitmap);
                if (hr != 0 || bitmap == IntPtr.Zero) return null;
                try
                {
                    BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();   // so the UI thread may use what this thread made
                    return source;
                }
                finally
                {
                    NativeMethods.DeleteObject(bitmap);
                }
            }
            catch (Exception e) when (e is COMException || e is FileNotFoundException || e is ArgumentException)
            {
                return null;
            }
            finally
            {
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        private static ImageSource? FromAssociatedIcon(string path)
        {
            try
            {
                using (Icon? icon = File.Exists(path) ? Icon.ExtractAssociatedIcon(path) : null)
                {
                    if (icon == null) return null;
                    BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
