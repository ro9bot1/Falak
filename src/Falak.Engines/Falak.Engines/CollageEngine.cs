using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Falak.Engines;

// Fully offline, non-destructive collage: no new dependencies or network services.
internal static class CollageEngine
{
    public static JobResult Create(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
    {
        if (inputs.Count < 2) throw new UserFacingException("اختر صورتين على الأقل لإنشاء كولاج.");
        if (inputs.Count > 36) throw new UserFacingException("الحد الأقصى للكولاج 36 صورة.");
        const int cell = 480;
        const int gutter = 12;
        int cols = (int)Math.Ceiling(Math.Sqrt(inputs.Count));
        int rows = (inputs.Count + cols - 1) / cols;
        int width = cols * cell + (cols + 1) * gutter;
        int height = rows * cell + (rows + 1) * gutter;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            for (int i = 0; i < inputs.Count; ++i)
            {
                ct.ThrowIfCancellationRequested();
                BitmapSource image = ImageEngine.Load(inputs[i]);
                double scale = Math.Min((double)cell / image.PixelWidth, (double)cell / image.PixelHeight);
                double w = image.PixelWidth * scale;
                double h = image.PixelHeight * scale;
                int x = i % cols;
                int y = i / cols;
                var rect = new Rect(gutter + x * (cell + gutter) + (cell - w) / 2.0,
                    gutter + y * (cell + gutter) + (cell - h) / 2.0, w, h);
                dc.DrawImage(image, rect);
                progress?.Report((double)(i + 1) / inputs.Count * 0.9);
            }
        }
        ct.ThrowIfCancellationRequested();
        var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        var output = OutputPaths.NextTo(inputs[0], ".png", "Collage");
        OutputPaths.WriteAtomically(output, temp =>
        {
            using var stream = File.Create(temp);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(result));
            encoder.Save(stream);
        });
        progress?.Report(1.0);
        return new JobResult(new[] { output }, null, "أنشأنا لوحة صور مجمعة");
    }
}
