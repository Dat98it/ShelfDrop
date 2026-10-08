using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShelfDrop.Core;

namespace ShelfDrop.App
{
    /// <summary>
    /// A shelf with a few things on it, for <c>--demo</c>: how the app's pictures are taken without a person dragging files around.
    /// The files are made in the temp folder, so closing the shelf deletes them like any other temporary copy.
    /// </summary>
    internal static class DemoContent
    {
        public static IReadOnlyList<ShelfItem> Create(TempStorage temp)
        {
            string folder = temp.MakeDirectory();

            string photo = Path.Combine(folder, "Sunset over the bay.png");
            WriteSunset(photo);

            string report = Path.Combine(folder, "Quarterly report.pdf");
            File.WriteAllBytes(report, Filler("%PDF-1.4\n", 184 * 1024));

            string sheet = Path.Combine(folder, "Budget 2026.xlsx");
            File.WriteAllBytes(sheet, Filler("PK", 51 * 1024));

            string notes = Path.Combine(folder, "Meeting notes.txt");
            File.WriteAllText(notes, new string('x', 1280));

            return new[]
            {
                ShelfItem.ForFile(photo),
                ShelfItem.ForFile(report),
                ShelfItem.ForLink(new Uri("https://github.com/Dat98it/ShelfDrop")),
                ShelfItem.ForFile(sheet),
                ShelfItem.ForText("Remember to send the invoice before Friday"),
                ShelfItem.ForFile(notes),
            };
        }

        private static byte[] Filler(string start, int length)
        {
            var bytes = new byte[length];
            Encoding.ASCII.GetBytes(start).CopyTo(bytes, 0);
            return bytes;
        }

        private static void WriteSunset(string path)
        {
            const int width = 640;
            const int height = 400;
            var sky = new LinearGradientBrush(Color.FromRgb(0xFC, 0x9E, 0x54), Color.FromRgb(0x5C, 0x33, 0x99), 90);
            sky.GradientStops.Insert(1, new GradientStop(Color.FromRgb(0xED, 0x54, 0x80), 0.55));

            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawRectangle(sky, null, new Rect(0, 0, width, height));
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFF, 0xF2, 0xBF)), null, new Point(430, 190), 70, 70);
                context.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x1F, 0x14, 0x38)), null,
                    Geometry.Parse("M0,400 L0,290 C220,230 440,360 640,300 L640,400 Z"));
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
