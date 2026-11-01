using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Falak.Core;

namespace Falak.App;

internal sealed class ColorPicker : UserControl
{
	private const double SquareWidth = 248.0;

	private const double SquareHeight = 156.0;

	private const double StripHeight = 14.0;

	private readonly Border _square = new Border
	{
		Width = 248.0,
		Height = 156.0,
		CornerRadius = new CornerRadius(10.0)
	};

	private readonly Ellipse _squareThumb = new Ellipse
	{
		Width = 16.0,
		Height = 16.0,
		Stroke = Brushes.White,
		StrokeThickness = 2.5,
		IsHitTestVisible = false
	};

	private readonly Canvas _squareLayer = new Canvas
	{
		Width = 248.0,
		Height = 156.0,
		Background = Brushes.Transparent
	};

	private readonly Canvas _stripLayer = new Canvas
	{
		Width = 248.0,
		Height = 20.0,
		Background = Brushes.Transparent
	};

	private readonly Border _stripThumb = new Border
	{
		Width = 10.0,
		Height = 20.0,
		CornerRadius = new CornerRadius(5.0),
		BorderBrush = Brushes.White,
		BorderThickness = new Thickness(2.5),
		IsHitTestVisible = false
	};

	private readonly Border _preview = new Border
	{
		Width = 34.0,
		Height = 34.0,
		CornerRadius = new CornerRadius(9.0),
		BorderThickness = new Thickness(1.0)
	};

	private readonly TextBox _hex = new TextBox
	{
		Width = 110.0,
		FlowDirection = FlowDirection.LeftToRight,
		FontFamily = new FontFamily("Consolas, Segoe UI"),
		FontSize = 13.5,
		VerticalContentAlignment = VerticalAlignment.Center,
		Padding = new Thickness(8.0, 5.0, 8.0, 5.0)
	};

	private readonly WrapPanel _documentRow = new WrapPanel
	{
		Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
		FlowDirection = FlowDirection.RightToLeft
	};

	private readonly TextBlock _documentLabel = new TextBlock
	{
		Text = "من ألوان الملف",
		Margin = new Thickness(0.0, 14.0, 0.0, 0.0),
		FlowDirection = FlowDirection.RightToLeft,
		HorizontalAlignment = HorizontalAlignment.Right
	};

	private double _h;

	private double _s;

	private double _v = 1.0;

	private bool _updating;

	public Rgb Value => Rgb.FromHsv(_h, _s, _v);

	public event Action<Rgb>? ColorChanged;

	public ColorPicker()
	{
		FlowDirection = FlowDirection.LeftToRight;
		DropShadowEffect effect = new DropShadowEffect
		{
			BlurRadius = 4.0,
			ShadowDepth = 0.0,
			Opacity = 0.6,
			Color = Colors.Black
		};
		_squareThumb.Effect = effect;
		_stripThumb.Effect = effect;
		Grid grid = new Grid
		{
			Width = 248.0,
			Height = 156.0,
			Cursor = Cursors.Cross
		};
		grid.Children.Add(_square);
		grid.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(10.0),
			Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0)
		});
		grid.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(10.0),
			Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90.0)
		});
		_squareLayer.Children.Add(_squareThumb);
		grid.Children.Add(_squareLayer);
		_squareLayer.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			_squareLayer.CaptureMouse();
			PickSquare(e.GetPosition(_squareLayer));
		};
		_squareLayer.MouseMove += (object _, MouseEventArgs e) =>
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			if (_squareLayer.IsMouseCaptured)
			{
				PickSquare(e.GetPosition(_squareLayer));
			}
		};
		_squareLayer.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			_squareLayer.ReleaseMouseCapture();
		};
		GradientStopCollection gradientStopCollection = new GradientStopCollection();
		for (int num = 0; num <= 6; num++)
		{
			Rgb rgb = Rgb.FromHsv(num * 60, 1.0, 1.0);
			gradientStopCollection.Add(new GradientStop(Color.FromRgb(rgb.R, rgb.G, rgb.B), (double)num / 6.0));
		}
		Grid grid2 = new Grid
		{
			Width = 248.0,
			Height = 20.0,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			Cursor = Cursors.Hand
		};
		grid2.Children.Add(new Border
		{
			Height = 14.0,
			CornerRadius = new CornerRadius(7.0),
			Background = new LinearGradientBrush(gradientStopCollection, 0.0)
		});
		_stripLayer.Children.Add(_stripThumb);
		grid2.Children.Add(_stripLayer);
		_stripLayer.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			_stripLayer.CaptureMouse();
			Point position = e.GetPosition(_stripLayer);
			PickHue(position.X);
		};
		_stripLayer.MouseMove += (object _, MouseEventArgs e) =>
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			if (_stripLayer.IsMouseCaptured)
			{
				Point position = e.GetPosition(_stripLayer);
				PickHue(position.X);
			}
		};
		_stripLayer.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			_stripLayer.ReleaseMouseCapture();
		};
		_preview.SetResourceReference(Border.BorderBrushProperty, "Line");
		_hex.SetResourceReference(Control.BackgroundProperty, "SurfaceRaised");
		_hex.SetResourceReference(Control.ForegroundProperty, "Ink");
		_hex.SetResourceReference(Control.BorderBrushProperty, "Line");
		_hex.KeyDown += (object _, KeyEventArgs e) =>
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			if ((int)e.Key == 6)
			{
				ApplyHex();
			}
		};
		_hex.LostFocus += (object _, RoutedEventArgs _) =>
		{
			ApplyHex();
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(_preview);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "#",
			Margin = new Thickness(12.0, 0.0, 4.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			FontSize = 14.0
		});
		stackPanel.Children.Add(_hex);
		_documentLabel.SetResourceReference(FrameworkElement.StyleProperty, "Caption");
		StackPanel stackPanel2 = new StackPanel
		{
			Width = 248.0
		};
		stackPanel2.Children.Add(grid);
		stackPanel2.Children.Add(grid2);
		stackPanel2.Children.Add(stackPanel);
		stackPanel2.Children.Add(_documentLabel);
		stackPanel2.Children.Add(_documentRow);
		Content = stackPanel2;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			UpdateVisuals();
		};
	}

	public void SetColor(Rgb colour)
	{
		var (h, num, num2) = colour.ToHsv();
		if (num > 0.01 && num2 > 0.01)
		{
			_h = h;
		}
		_s = num;
		_v = num2;
		UpdateVisuals();
	}

	public void SetDocumentColors(IReadOnlyList<Rgb> colours)
	{
		_documentRow.Children.Clear();
		Rgb[] second = new Rgb[2]
		{
			Rgb.White,
			Rgb.Black
		};
		foreach (Rgb colour in colours.Concat(second))
		{
			Border border = new Border
			{
				Width = 26.0,
				Height = 26.0,
				CornerRadius = new CornerRadius(13.0),
				Margin = new Thickness(0.0, 0.0, 8.0, 6.0),
				Cursor = Cursors.Hand,
				Background = new SolidColorBrush(Color.FromRgb(colour.R, colour.G, colour.B)),
				BorderThickness = new Thickness(1.0),
				ToolTip = colour.Hex
			};
			border.SetResourceReference(Border.BorderBrushProperty, "Line");
			border.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
			{
				SetColor(colour);
				ColorChanged?.Invoke(colour);
			};
			_documentRow.Children.Add(border);
		}
		_documentLabel.Visibility = ((colours.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void PickSquare(Point p)
	{
		_s = Math.Clamp(p.X / 248.0, 0.0, 1.0);
		_v = 1.0 - Math.Clamp(p.Y / 156.0, 0.0, 1.0);
		Changed();
	}

	private void PickHue(double x)
	{
		_h = Math.Clamp(x / 248.0, 0.0, 1.0) * 359.9;
		Changed();
	}

	private void ApplyHex()
	{
		if (!_updating && Rgb.TryParseHex(_hex.Text, out var rgb) && !(rgb == Value))
		{
			SetColor(rgb);
			ColorChanged?.Invoke(rgb);
		}
	}

	private void Changed()
	{
		UpdateVisuals();
		ColorChanged?.Invoke(Value);
	}

	private void UpdateVisuals()
	{
		_updating = true;
		Rgb rgb = Rgb.FromHsv(_h, 1.0, 1.0);
		_square.Background = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
		Canvas.SetLeft(_squareThumb, _s * 248.0 - 8.0);
		Canvas.SetTop(_squareThumb, (1.0 - _v) * 156.0 - 8.0);
		Canvas.SetLeft(_stripThumb, _h / 360.0 * 248.0 - 5.0);
		Rgb value = Value;
		_preview.Background = new SolidColorBrush(Color.FromRgb(value.R, value.G, value.B));
		if (!_hex.IsKeyboardFocused)
		{
			_hex.Text = value.Hex.TrimStart('#');
		}
		_updating = false;
	}
}
