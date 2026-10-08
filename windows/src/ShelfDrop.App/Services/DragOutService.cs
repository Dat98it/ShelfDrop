using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Windows;
using ShelfDrop.Core;

namespace ShelfDrop.App.Services
{
    /// <summary>Starts a drag that leaves the shelf, carrying files, a link or text to whatever the user drops it on.</summary>
    internal sealed class DragOutService
    {
        /// <summary>True while one of our own drags is under way, so the shelf does not accept its own items back as new ones.</summary>
        public bool IsDragging { get; private set; }

        public void Begin(DependencyObject source, DragOutPayload payload)
        {
            if (payload.IsEmpty) return;

            IsDragging = true;
            try
            {
                // Copy only: with Move, Explorer could relocate the user's original files.
                DragDrop.DoDragDrop(source, BuildData(payload), DragDropEffects.Copy);
            }
            finally
            {
                IsDragging = false;
            }
        }

        public static DataObject BuildData(DragOutPayload payload)
        {
            var data = new DataObject();

            if (payload.FilePaths.Count > 0)
            {
                var paths = new StringCollection();
                foreach (string path in payload.FilePaths) paths.Add(path);
                data.SetFileDropList(paths);
            }

            if (payload.Url != null)
            {
                // A link is offered in both of the formats browsers and Explorer understand: Unicode and plain.
                data.SetData(WpfDropPayload.UnicodeUrlFormat, new MemoryStream(Encoding.Unicode.GetBytes(payload.Url.AbsoluteUri + "\0")));
                data.SetData(WpfDropPayload.AnsiUrlFormat, new MemoryStream(Encoding.ASCII.GetBytes(payload.Url.AbsoluteUri + "\0")));
            }

            if (payload.Text != null) data.SetText(payload.Text);

            return data;
        }
    }
}
