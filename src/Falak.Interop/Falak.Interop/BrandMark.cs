using System;
using System.Windows;
using System.Windows.Media;

namespace Falak.Interop;

public static class BrandMark
{
	public static readonly Color Tile = BrandPalette.Hex("#0E6B53");

	public static readonly Color Basalt = BrandPalette.Hex("#121614");

	public static readonly Color GoldHub = BrandPalette.Hex("#D2AE68");

	public static void Draw(DrawingContext dc, double size, bool tile = true, Color? mono = null, Color? tileColor = null)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		double num = size / 1024.0;
		if (tile)
		{
			dc.DrawRoundedRectangle(new SolidColorBrush(tileColor ?? Tile), null, new Rect(0.0, 0.0, size, size), 230.0 * num, 230.0 * num);
		}
		double num2 = (tile ? 1.0 : 1.32);
		SolidColorBrush brush = new SolidColorBrush(mono ?? Colors.White);
		Point center = new Point(size / 2.0, size / 2.0);
		double inner = 170.0 * num * num2;
		double outer = 300.0 * num * num2;
		dc.DrawGeometry(brush, null, Sector(center, 60.0, 330.0, inner, outer));
		double num3 = 70.0 * num * num2;
		double num4 = Math.PI * -5.0 / 12.0;
		Point center2 = new Point(center.X + num3 * Math.Cos(num4), center.Y + num3 * Math.Sin(num4));
		dc.DrawGeometry(brush, null, Sector(center2, -18.0, 48.0, inner, 330.0 * num * num2));
	}

	public static StreamGeometry Sector(Point center, double fromDeg, double toDeg, double inner, double outer)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		bool isLargeArc = toDeg - fromDeg > 180.0;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(Polar(center, fromDeg, outer), isFilled: true, isClosed: true);
			streamGeometryContext.ArcTo(Polar(center, toDeg, outer), new Size(outer, outer), 0.0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
			streamGeometryContext.LineTo(Polar(center, toDeg, inner), isStroked: true, isSmoothJoin: true);
			streamGeometryContext.ArcTo(Polar(center, fromDeg, inner), new Size(inner, inner), 0.0, isLargeArc, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
		}
		((Freezable)streamGeometry).Freeze();
		return streamGeometry;
	}

	public static Point Polar(Point center, double degrees, double radius)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		double num = degrees * Math.PI / 180.0;
		return new Point(center.X + radius * Math.Sin(num), center.Y - radius * Math.Cos(num));
	}
}
