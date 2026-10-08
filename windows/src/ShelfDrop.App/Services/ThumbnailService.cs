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

        private static ImageSource? Load(string path, int pixels)
        {
            ImageSource? fromShell = FromShell(path, pixels);
            return fromShell ?? FromAssociatedIcon(path);
        }

        private static ImageSource? FromShell(string path, int pixels)
        {
            Guid iid = NativeMethods.IID_IShellItemImageFactory;
            NativeMethods.IShellItemImageFactory? factory = null;
            try
            {
                NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                int hr = factory.GetImage(new NativeMethods.SIZE { cx = pixels, cy = pixels }, NativeMethods.SIIGBF_BIGGERSIZEOK, out IntPtr bitmap);
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
