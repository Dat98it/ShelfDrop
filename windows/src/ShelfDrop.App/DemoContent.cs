using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

        /// <summary>
        /// A shelf that looks good in a picture: a few photos, a PDF, a folder, an archive, a link and a note. Sizes are made
        /// to look like real ones. <paramref name="count"/> is 6 (the default shelf) or 10 (a wide one: two full rows of five).
        /// </summary>
        public static IReadOnlyList<ShelfItem> CreateForPictures(TempStorage temp, int count)
        {
            string folder = temp.MakeDirectory();

            string sunset = Path.Combine(folder, "Sunset.png");
            string mountains = Path.Combine(folder, "Mountains.png");
            WritePhoto(sunset, Photo.Sunset);
            WritePhoto(mountains, Photo.Mountains);

            string report = Path.Combine(folder, "Q3 report.pdf");
            File.WriteAllBytes(report, Filler("%PDF-1.7\n", 2_516_582));   // 2.4 MB

            string assets = Path.Combine(folder, "Project assets");
            Directory.CreateDirectory(assets);
            File.WriteAllText(Path.Combine(assets, "readme.txt"), "Logos, icons and mock-ups.");

            ShelfItem photo = ShelfItem.ForFile(sunset);
            ShelfItem link = ShelfItem.ForLink(new Uri("https://github.com/Dat98it/ShelfDrop"));
            ShelfItem pdf = ShelfItem.ForFile(report);
            ShelfItem projectFolder = ShelfItem.ForFile(assets);
            ShelfItem note = ShelfItem.ForText("Ideas for v2");
            ShelfItem trail = ShelfItem.ForFile(mountains);

            if (count <= 6) return new[] { photo, link, pdf, projectFolder, note, trail };

            string aurora = Path.Combine(folder, "Aurora.png");
            string ocean = Path.Combine(folder, "Ocean.png");
            WritePhoto(aurora, Photo.Aurora);
            WritePhoto(ocean, Photo.Ocean);
            string archive = Path.Combine(folder, "Designs.zip");
            using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                using (Stream entry = zip.CreateEntry("mockups.bin", CompressionLevel.NoCompression).Open())
                    entry.Write(new byte[5_557_452], 0, 5_557_452);   // 5.3 MB
            }

            return new[]
            {
                photo, link, pdf, trail, projectFolder,
                note, ShelfItem.ForFile(aurora), ShelfItem.ForFile(archive), ShelfItem.ForFile(ocean),
                ShelfItem.ForLink(new Uri("https://figma.com/files/team/shelfdrop")),
            };
        }

        private enum Photo
        {
            Sunset,
            Mountains,
            Aurora,
            Ocean,
        }

        private static byte[] Filler(string start, int length)
        {
            var bytes = new byte[length];
            Encoding.ASCII.GetBytes(start).CopyTo(bytes, 0);
            return bytes;
        }

        private static void WriteSunset(string path) => WritePhoto(path, Photo.Sunset);

        /// <summary>Draws a made-up photograph (a landscape in flat colours) and saves it as a PNG, 1600 x 1000.</summary>
        private static void WritePhoto(string path, Photo kind)
        {
            const int width = 1600;
            const int height = 1000;
            const double scale = 2.5;   // the pictures below are drawn on a 640 x 400 canvas

            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.PushTransform(new ScaleTransform(scale, scale));
                switch (kind)
                {
                    case Photo.Sunset:
                        Sky(context, 0xFC9E54, 0xED5480, 0x5C3399);
                        context.DrawEllipse(Brush(0xFFF2BF), null, new Point(430, 190), 70, 70);
                        context.DrawGeometry(Brush(0x1F1438), null, Geometry.Parse("M0,400 L0,290 C220,230 440,360 640,300 L640,400 Z"));
                        break;

                    case Photo.Mountains:
                        Sky(context, 0xA6DBFF, 0x4D8FEB, 0x1A3380);
                        context.DrawEllipse(Brush(0xFFFFF2), null, new Point(470, 90), 34, 34);
                        context.DrawGeometry(Brush(0x16264D), null, Geometry.Parse("M0,400 L0,250 L150,150 L250,215 L400,95 L520,200 L640,140 L640,400 Z"));
                        context.DrawGeometry(Brush(0xF2F7FF), null, Geometry.Parse("M400,95 L368,140 L392,132 L410,150 L430,128 L452,142 Z"));
                        context.DrawGeometry(Brush(0x0B1633), null, Geometry.Parse("M0,400 L0,320 C160,280 320,360 640,290 L640,400 Z"));
                        break;

                    case Photo.Aurora:
                        Sky(context, 0x050B24, 0x0C2A47, 0x103B4A);
                        for (int i = 0; i < 40; i++)
                            context.DrawEllipse(Brush(0xFFFFFF), null, new Point((i * 97) % 640, (i * 53) % 220), 0.9, 0.9);
                        Glow(context, 0x2BFF9E, new Point(220, 150), 260, 70, 0.55);
                        Glow(context, 0x7A5CFF, new Point(430, 130), 220, 60, 0.45);
                        context.DrawGeometry(Brush(0x040A14), null, Geometry.Parse("M0,400 L0,330 L90,300 L150,320 L260,285 L380,325 L500,295 L640,330 L640,400 Z"));
                        break;

                    case Photo.Ocean:
                        Sky(context, 0xFFD9B8, 0xFF9FB2, 0x8E7CC3);
                        context.DrawEllipse(Brush(0xFFF6D5), null, new Point(320, 205), 52, 52);
                        context.DrawRectangle(new LinearGradientBrush(Rgb(0x2D7FB8), Rgb(0x0B2F5E), 90), null, new Rect(0, 215, 640, 185));
                        for (int i = 0; i < 9; i++)
                            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xF1, 0xC8)), null,
                                new Rect(320 - 44 + i * 2, 222 + i * 17, 88 - i * 4, 4));
                        break;
                }
                context.Pop();
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        private static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        private static SolidColorBrush Brush(int rgb) => new SolidColorBrush(Rgb(rgb));

        private static void Sky(DrawingContext context, int top, int middle, int bottom)
        {
            var sky = new LinearGradientBrush(Color.FromRgb((byte)(top >> 16), (byte)(top >> 8), (byte)top),
                Color.FromRgb((byte)(bottom >> 16), (byte)(bottom >> 8), (byte)bottom), 90);
            sky.GradientStops.Insert(1, new GradientStop(Color.FromRgb((byte)(middle >> 16), (byte)(middle >> 8), (byte)middle), 0.55));
            context.DrawRectangle(sky, null, new Rect(0, 0, 640, 400));
        }

        private static void Glow(DrawingContext context, int rgb, Point centre, double radiusX, double radiusY, double strength)
        {
            Color colour = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            var brush = new RadialGradientBrush(Color.FromArgb((byte)(255 * strength), colour.R, colour.G, colour.B), Color.FromArgb(0, colour.R, colour.G, colour.B));
            context.DrawEllipse(brush, null, centre, radiusX, radiusY);
        }
    }
}
