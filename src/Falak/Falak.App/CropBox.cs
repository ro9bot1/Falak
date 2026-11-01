using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Falak.App;

internal sealed class CropBox : FrameworkElement
{
	private enum Part
	{
		None,
		Move,
		N,
		S,
		E,
		W,
		NE,
		NW,
		SE,
		SW
	}

	private const double Inset = 18.0;

	private const double CornerHit = 16.0;

	private const double EdgeHit = 10.0;

	private const double MinScreenSize = 36.0;

	private BitmapSource? _source;

	private Rect _crop;

	private Part _dragging;

	private Point _dragStart;

	private Rect _cropAtStart;

	public BitmapSource? Source
	{
		get
		{
			return _source;
		}
		set
		{
			_source = value;
			InvalidateVisual();
		}
	}

	public Rect Crop
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _crop;
		}
		set
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			_crop = Clamp(value);
			InvalidateVisual();
			CropChanged?.Invoke();
		}
	}

	private Rect Bounds
	{
		get
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			if (_source != null)
			{
				return new Rect(0.0, 0.0, (double)_source.PixelWidth, (double)_source.PixelHeight);
			}
			return Rect.Empty;
		}
	}

	private Rect ImageRect
	{
		get
		{
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			if (_source == null)
			{
				return Rect.Empty;
			}
			double num = Math.Max(1.0, ActualWidth - 36.0);
			double num2 = Math.Min(val2: Math.Max(1.0, ActualHeight - 36.0) / (double)_source.PixelHeight, val1: num / (double)_source.PixelWidth);
			double num3 = (double)_source.PixelWidth * num2;
			double num4 = (double)_source.PixelHeight * num2;
			return new Rect((ActualWidth - num3) / 2.0, (ActualHeight - num4) / 2.0, num3, num4);
		}
	}

	private double Scale
	{
		get
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			if (_source != null)
			{
				Rect imageRect = ImageRect;
				return imageRect.Width / (double)_source.PixelWidth;
			}
			return 1.0;
		}
	}

	private Rect ScreenCrop
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			Rect imageRect = ImageRect;
			double scale = Scale;
			return new Rect(imageRect.X + _crop.X * scale, imageRect.Y + _crop.Y * scale, _crop.Width * scale, _crop.Height * scale);
		}
	}

	public event Action? CropChanged;

	public event Action? Committed;

	public CropBox()
	{
		FlowDirection = FlowDirection.LeftToRight;
		Focusable = true;
		FocusVisualStyle = null;
	}

	private Rect Clamp(Rect r)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Rect bounds = Bounds;
		if (bounds.IsEmpty)
		{
			return r;
		}
		double num = Math.Clamp(r.Width, 1.0, bounds.Width);
		double num2 = Math.Clamp(r.Height, 1.0, bounds.Height);
		double num3 = Math.Clamp(r.X, 0.0, bounds.Width - num);
		double num4 = Math.Clamp(r.Y, 0.0, bounds.Height - num2);
		return new Rect(num3, num4, num, num2);
	}

	private Part HitPart(Point p)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		if (_source == null)
		{
			return Part.None;
		}
		Rect screenCrop = ScreenCrop;
		bool flag = Near(p.X, screenCrop.Left, 16.0);
		bool flag2 = Near(p.X, screenCrop.Right, 16.0);
		bool flag3 = Near(p.Y, screenCrop.Top, 16.0);
		bool flag4 = Near(p.Y, screenCrop.Bottom, 16.0);
		if (flag3 & flag)
		{
			return Part.NW;
		}
		if (flag3 & flag2)
		{
			return Part.NE;
		}
		if (flag4 & flag)
		{
			return Part.SW;
		}
		if (flag4 & flag2)
		{
			return Part.SE;
		}
		bool flag5 = p.X > screenCrop.Left && p.X < screenCrop.Right;
		bool flag6 = p.Y > screenCrop.Top && p.Y < screenCrop.Bottom;
		if (flag5 && Near(p.Y, screenCrop.Top, 10.0))
		{
			return Part.N;
		}
		if (flag5 && Near(p.Y, screenCrop.Bottom, 10.0))
		{
			return Part.S;
		}
		if (flag6 && Near(p.X, screenCrop.Left, 10.0))
		{
			return Part.W;
		}
		if (flag6 && Near(p.X, screenCrop.Right, 10.0))
		{
			return Part.E;
		}
		if (!screenCrop.Contains(p))
		{
			return Part.None;
		}
		return Part.Move;
		static bool Near(double a, double b, double tolerance)
		{
			return Math.Abs(a - b) <= tolerance;
		}
	}

	private static Cursor CursorFor(Part part)
	{
		switch (part)
		{
		case Part.NW:
		case Part.SE:
			return Cursors.SizeNWSE;
		case Part.NE:
		case Part.SW:
			return Cursors.SizeNESW;
		case Part.N:
		case Part.S:
			return Cursors.SizeNS;
		case Part.E:
		case Part.W:
			return Cursors.SizeWE;
		case Part.Move:
			return Cursors.SizeAll;
		default:
			return Cursors.Arrow;
		}
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		base.OnMouseLeftButtonDown(e);
		Focus();
		if (e.ClickCount == 2)
		{
			Committed?.Invoke();
			return;
		}
		_dragging = HitPart(e.GetPosition(this));
		if (_dragging != Part.None)
		{
			_dragStart = e.GetPosition(this);
			_cropAtStart = _crop;
			CaptureMouse();
			e.Handled = true;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		base.OnMouseMove(e);
		Point position = e.GetPosition(this);
		if (_dragging == Part.None)
		{
			Cursor = CursorFor(HitPart(position));
			return;
		}
		double scale = Scale;
		double num = (position.X - _dragStart.X) / scale;
		double num2 = (position.Y - _dragStart.Y) / scale;
		double num3 = 36.0 / scale;
		Rect bounds = Bounds;
		Rect cropAtStart = _cropAtStart;
		double num4 = cropAtStart.Left;
		double num5 = cropAtStart.Top;
		double num6 = cropAtStart.Right;
		double num7 = cropAtStart.Bottom;
		if (_dragging == Part.Move)
		{
			double num8 = Math.Clamp(cropAtStart.X + num, 0.0, bounds.Width - cropAtStart.Width);
			double num9 = Math.Clamp(cropAtStart.Y + num2, 0.0, bounds.Height - cropAtStart.Height);
			_crop = new Rect(num8, num9, cropAtStart.Width, cropAtStart.Height);
		}
		else
		{
			bool flag;
			switch (_dragging)
			{
			case Part.W:
			case Part.NW:
			case Part.SW:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				num4 = Math.Clamp(cropAtStart.Left + num, 0.0, num6 - num3);
			}
			switch (_dragging)
			{
			case Part.E:
			case Part.NE:
			case Part.SE:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				num6 = Math.Clamp(cropAtStart.Right + num, num4 + num3, bounds.Width);
			}
			Part dragging = _dragging;
			if ((dragging == Part.N || (uint)(dragging - 6) <= 1u) ? true : false)
			{
				num5 = Math.Clamp(cropAtStart.Top + num2, 0.0, num7 - num3);
			}
			dragging = _dragging;
			if ((dragging == Part.S || (uint)(dragging - 8) <= 1u) ? true : false)
			{
				num7 = Math.Clamp(cropAtStart.Bottom + num2, num5 + num3, bounds.Height);
			}
			_crop = new Rect(num4, num5, num6 - num4, num7 - num5);
		}
		InvalidateVisual();
		CropChanged?.Invoke();
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		if (_dragging != Part.None)
		{
			_dragging = Part.None;
			ReleaseMouseCapture();
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		base.OnKeyDown(e);
		if ((int)e.Key == 6)
		{
			Committed?.Invoke();
			e.Handled = true;
		}
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		InvalidateVisual();
	}

	protected override void OnRender(DrawingContext dc)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_030c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0321: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_034d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0354: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0456: Unknown result type (might be due to invalid IL or missing references)
		//IL_0467: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0617: Unknown result type (might be due to invalid IL or missing references)
		//IL_0638: Unknown result type (might be due to invalid IL or missing references)
		//IL_068b: Unknown result type (might be due to invalid IL or missing references)
		dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
		if (_source != null)
		{
			Rect imageRect = ImageRect;
			Rect screenCrop = ScreenCrop;
			dc.DrawImage(_source, imageRect);
			CombinedGeometry geometry = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(imageRect), new RectangleGeometry(screenCrop));
			dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(150, 10, 14, 12)), null, geometry);
			Pen pen = new Pen(new SolidColorBrush(Color.FromArgb(110, byte.MaxValue, byte.MaxValue, byte.MaxValue)), 1.0);
			for (int i = 1; i <= 2; i++)
			{
				dc.DrawLine(pen, new Point(screenCrop.Left + screenCrop.Width * (double)i / 3.0, screenCrop.Top), new Point(screenCrop.Left + screenCrop.Width * (double)i / 3.0, screenCrop.Bottom));
				dc.DrawLine(pen, new Point(screenCrop.Left, screenCrop.Top + screenCrop.Height * (double)i / 3.0), new Point(screenCrop.Right, screenCrop.Top + screenCrop.Height * (double)i / 3.0));
			}
			dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 3.0), screenCrop);
			dc.DrawRectangle(null, new Pen(Brushes.White, 1.2), screenCrop);
			Pen pen2 = new Pen(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), 7.0)
			{
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round
			};
			Pen pen3 = new Pen(Brushes.White, 4.5)
			{
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round
			};
			double num = Math.Min(20.0, Math.Min(screenCrop.Width, screenCrop.Height) / 3.0);
			Pen[] array = new Pen[2] { pen2, pen3 };
			foreach (Pen pen4 in array)
			{
				L(screenCrop.TopLeft, screenCrop.TopLeft + new Vector(num, 0.0));
				L(screenCrop.TopLeft, screenCrop.TopLeft + new Vector(0.0, num));
				L(screenCrop.TopRight, screenCrop.TopRight + new Vector(0.0 - num, 0.0));
				L(screenCrop.TopRight, screenCrop.TopRight + new Vector(0.0, num));
				L(screenCrop.BottomLeft, screenCrop.BottomLeft + new Vector(num, 0.0));
				L(screenCrop.BottomLeft, screenCrop.BottomLeft + new Vector(0.0, 0.0 - num));
				L(screenCrop.BottomRight, screenCrop.BottomRight + new Vector(0.0 - num, 0.0));
				L(screenCrop.BottomRight, screenCrop.BottomRight + new Vector(0.0, 0.0 - num));
				double num2 = Math.Min(14.0, screenCrop.Width / 6.0);
				double num3 = Math.Min(14.0, screenCrop.Height / 6.0);
				double num4 = screenCrop.Left + screenCrop.Width / 2.0;
				double num5 = screenCrop.Top + screenCrop.Height / 2.0;
				L(new Point(num4 - num2, screenCrop.Top), new Point(num4 + num2, screenCrop.Top));
				L(new Point(num4 - num2, screenCrop.Bottom), new Point(num4 + num2, screenCrop.Bottom));
				L(new Point(screenCrop.Left, num5 - num3), new Point(screenCrop.Left, num5 + num3));
				L(new Point(screenCrop.Right, num5 - num3), new Point(screenCrop.Right, num5 + num3));
				void L(Point a, Point b)
				{
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					dc.DrawLine(pen4, a, b);
				}
			}
			FormattedText formattedText = new FormattedText($"{Math.Round(_crop.Width)} × {Math.Round(_crop.Height)}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11.5, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			if (screenCrop.Width > formattedText.Width + 24.0 && screenCrop.Height > 48.0)
			{
				Rect rectangle = new Rect(screenCrop.Left + (screenCrop.Width - formattedText.Width) / 2.0 - 9.0, screenCrop.Top + 10.0, formattedText.Width + 18.0, formattedText.Height + 6.0);
				dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(170, 10, 14, 12)), null, rectangle, rectangle.Height / 2.0, rectangle.Height / 2.0);
				dc.DrawText(formattedText, new Point(rectangle.Left + 9.0, rectangle.Top + 3.0));
			}
		}
	}
}
