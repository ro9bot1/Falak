using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Falak.Core;

namespace Falak.Interop;

public sealed class WheelRenderer
{
	private const int MaxCachedFrames = 256;

	private const double LabelBoxWidth = 80.0;

	private const double HoverLift = 4.0;

	private const double SliceGap = 5.0;

	private const double SliceRounding = 10.0;

	private const double IconLabelGap = 4.0;

	private const double DiscRadius = 136.0;

	public static readonly FontFamily UiFont = new FontFamily("Thmanyah Sans, Thmanyah sans 1.2, Dubai, Segoe UI");

	private readonly Dictionary<string, byte[]> _frames = new Dictionary<string, byte[]>();

	private readonly Dictionary<(int Count, int Index), Geometry> _pillows = new Dictionary<(int, int), Geometry>();

	private readonly BrandPalette _palette;

	private readonly bool _arabic;

	private readonly Typeface _labelFace = new Typeface(UiFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

	private readonly Typeface _centerFace = new Typeface(UiFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

	private readonly CultureInfo _culture;

	private static readonly Typeface IconFace = new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

	public bool Glass { get; }

	public WheelRenderer(BrandPalette palette, bool arabic = true, bool glass = false)
	{
		_palette = palette;
		_arabic = arabic;
		Glass = glass;
		_culture = CultureInfo.GetCultureInfo(arabic ? "ar-SA" : "en-US");
	}

	public byte[] Render(WheelMenu menu, WheelMode mode, WheelHit hit, WheelGeometry geometry)
	{
		string key = $"{mode}|{menu.Hub.Id}|{string.Join(',', menu.Slices.Select((WheelAction s) => s.Id))}|{hit}|{geometry.SizePx}";
		if (_frames.TryGetValue(key, out byte[] value))
		{
			return value;
		}
		BitmapSource bitmapSource = RenderBitmap(menu, mode, hit, geometry);
		byte[] array = new byte[geometry.SizePx * geometry.SizePx * 4];
		bitmapSource.CopyPixels(array, geometry.SizePx * 4, 0);
		if (_frames.Count >= 256)
		{
			_frames.Clear();
		}
		_frames[key] = array;
		return array;
	}

	public BitmapSource RenderBitmap(WheelMenu menu, WheelMode mode, WheelHit hit, WheelGeometry geometry)
	{
		DrawingVisual drawingVisual = new DrawingVisual
		{
			Effect = new DropShadowEffect
			{
				BlurRadius = 30.0,
				ShadowDepth = 6.0,
				Direction = 270.0,
				Opacity = (_palette.IsDark ? 0.55 : 0.2),
				Color = Color.FromRgb(8, 20, 15)
			}
		};
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.PushTransform(new ScaleTransform(geometry.Scale, geometry.Scale));
			Draw(drawingContext, menu, mode, hit, geometry.Scale);
			drawingContext.Pop();
		}
		int sizePx = geometry.SizePx;
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(sizePx, sizePx, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		((Freezable)renderTargetBitmap).Freeze();
		return renderTargetBitmap;
	}

	private void Draw(DrawingContext dc, WheelMenu menu, WheelMode mode, WheelHit hit, double pixelsPerDip)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0486: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_052c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_063b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0566: Unknown result type (might be due to invalid IL or missing references)
		//IL_069d: Unknown result type (might be due to invalid IL or missing references)
		//IL_070d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		Point center = new Point(150.0, 150.0);
		bool isPending = menu.IsPending;
		bool isDark = _palette.IsDark;
		Brush brush = (Glass ? ((Brush)new LinearGradientBrush(BrandPalette.WithAlpha(BrandPalette.Mix(_palette.WheelDisc, Colors.White, isDark ? 0.08 : 0.12), 240), BrandPalette.WithAlpha(BrandPalette.Mix(_palette.WheelDisc, Colors.Black, isDark ? 0.12 : 0.06), 236), 90.0)) : ((Brush)_palette.Brush(_palette.WheelDisc)));
		dc.DrawEllipse(brush, new Pen(_palette.Brush(BrandPalette.WithAlpha(_palette.Line, 200)), 1.0), center, 136.0, 136.0);
		LinearGradientBrush brush2 = new LinearGradientBrush(Color.FromArgb((byte)(isDark ? 70u : 230u), byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb((byte)(isDark ? 8u : 40u), byte.MaxValue, byte.MaxValue, byte.MaxValue), 90.0);
		dc.DrawEllipse(null, new Pen(brush2, 1.2), center, 134.8, 134.8);
		int count = menu.Slices.Count;
		WheelGeometry wheelGeometry = new WheelGeometry(count);
		WheelAction wheelAction = menu.ActionFor(hit);
		for (int i = 0; i < count; i++)
		{
			bool flag = hit.Kind == WheelHitKind.Slice && hit.Slice == i && !isPending;
			double degrees = wheelGeometry.SliceStartDegrees(i) + wheelGeometry.SliceSweep / 2.0;
			Point val = (Point)(flag ? BrandMark.Polar(new Point(0.0, 0.0), degrees, 4.0) : new Point(0.0, 0.0));
			dc.PushTransform(new TranslateTransform(val.X, val.Y));
			Pen pen = null;
			Brush brush3;
			if (flag)
			{
				brush3 = new LinearGradientBrush(BrandPalette.Mix(_palette.Accent, Colors.White, isDark ? 0.06 : 0.12), _palette.Accent, 90.0);
			}
			else
			{
				Color a = ((i == 0 && !isPending) ? BrandPalette.Mix(_palette.WheelSlice, _palette.Accent, 0.1) : _palette.WheelSlice);
				byte alpha = (byte)((!Glass) ? 255u : (isDark ? 236u : 242u));
				brush3 = new LinearGradientBrush(BrandPalette.WithAlpha(BrandPalette.Mix(a, Colors.White, isDark ? 0.05 : 0.55), alpha), BrandPalette.WithAlpha(BrandPalette.Mix(a, Colors.Black, isDark ? 0.1 : 0.03), alpha), 90.0);
				pen = new Pen(new LinearGradientBrush(Color.FromArgb((byte)(isDark ? 40u : 255u), byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb((byte)(isDark ? 12u : 90u), byte.MaxValue, byte.MaxValue, byte.MaxValue), 90.0), 1.0);
			}
			dc.DrawGeometry(brush3, pen, Pillow(count, i, center));
			if (!isPending)
			{
				Point at = BrandMark.Polar(center, degrees, 92.0);
                // Only the Tools wheel becomes denser. Preserve all geometry,
                // visual palette and the 8-slice Space wheel from the original.
                bool compactTools = (mode == WheelMode.Tools && count >= 8) || (mode == WheelMode.Actions && count >= 10);
                double slotWidth = compactTools
                    ? (count >= 10 ? 49.0 : count == 9 ? 55.0 : 66.0)
                    : 80.0;
                DrawIconLabel(dc, menu.Slices[i], at,
                    flag ? _palette.AccentInk : _palette.Ink,
                    flag ? _palette.AccentInk : _palette.Accent,
                    compactTools ? 16.8 : 19.0,
                    compactTools ? (count >= 10 ? 8.1 : 9.1) : 10.0,
                    pixelsPerDip, slotWidth,
                    compactTools ? CompactToolsLabel(menu.Slices[i]) : null);
			}
			dc.Pop();
		}
		if (menu.IsEmpty)
		{
			dc.DrawGeometry(_palette.Brush(_palette.WheelSlice), null, BrandMark.Sector(center, -60.0, 60.0, 56.0, 128.0));
			FormattedText formattedText = Text(Label(menu.Hub), 11.0, _labelFace, _palette.Muted, pixelsPerDip);
			Point val2 = BrandMark.Polar(center, 0.0, 92.0);
			dc.DrawText(formattedText, new Point(val2.X - 40.0, val2.Y - formattedText.Height / 2.0));
		}
		for (int num = 4; num >= 1; num--)
		{
			dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(isDark ? 26u : 12u), 0, 0, 0)), null, new Point(150.0, 151.5), 46.0 + (double)num, 46.0 + (double)num);
		}
		dc.DrawEllipse(_palette.Brush(_palette.Surface), new Pen(_palette.Brush(_palette.Gold), 1.3), center, 46.0, 46.0);
		if (isPending)
		{
			FormattedText formattedText2 = Text("•••", 12.0, _centerFace, _palette.Muted, pixelsPerDip);
			dc.DrawText(formattedText2, new Point(110.0, 150.0 - formattedText2.Height / 2.0));
		}
		else if ((object)wheelAction != null)
		{
			FormattedText formattedText3 = Text(Label(wheelAction), 12.5, _centerFace, _palette.Ink, pixelsPerDip);
			dc.DrawText(formattedText3, new Point(110.0, 150.0 - formattedText3.Height / 2.0));
		}
		else
		{
			string text = (_arabic ? SliceCatalog.CaptionAr(mode) : SliceCatalog.CaptionEn(mode));
			FormattedText formattedText4 = Text(text, 11.5, _labelFace, _palette.Muted, pixelsPerDip);
			dc.DrawText(formattedText4, new Point(110.0, 150.0 - formattedText4.Height / 2.0));
		}
	}

	private Geometry Pillow(int count, int index, Point center)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		if (_pillows.TryGetValue((count, index), out Geometry value))
		{
			return value;
		}
		WheelGeometry wheelGeometry = new WheelGeometry(count);
		double num = wheelGeometry.SliceStartDegrees(index);
		double num2 = num + wheelGeometry.SliceSweep;
		Geometry geometry;
		double d;
		if (count == 1)
		{
			geometry = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(center, 128.0, 128.0), new EllipseGeometry(center, 56.0, 56.0));
		}
		else
		{
			double num3 = 66.0;
			double num4 = 118.0;
			d = 12.5;
			double num5 = num + Inset(num4);
			double num6 = num2 - Inset(num4);
			double num7 = num + Inset(num3);
			double num8 = num2 - Inset(num3);
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(BrandMark.Polar(center, num5, num4), isFilled: true, isClosed: true);
				streamGeometryContext.ArcTo(BrandMark.Polar(center, num6, num4), new Size(num4, num4), 0.0, num6 - num5 > 180.0, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
				streamGeometryContext.LineTo(BrandMark.Polar(center, num8, num3), isStroked: true, isSmoothJoin: true);
				streamGeometryContext.ArcTo(BrandMark.Polar(center, num7, num3), new Size(num3, num3), 0.0, num8 - num7 > 180.0, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
			}
			Pen pen = new Pen(Brushes.Black, 20.0)
			{
				LineJoin = PenLineJoin.Round
			};
			geometry = Geometry.Combine(streamGeometry, streamGeometry.GetWidenedPathGeometry(pen), GeometryCombineMode.Union, null);
		}
		((Freezable)geometry).Freeze();
		_pillows[(count, index)] = geometry;
		return geometry;
		double Inset(double radius)
		{
			return Math.Asin(Math.Min(1.0, d / radius)) * 180.0 / Math.PI;
		}
	}

	private void DrawIconLabel(DrawingContext dc, WheelAction action, Point at, Color ink, Color iconInk, double iconSize, double labelSize, double pixelsPerDip, double labelWidth, string? shortenedLabel)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		FormattedText formattedText = new FormattedText(ActionGlyphs.For(action.Id), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, IconFace, iconSize, _palette.Brush(iconInk), pixelsPerDip);
		FormattedText formattedText2 = Text(shortenedLabel ?? Label(action), labelSize, _labelFace, ink, pixelsPerDip);
        formattedText2.MaxTextWidth = labelWidth;
		formattedText2.MaxLineCount = shortenedLabel is null ? 1 : 2;
		double num = formattedText.Height * 0.82 + 4.0 + formattedText2.Height;
		double num2 = at.Y - num / 2.0;
		dc.DrawText(formattedText, new Point(at.X - formattedText.Width / 2.0, num2));
		dc.DrawText(formattedText2, new Point(at.X - labelWidth / 2.0, num2 + formattedText.Height * 0.82 + 4.0));
	}

    // Tool names are shortened in the visual wheel ONLY, not in dialogs/logs.
    // More actions are accessible without rendering text outside the wedges.
    private string CompactToolsLabel(WheelAction action)
    {
        return action.Id switch
        {
            "tool.video-metadata" => _arabic ? "حذف البيانات" : "Strip data",
            "tool.video-compress" => _arabic ? "ضغط الفيديو" : "Compress",
            "tool.video-redact" => _arabic ? "حجب منطقة" : "Redact",
            "tool.video-snapshot" => _arabic ? "لقطة" : "Snapshot",
            "tool.video-split" => _arabic ? "تقسيم" : "Split",
            "tool.messaging" => _arabic ? "إرسال" : "Message",
            "tool.audio-metadata" => _arabic ? "حذف البيانات" : "Strip data",
            "tool.strip-metadata" => _arabic ? "حذف البيانات" : "Strip data",
            "tool.image-redact" => _arabic ? "حجب منطقة" : "Redact",
            "tool.image-annotate" => _arabic ? "رسم وكتابة" : "Annotate",
            "tool.compress-pdf-strong" => _arabic ? "ضغط قوي" : "Strong",
            _ => Label(action)
        };
    }

	private string Label(WheelAction action)
	{
		if (!_arabic)
		{
			return action.LabelEn;
		}
		return action.LabelAr;
	}

	private FormattedText Text(string text, double size, Typeface face, Color color, double pixelsPerDip)
	{
		return new FormattedText(text, _culture, _arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, face, size, _palette.Brush(color), pixelsPerDip)
		{
			MaxTextWidth = 80.0,
			TextAlignment = TextAlignment.Center,
			MaxLineCount = 2,
			Trimming = TextTrimming.CharacterEllipsis,
			LineHeight = size * 1.35
		};
	}
}
