using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Falak.App;

// Consistent, localized modal dialogs for macro management. Uses the same
// theme brushes/button styles as the rest of Falak instead of native MessageBox.
internal sealed class MacroDialog : Window
{
    private MacroDialog(Window? parent, string title, string message,
        string? acceptCaption, bool destructive, bool arabic)
    {
        Title = title;
        Width = 470;
        MinWidth = 410;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        SetResourceReference(FontFamilyProperty, "UiFont");
        SetResourceReference(ForegroundProperty, "Ink");

        if (parent != null && parent.IsVisible && parent != this)
        {
            Owner = parent;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var shell = new Border
        {
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(24, 20, 24, 22),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.23,
                ShadowDepth = 5,
                BlurRadius = 24
            },
            Margin = new Thickness(16)
        };
        // The regular Surface brush is intentionally translucent in Glass mode
        // (alpha 156 on dark themes). A floating modal must instead use the
        // fully opaque PopupSurface so the owner's text cannot bleed through.
        // This preserves rounded corners and the shadow outside the solid card.
        shell.SetResourceReference(Border.BackgroundProperty, "PopupSurface");
        shell.SetResourceReference(Border.BorderBrushProperty, "PopupBorder");

        var content = new StackPanel();
        shell.Child = content;
        Content = shell;

        var heading = new DockPanel { LastChildFill = true };
        var close = new Button
        {
            Content = "×",
            FontSize = 19,
            Width = 30,
            Height = 30,
            ToolTip = arabic ? "إغلاق" : "Close",
            Style = Application.Current.TryFindResource("IconButton") as Style
        };
        close.Click += (_, _) => DialogResult = false;
        DockPanel.SetDock(close, Dock.Right);
        heading.Children.Add(close);

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        titleText.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        heading.Children.Add(titleText);
        content.Children.Add(heading);

        var line = new Border { Height = 1, Margin = new Thickness(0, 15, 0, 17) };
        line.SetResourceReference(Border.BackgroundProperty, "Line");
        content.Children.Add(line);

        var body = new TextBlock
        {
            Text = message,
            FontSize = 14,
            LineHeight = 25,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 22)
        };
        body.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        content.Children.Add(body);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        if (acceptCaption != null)
        {
            var confirm = new Button
            {
                Content = acceptCaption,
                Style = Application.Current.TryFindResource(destructive ? "SecondaryButton" : "PrimaryButton") as Style,
                MinWidth = 116,
                Height = 37,
                Margin = new Thickness(0, 0, 10, 0)
            };
            if (destructive)
            {
                confirm.SetResourceReference(Control.BackgroundProperty, "DangerSoft");
                confirm.SetResourceReference(Control.ForegroundProperty, "Danger");
                confirm.SetResourceReference(Control.BorderBrushProperty, "Danger");
            }
            confirm.Click += (_, _) => DialogResult = true;
            actions.Children.Add(confirm);

            var cancel = new Button
            {
                Content = arabic ? "إلغاء" : "Cancel",
                Style = Application.Current.TryFindResource("GhostButton") as Style,
                Height = 37,
                MinWidth = 86,
                IsCancel = true,
                IsDefault = true
            };
            cancel.Click += (_, _) => DialogResult = false;
            actions.Children.Add(cancel);
            Loaded += (_, _) => Keyboard.Focus(cancel);
        }
        else
        {
            var ok = new Button
            {
                Content = arabic ? "حسنًا" : "OK",
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                Width = 110,
                Height = 37,
                IsDefault = true,
                IsCancel = true
            };
            ok.Click += (_, _) => DialogResult = true;
            actions.Children.Add(ok);
            Loaded += (_, _) => Keyboard.Focus(ok);
        }
        content.Children.Add(actions);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        };
    }

    public static bool Confirm(Window? parent, string title, string message,
        string confirmCaption, bool danger = false)
    {
        var arabic = App.Host.Settings.Language != "en";
        return new MacroDialog(parent, title, message, confirmCaption, danger, arabic).ShowDialog() == true;
    }

    public static void Notice(Window? parent, string title, string message)
    {
        var arabic = App.Host.Settings.Language != "en";
        _ = new MacroDialog(parent, title, message, null, false, arabic).ShowDialog();
    }
}
