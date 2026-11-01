using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Falak.Core;
using Falak.Engines;

namespace Falak.App;

public partial class DesignerWindow : Window, IComponentConnector
{
	private enum PickTarget
	{
		Solid,
		From,
		To,
		Pen
	}

	private const int ExportLongSide = 2160;

	private readonly string _source;

	private readonly bool _isPdf;

	private readonly JobRunner _jobs;

	private BitmapSource? _full;

	private Int32Rect? _crop;

	private int _page;

	private int _pages = 1;

	private double? _ratio = 1.0;

	private bool _cropping;

	private bool _syncing;

	private bool _chosenByUser;

	private BackgroundSpec _background = new BackgroundSpec(Rgb.White);

	private IReadOnlyList<Rgb> _documentColours = Array.Empty<Rgb>();

	private Rgb _penColour = new Rgb(229, 72, 77);

	private readonly Dictionary<int, StrokeCollection> _ink = new Dictionary<int, StrokeCollection>();

	private InkCanvas? _previewInk;

	private Grid? _previewRoot;

	private readonly ColorPicker _picker = new ColorPicker();

	private readonly Popup _pickerPopup;

	private PickTarget _pickTarget;

	private static readonly (string Label, double? Ratio)[] Sizes = new (string, double?)[5]
	{
		("مربع 1:1", 1.0),
		("منشور 4:5", 0.8),
		("قصة 9:16", 0.5625),
		("عريض 16:9", 1.7777777777777777),
		("كما هو", null)
	};

	private static readonly Rgb[] PenBasics = new Rgb[4]
	{
		Rgb.White,
		Rgb.Black,
		new Rgb(229, 72, 77),
		new Rgb(245, 197, 24)
	};

	private StrokeCollection Strokes
	{
		get
		{
			if (!_ink.TryGetValue(_page, out StrokeCollection value))
			{
				return _ink[_page] = new StrokeCollection();
			}
			return value;
		}
	}

	private BitmapSource? Visible
	{
		get
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			if (_full != null)
			{
				Int32Rect? crop = _crop;
				if (crop.HasValue)
				{
					Int32Rect valueOrDefault = crop.GetValueOrDefault();
					return new CroppedBitmap(_full, valueOrDefault);
				}
				return _full;
			}
			return null;
		}
	}

	public DesignerWindow(string source, JobRunner jobs, bool openCrop = false)
	{
		InitializeComponent();
		Loaded += (_, _) => UiText.Apply(this, App.Host.Settings.Language);
		_source = source;
		_jobs = jobs;
		_isPdf = Path.GetExtension(source).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
		SourceName.Text = Path.GetFileName(source);
		PagePanel.Visibility = ((!_isPdf) ? Visibility.Collapsed : Visibility.Visible);
		Border border = new Border
		{
			Padding = new Thickness(16.0),
			CornerRadius = new CornerRadius(14.0),
			BorderThickness = new Thickness(1.0),
			Margin = new Thickness(14.0),
			Child = _picker
		};
		border.SetResourceReference(Border.BackgroundProperty, "PopupSurface");
		border.SetResourceReference(Border.BorderBrushProperty, "PopupBorder");
		border.Effect = new DropShadowEffect
		{
			BlurRadius = 24.0,
			ShadowDepth = 6.0,
			Direction = 270.0,
			Opacity = 0.22,
			Color = Color.FromRgb(8, 20, 15)
		};
		_pickerPopup = new Popup
		{
			Child = border,
			StaysOpen = false,
			AllowsTransparency = true,
			Placement = PlacementMode.Bottom,
			PopupAnimation = PopupAnimation.Fade
		};
		_picker.ColorChanged += OnPicked;
		(string, double?)[] sizes = Sizes;
		for (int i = 0; i < sizes.Length; i++)
		{
			(string, double?) tuple = sizes[i];
			string item = tuple.Item1;
			double? ratio = tuple.Item2;
			RadioButton radioButton = new RadioButton
			{
				Style = (Style)FindResource("Chip"),
				Content = item,
				GroupName = "ratio",
				IsChecked = (ratio == _ratio)
			};
			radioButton.Checked += (object _, RoutedEventArgs _) =>
			{
				_ratio = ratio;
				Refresh();
			};
			Ratios.Children.Add(radioButton);
		}
		Cropper.Committed += ApplyCrop;
		KeyDown += (object _, KeyEventArgs e) =>
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Invalid comparison between Unknown and I4
			if ((int)e.Key == 13 && _cropping)
			{
				ExitCrop();
			}
		};
		Loaded += async (object _, RoutedEventArgs _) =>
		{
            await LoadSourceAsync();
            if (openCrop && _full != null)
                Crop_Click(this, new RoutedEventArgs());
        };
	}

	private static Color C(Rgb c)
	{
		return Color.FromRgb(c.R, c.G, c.B);
	}

	private static Brush BrushFor(BackgroundSpec spec)
	{
		Rgb? to = spec.To;
		object obj;
		if (to.HasValue)
		{
			Rgb valueOrDefault = to.GetValueOrDefault();
			obj = new LinearGradientBrush(C(spec.From), C(valueOrDefault), spec.Angle);
		}
		else
		{
			obj = new SolidColorBrush(C(spec.From));
		}
		((Freezable)obj).Freeze();
		return (Brush)obj;
	}

	private async Task LoadSourceAsync()
	{
		_ = 1;
		try
		{
			if (_isPdf)
			{
				_pages = await Previews.PdfPageCount(_source);
				_full = Decode(await Previews.RenderPdfPage(_source, _page, 1800));
				PageText.Text = UiText.IsArabic ? $"{_page + 1} من {_pages}" : $"{_page + 1} of {_pages}";
			}
			else
			{
				_full = Previews.LoadImage(_source);
			}
			_crop = null;
			ResetCropButton.Visibility = Visibility.Collapsed;
			UpdateDocumentColours();
			Refresh();
		}
		catch (Exception ex)
		{
			HintText.Text = UiText.IsArabic ? ((ex is UserFacingException) ? ex.Message : "تعذ\u0651ر فتح الملف.") : "Unable to open or render the selected file.";
			Log.Write($"Designer load failed: {ex}");
		}
	}

	private static BitmapSource Decode(byte[] png)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = new MemoryStream(png);
		bitmapImage.EndInit();
		((Freezable)bitmapImage).Freeze();
		return bitmapImage;
	}

	private void UpdateDocumentColours()
	{
		BitmapSource visible = Visible;
		if (visible != null)
		{
			double num = Math.Min(1.0, 120.0 / (double)Math.Max(visible.PixelWidth, visible.PixelHeight));
			FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(new TransformedBitmap(visible, new ScaleTransform(num, num)), PixelFormats.Bgra32, null, 0.0);
			byte[] array = new byte[formatConvertedBitmap.PixelWidth * formatConvertedBitmap.PixelHeight * 4];
			formatConvertedBitmap.CopyPixels(array, formatConvertedBitmap.PixelWidth * 4, 0);
			_documentColours = DocumentColors.Extract(array);
			_picker.SetDocumentColors(_documentColours);
			IReadOnlyList<BackgroundSpec> readOnlyList = DocumentColors.Suggestions(_documentColours);
			if (!_chosenByUser)
			{
				_background = ((readOnlyList.Count > 1) ? readOnlyList[1] : readOnlyList[0]);
			}
			BuildSuggestions(readOnlyList);
			BuildPenColours();
			SyncBackgroundControls();
		}
	}

	private void BuildSuggestions(IReadOnlyList<BackgroundSpec> suggestions)
	{
		Swatches.Children.Clear();
		SuggestLabel.Text = UiText.IsArabic ? ((_documentColours.Count > 0) ? "مقترحة من ألوان الملف" : "مقترحة") : ((_documentColours.Count > 0) ? "Suggested from file colors" : "Suggested");
		foreach (BackgroundSpec spec in suggestions)
		{
			RadioButton radioButton = new RadioButton
			{
				Style = (Style)FindResource("Swatch"),
				Background = BrushFor(spec),
				GroupName = "bg",
				Tag = spec
			};
			Rgb? to = spec.To;
			string toolTip;
			if (to.HasValue)
			{
				Rgb valueOrDefault = to.GetValueOrDefault();
				toolTip = (UiText.IsArabic ? "تدر\u0651ج " : "Gradient ") + spec.From.Hex + " ← " + valueOrDefault.Hex;
			}
			else
			{
				toolTip = spec.From.Hex;
			}
			radioButton.ToolTip = toolTip;
			RadioButton radioButton2 = radioButton;
			AutomationProperties.SetName((DependencyObject)(object)radioButton2, spec.IsGradient ? (UiText.IsArabic ? "تدر\u0651ج مقترح" : "Suggested gradient") : ((UiText.IsArabic ? "لون مقترح " : "Suggested color ") + spec.From.Hex));
			radioButton2.Checked += (object _, RoutedEventArgs _) =>
			{
				if (!_syncing)
				{
					SetBackground(spec);
				}
			};
			Swatches.Children.Add(radioButton2);
		}
	}

	private void BuildPenColours()
	{
		PenColors.Children.Clear();
		List<Rgb> list = new List<Rgb>();
		foreach (Rgb c in _documentColours.Take(3).Concat(PenBasics))
		{
			if (list.All((Rgb p) => p.DistanceTo(c) > 20.0))
			{
				list.Add(c);
			}
		}
		foreach (Rgb colour in list)
		{
			RadioButton radioButton = new RadioButton
			{
				Style = (Style)FindResource("SmallSwatch"),
				Background = new SolidColorBrush(C(colour)),
				GroupName = "pen",
				Tag = colour,
				ToolTip = colour.Hex,
				IsChecked = (colour == _penColour)
			};
			AutomationProperties.SetName((DependencyObject)(object)radioButton, (UiText.IsArabic ? "لون القلم " : "Pen color ") + colour.Hex);
			radioButton.Checked += (object _, RoutedEventArgs _) =>
			{
				if (!_syncing)
				{
					SetPen(colour);
				}
			};
			PenColors.Children.Add(radioButton);
		}
		PenDot.Background = new SolidColorBrush(C(_penColour));
	}

	private void SetBackground(BackgroundSpec spec)
	{
		_background = spec;
		_chosenByUser = true;
		SyncBackgroundControls();
		if (_previewRoot != null)
		{
			_previewRoot.Background = BrushFor(spec);
		}
	}

	private void SyncBackgroundControls()
	{
		_syncing = true;
		bool isGradient = _background.IsGradient;
		SolidMode.IsChecked = !isGradient;
		GradientMode.IsChecked = isGradient;
		SolidRow.Visibility = (isGradient ? Visibility.Collapsed : Visibility.Visible);
		GradientRow.Visibility = ((!isGradient) ? Visibility.Collapsed : Visibility.Visible);
		Border solidDot = SolidDot;
		Brush background = (FromDot.Background = new SolidColorBrush(C(_background.From)));
		solidDot.Background = background;
		ToDot.Background = new SolidColorBrush(C(_background.To ?? _background.From));
		AngleSlider.Value = _background.Angle;
		foreach (RadioButton item in Swatches.Children.OfType<RadioButton>())
		{
			item.IsChecked = object.Equals(item.Tag, _background);
		}
		_syncing = false;
	}

	private void BackgroundMode_Checked(object sender, RoutedEventArgs e)
	{
		if (!_syncing && IsLoaded)
		{
			if (GradientMode.IsChecked == true && !_background.IsGradient)
			{
				SetBackground(new BackgroundSpec(_background.From, _background.From.Tint(0.6), AngleSlider.Value));
			}
			else if (SolidMode.IsChecked == true && _background.IsGradient)
			{
				SetBackground(new BackgroundSpec(_background.From));
			}
		}
	}

	private void Angle_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_syncing && IsLoaded && _background.IsGradient)
		{
			SetBackground(_background with
			{
				Angle = Math.Round(AngleSlider.Value)
			});
		}
	}

	private void SwapGradient_Click(object sender, RoutedEventArgs e)
	{
		Rgb? to = _background.To;
		if (to.HasValue)
		{
			Rgb valueOrDefault = to.GetValueOrDefault();
			SetBackground(_background with
			{
				From = valueOrDefault,
				To = _background.From
			});
		}
	}

	private void PickSolid_Click(object sender, RoutedEventArgs e)
	{
		OpenPicker(PickTarget.Solid, SolidButton, _background.From);
	}

	private void PickFrom_Click(object sender, RoutedEventArgs e)
	{
		OpenPicker(PickTarget.From, FromButton, _background.From);
	}

	private void PickTo_Click(object sender, RoutedEventArgs e)
	{
		OpenPicker(PickTarget.To, ToButton, _background.To ?? _background.From);
	}

	private void PickPen_Click(object sender, RoutedEventArgs e)
	{
		OpenPicker(PickTarget.Pen, PenButton, _penColour);
	}

	private void OpenPicker(PickTarget target, UIElement anchor, Rgb current)
	{
		_pickTarget = target;
		_picker.SetColor(current);
		_pickerPopup.PlacementTarget = anchor;
		_pickerPopup.IsOpen = true;
	}

	private void OnPicked(Rgb colour)
	{
		switch (_pickTarget)
		{
		case PickTarget.Solid:
			SetBackground(new BackgroundSpec(colour));
			break;
		case PickTarget.From:
			SetBackground(_background with
			{
				From = colour
			});
			break;
		case PickTarget.To:
			SetBackground(_background with
			{
				To = colour
			});
			break;
		case PickTarget.Pen:
			SetPen(colour);
			break;
		}
	}

	private void SetPen(Rgb colour)
	{
		_penColour = colour;
		PenDot.Background = new SolidColorBrush(C(colour));
		_syncing = true;
		foreach (RadioButton item in PenColors.Children.OfType<RadioButton>())
		{
			item.IsChecked = object.Equals(item.Tag, colour);
		}
		_syncing = false;
		ApplyInkMode();
	}

	private DrawingAttributes PenAttributes()
	{
		double num = ((_full == null) ? 1000 : Math.Max(_full.PixelWidth, _full.PixelHeight));
		double num2 = ThicknessSlider.Value * num;
		bool valueOrDefault = MarkerTool.IsChecked == true;
		return new DrawingAttributes
		{
			Color = C(_penColour),
			Width = (valueOrDefault ? (num2 * 3.0) : num2),
			Height = (valueOrDefault ? (num2 * 3.0) : num2),
			IsHighlighter = valueOrDefault,
			StylusTip = ((!valueOrDefault) ? StylusTip.Ellipse : StylusTip.Rectangle),
			FitToCurve = true
		};
	}

	private void ApplyInkMode()
	{
		if (_previewInk != null)
		{
			bool flag = DrawSwitch.IsChecked == true && !_cropping;
			_previewInk.IsHitTestVisible = flag;
			_previewInk.EditingMode = (flag ? ((EraserTool.IsChecked != true) ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.EraseByStroke) : InkCanvasEditingMode.None);
			_previewInk.DefaultDrawingAttributes = PenAttributes();
		}
	}

	private void Draw_Changed(object sender, RoutedEventArgs e)
	{
		bool valueOrDefault = DrawSwitch.IsChecked == true;
		DrawPanel.Visibility = ((!valueOrDefault) ? Visibility.Collapsed : Visibility.Visible);
		ApplyInkMode();
		UpdateHint();
	}

	private void Tool_Checked(object sender, RoutedEventArgs e)
	{
		ApplyInkMode();
	}

	private void Thickness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ApplyInkMode();
	}

	private void UndoInk_Click(object sender, RoutedEventArgs e)
	{
		if (Strokes.Count > 0)
		{
			Strokes.RemoveAt(Strokes.Count - 1);
		}
	}

	private void ClearInk_Click(object sender, RoutedEventArgs e)
	{
		Strokes.Clear();
	}

	private void Refresh()
	{
		if (IsLoaded)
		{
			BitmapSource visible = Visible;
			if (visible != null)
			{
				PreviewBox.Child = Compose(visible, 1080.0, interactive: true);
				UpdateHint();
			}
		}
	}

	private void UpdateHint()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		BitmapSource visible = Visible;
		if (visible != null)
		{
			Size val = CanvasSize(visible, 2160.0);
			HintText.Text = UiText.IsArabic
                ? ((DrawSwitch.IsChecked == true) ? "ارسم على الصورة مباشرة · سي\u064fحفظ الرسم مع التصميم" : $"سي\u064fحفظ بمقاس {val.Width:0}×{val.Height:0} بكسل")
                : ((DrawSwitch.IsChecked == true) ? "Draw directly on the image; strokes are saved with your design" : $"Will save at {val.Width:0} × {val.Height:0} pixels");
		}
	}

	private Size CanvasSize(BitmapSource visible, double longSide)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		double num = _ratio ?? ((double)visible.PixelWidth / (double)visible.PixelHeight);
		if (!(num >= 1.0))
		{
			return new Size(Math.Round(longSide * num), longSide);
		}
		return new Size(longSide, Math.Round(longSide / num));
	}

	private FrameworkElement Compose(BitmapSource visible, double longSide, bool interactive)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		Size val = CanvasSize(visible, longSide);
		double num = PaddingSlider.Value * Math.Min(val.Width, val.Height);
		double num2 = Math.Min((val.Width - 2.0 * num) / (double)visible.PixelWidth, (val.Height - 2.0 * num) / (double)visible.PixelHeight);
		double num3 = (double)visible.PixelWidth * num2;
		double num4 = (double)visible.PixelHeight * num2;
		double num5 = RadiusSlider.Value * Math.Min(val.Width, val.Height);
		Grid grid = new Grid
		{
			Width = val.Width,
			Height = val.Height,
			Background = BrushFor(_background)
		};
		if (ShadowSwitch.IsChecked == true)
		{
			grid.Children.Add(new Border
			{
				Width = num3,
				Height = num4,
				CornerRadius = new CornerRadius(num5),
				Background = Brushes.Black,
				Effect = new DropShadowEffect
				{
					BlurRadius = longSide * 0.045,
					ShadowDepth = longSide * 0.012,
					Direction = 270.0,
					Opacity = 0.28
				}
			});
		}
		Grid grid2 = new Grid
		{
			Width = num3,
			Height = num4,
			Clip = new RectangleGeometry(new Rect(0.0, 0.0, num3, num4), num5, num5)
		};
		Image image = new Image
		{
			Source = visible,
			Width = num3,
			Height = num4,
			Stretch = Stretch.Fill
		};
		RenderOptions.SetBitmapScalingMode((DependencyObject)(object)image, BitmapScalingMode.HighQuality);
		grid2.Children.Add(image);
		Int32Rect val2 = _crop ?? new Int32Rect(0, 0, _full.PixelWidth, _full.PixelHeight);
		InkCanvas inkCanvas = new InkCanvas
		{
			Strokes = Strokes,
			Background = Brushes.Transparent,
			Width = _full.PixelWidth,
			Height = _full.PixelHeight,
			EditingMode = InkCanvasEditingMode.None,
			IsHitTestVisible = false,
			RenderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)new TranslateTransform(-val2.X, -val2.Y),
					(Transform)new ScaleTransform(num2, num2)
				}
			}
		};
		Canvas canvas = new Canvas
		{
			Width = num3,
			Height = num4
		};
		canvas.Children.Add(inkCanvas);
		grid2.Children.Add(canvas);
		grid.Children.Add(grid2);
		if (interactive)
		{
			_previewRoot = grid;
			_previewInk = inkCanvas;
			ApplyInkMode();
		}
		return grid;
	}

	private BitmapSource Export()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		FrameworkElement frameworkElement = Compose(Visible, 2160.0, interactive: false);
		frameworkElement.Measure(new Size(frameworkElement.Width, frameworkElement.Height));
		frameworkElement.Arrange(new Rect(0.0, 0.0, frameworkElement.Width, frameworkElement.Height));
		frameworkElement.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)frameworkElement.Width, (int)frameworkElement.Height, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(frameworkElement);
		((Freezable)renderTargetBitmap).Freeze();
		return renderTargetBitmap;
	}

	private void Option_Changed(object sender, RoutedEventArgs e)
	{
		Refresh();
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (Visible == null)
		{
			return;
		}
		BitmapSource image = Export();
		string text = OutputPaths.NextTo(_source, ".png", "تصميم");
		OutputPaths.WriteAtomically(text, (string temp) =>
		{
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder
			{
				Frames = { BitmapFrame.Create(image) }
			};
			using FileStream stream = File.Create(temp);
			pngBitmapEncoder.Save(stream);
		});
		_jobs.Track(SliceCatalog.Action("tool.background"), new _003C_003Ez__ReadOnlySingleElementList<string>(_source)).Complete(new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>(text), null, (UiText.IsArabic ? $"حفظنا التصميم بمقاس {image.PixelWidth}×{image.PixelHeight}" : $"Saved design at {image.PixelWidth} × {image.PixelHeight} pixels")));
		Close();
	}

	private void Copy_Click(object sender, RoutedEventArgs e)
	{
		if (Visible != null)
		{
			Clipboard.SetImage(Export());
			_jobs.Track(SliceCatalog.Action("tool.background"), new _003C_003Ez__ReadOnlySingleElementList<string>(_source)).Complete(new JobResult(Array.Empty<string>(), null, "التصميم في الحافظة، الصقه في أي برنامج"), succeededWithoutFiles: true);
		}
	}

	private async void NextPage_Click(object sender, RoutedEventArgs e)
	{
		if (_page + 1 < _pages)
		{
			_page++;
			await LoadSourceAsync();
		}
	}

	private async void PrevPage_Click(object sender, RoutedEventArgs e)
	{
		if (_page != 0)
		{
			_page--;
			await LoadSourceAsync();
		}
	}

	private void Crop_Click(object sender, RoutedEventArgs e)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		if (_full != null)
		{
			_cropping = true;
			Cropper.Source = _full;
			CropBox cropper = Cropper;
			Int32Rect? crop = _crop;
			Rect crop2;
			if (crop.HasValue)
			{
				Int32Rect valueOrDefault = crop.GetValueOrDefault();
				crop2 = new Rect((double)valueOrDefault.X, (double)valueOrDefault.Y, (double)valueOrDefault.Width, (double)valueOrDefault.Height);
			}
			else
			{
				crop2 = new Rect(0.0, 0.0, (double)_full.PixelWidth, (double)_full.PixelHeight);
			}
			cropper.Crop = crop2;
			Cropper.Visibility = Visibility.Visible;
			PreviewBox.Visibility = Visibility.Collapsed;
			CropBar.Visibility = Visibility.Visible;
			HintText.Visibility = Visibility.Collapsed;
			ApplyInkMode();
			Cropper.Focus();
		}
	}

	private void ExitCrop()
	{
		_cropping = false;
		Cropper.Visibility = Visibility.Collapsed;
		PreviewBox.Visibility = Visibility.Visible;
		CropBar.Visibility = Visibility.Collapsed;
		HintText.Visibility = Visibility.Visible;
		Button resetCropButton = ResetCropButton;
		Int32Rect? crop = _crop;
		resetCropButton.Visibility = ((!crop.HasValue) ? Visibility.Collapsed : Visibility.Visible);
		TextBlock cropLabel = CropLabel;
		crop = _crop;
		cropLabel.Text = ((!crop.HasValue) ? "قص\u0651" : "تعديل القص");
		ApplyInkMode();
	}

	private void ApplyCrop()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		if (_full != null)
		{
			Rect crop = Cropper.Crop;
			Int32Rect value = new Int32Rect((int)Math.Round(crop.X), (int)Math.Round(crop.Y), (int)Math.Round(crop.Width), (int)Math.Round(crop.Height));
			value.Width = Math.Min(value.Width, _full.PixelWidth - value.X);
			value.Height = Math.Min(value.Height, _full.PixelHeight - value.Y);
			bool flag = (double)value.Width * (double)value.Height >= 0.995 * (double)_full.PixelWidth * (double)_full.PixelHeight;
			_crop = ((flag || value.Width < 8 || value.Height < 8) ? ((Int32Rect?)null) : new Int32Rect?(value));
			ExitCrop();
			UpdateDocumentColours();
			Refresh();
		}
	}

	private void CropDone_Click(object sender, RoutedEventArgs e)
	{
		ApplyCrop();
	}

	private void CropCancel_Click(object sender, RoutedEventArgs e)
	{
		ExitCrop();
	}

	private void CropFull_Click(object sender, RoutedEventArgs e)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (_full != null)
		{
			Cropper.Crop = new Rect(0.0, 0.0, (double)_full.PixelWidth, (double)_full.PixelHeight);
		}
	}

	private void ResetCrop_Click(object sender, RoutedEventArgs e)
	{
		_crop = null;
		ResetCropButton.Visibility = Visibility.Collapsed;
		CropLabel.Text = "قص\u0651";
		UpdateDocumentColours();
		Refresh();
	}

	internal void ShowDrawingSample()
	{
		if (_full != null)
		{
			double num = _full.PixelWidth;
			double num2 = _full.PixelHeight;
			StylusPointCollection stylusPointCollection = new StylusPointCollection();
			for (int i = 0; i <= 40; i++)
			{
				double num3 = (double)i / 40.0;
				stylusPointCollection.Add(new StylusPoint(num * (0.2 + 0.6 * num3), num2 * (0.62 + 0.12 * Math.Sin(num3 * Math.PI * 2.0))));
			}
			Strokes.Add(new Stroke(stylusPointCollection, PenAttributes()));
			DrawSwitch.IsChecked = true;
		}
	}

	internal void ShowCropSample()
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		Crop_Click(this, new RoutedEventArgs());
		if (_full != null)
		{
			Cropper.Crop = new Rect((double)_full.PixelWidth * 0.12, (double)_full.PixelHeight * 0.18, (double)_full.PixelWidth * 0.62, (double)_full.PixelHeight * 0.55);
		}
	}

	internal BitmapSource ExportForReview()
	{
		return Export();
	}
}
