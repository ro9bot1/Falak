using System.Linq;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Falak.Core;
using Falak.Interop;

namespace Falak.App;

internal static class Art
{
	public static BitmapSource Mark(int px, bool tile = true, Color? mono = null)
	{
        var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/assets/falak.ico"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - px)).First();
        BitmapSource image = new TransformedBitmap(frame, new ScaleTransform((double)px / frame.PixelWidth, (double)px / frame.PixelHeight));
        image.Freeze();
        return image;
	}

	public static BitmapSource SampleWheel(BrandPalette palette, double scale = 2.0, bool arabic = true)
	{
		WheelRenderer wheelRenderer = new WheelRenderer(palette, arabic);
		WheelMenu wheelMenu = SliceCatalog.For(WheelMode.Convert, FileKind.Image, ".heic");
		return wheelRenderer.RenderBitmap(wheelMenu, WheelMode.Convert, WheelHit.OfSlice(0), new WheelGeometry(wheelMenu.Slices.Count, scale));
	}
}
