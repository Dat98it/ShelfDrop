using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using ShelfDrop.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using WinRT;

namespace ShelfDrop.App.Services
{
    /// <summary>
    /// The system's own share sheet (Nearby sharing, Mail, Teams, whatever the user has installed), opened for the shelf's window.
    /// If it cannot be opened, the items are put on the clipboard instead and the user is told so, so that the button
    /// never just does nothing.
    /// </summary>
    internal sealed class ShareSheetPresenter : ISharePresenter
    {
        // The interop interface that lets a desktop program (which has a window, not a "CoreWindow") use the share sheet.
        [ComImport]
        [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDataTransferManagerInterop
        {
            IntPtr GetForWindow(IntPtr appWindow, ref Guid riid);

            void ShowShareUIForWindow(IntPtr appWindow);
        }

        private static readonly Guid DataTransferManagerIid = new Guid("a5caee9b-8708-49d1-8d36-67d25a8da00c");

        private readonly Func<IntPtr> _windowHandle;
        private readonly Action<string, string> _tell;

        /// <param name="windowHandle">The window the share sheet belongs to.</param>
        /// <param name="tell">Shows a message to the user: title, text.</param>
        public ShareSheetPresenter(Func<IntPtr> windowHandle, Action<string, string> tell)
        {
            _windowHandle = windowHandle;
            _tell = tell;
        }

        public void Present(IReadOnlyList<ShelfItem> items, Action<bool> completed)
        {
            try
            {
                IntPtr window = _windowHandle();
                IDataTransferManagerInterop interop = DataTransferManager.As<IDataTransferManagerInterop>();
                Guid iid = DataTransferManagerIid;
                DataTransferManager manager = MarshalInterface<DataTransferManager>.FromAbi(interop.GetForWindow(window, ref iid));

                // The manager belongs to the window, not to this call: leave no handler behind for the next one.
                TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? onRequested = null;
                TypedEventHandler<DataTransferManager, TargetApplicationChosenEventArgs>? onChosen = null;
                onRequested = (sender, args) =>
                {
                    sender.DataRequested -= onRequested;
                    DataRequest request = args.Request;
                    DataRequestDeferral deferral = request.GetDeferral();
                    _ = FillAsync(request, items, deferral);
                };
                onChosen = (sender, args) =>
                {
                    sender.TargetApplicationChosen -= onChosen;
                    completed(true);
                };
                manager.DataRequested += onRequested;
                manager.TargetApplicationChosen += onChosen;

                interop.ShowShareUIForWindow(window);
            }
            catch (Exception e)
            {
                Log.Error("the share sheet could not be opened", e);
                FallBackToClipboard(items);
            }
        }

        private static async Task FillAsync(DataRequest request, IReadOnlyList<ShelfItem> items, DataRequestDeferral deferral)
        {
            try
            {
                DataPackage package = request.Data;
                package.Properties.Title = items.Count == 1 ? items[0].Title : items.Count + " items from ShelfDrop";

                var storage = new List<IStorageItem>();
                var lines = new List<string>();
                var links = new List<Uri>();
                foreach (ShelfItem item in items)
                {
                    switch (item.Kind)
                    {
                        case ShelfItemKind.File:
                            IStorageItem? stored = await TryGetStorageItemAsync(item.Path!);
                            if (stored != null) storage.Add(stored);
                            break;
                        case ShelfItemKind.Link:
                            links.Add(item.Url!);
                            break;
                        case ShelfItemKind.Text:
                            lines.Add(item.Text!);
                            break;
                    }
                }

                if (storage.Count > 0) package.SetStorageItems(storage);
                // A single link is shared as a link; with other things it travels in the text.
                if (links.Count == 1 && lines.Count == 0 && storage.Count == 0) package.SetWebLink(links[0]);
                else lines.AddRange(links.Select(l => l.OriginalString));
                if (lines.Count > 0) package.SetText(string.Join(Environment.NewLine + Environment.NewLine, lines));

                if (storage.Count == 0 && lines.Count == 0 && links.Count == 0)
                    request.FailWithDisplayText("None of the items could be shared.");
            }
            catch (Exception e)
            {
                Log.Error("could not prepare the items for sharing", e);
                request.FailWithDisplayText("The items could not be shared.");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private static async Task<IStorageItem?> TryGetStorageItemAsync(string path)
        {
            try
            {
                return Directory.Exists(path)
                    ? await StorageFolder.GetFolderFromPathAsync(path)
                    : (IStorageItem)await StorageFile.GetFileFromPathAsync(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is COMException || e is ArgumentException)
            {
                Log.Error("a file could not be opened for sharing", e);
                return null;
            }
        }

        private void FallBackToClipboard(IReadOnlyList<ShelfItem> items)
        {
            try
            {
                var paths = new StringCollection();
                foreach (ShelfItem item in items.Where(i => i.Kind == ShelfItemKind.File)) paths.Add(item.Path!);
                var texts = items.Where(i => i.Kind != ShelfItemKind.File)
                    .Select(i => i.Kind == ShelfItemKind.Link ? i.Url!.OriginalString : i.Text!)
                    .ToList();

                var data = new DataObject();
                if (paths.Count > 0) data.SetFileDropList(paths);
                if (texts.Count > 0) data.SetText(string.Join(Environment.NewLine, texts));
                System.Windows.Clipboard.SetDataObject(data, copy: true);

                _tell("Share is not available",
                    "Windows' share sheet could not be opened, so the items were copied to the clipboard instead. "
                    + "Paste them into the app you wanted to share with.");
            }
            catch (Exception e)
            {
                Log.Error("the clipboard fallback failed too", e);
                _tell("Share is not available", "Windows' share sheet could not be opened.");
            }
        }
    }
}
