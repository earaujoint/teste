using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Macro.Controls;

public sealed class ActionNotification : Border
{
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeights.SemiBold };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    public ActionNotification()
    {
        Child = _text;
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(18, 14, 18, 14);
        BorderThickness = new Thickness(1);
        MaxWidth = 440;
        Margin = new Thickness(24);
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        _timer.Tick += (_, _) => { _timer.Stop(); Visibility = Visibility.Collapsed; };
        Unloaded += (_, _) => { _timer.Stop(); Visibility = Visibility.Collapsed; };
    }

    public void Show(string message, bool pending = false, bool error = false)
    {
        _timer.Stop();
        var color = error ? "#F4A6A6" : pending ? "#E4C18A" : "#8EE3B2";
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(error ? "#452727" : pending ? "#393124" : "#203D2D"));
        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        _text.Foreground = BorderBrush;
        _text.Text = (error ? "!  " : pending ? "↕  " : "✓  ") + message;
        Visibility = Visibility.Visible;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        _timer.Start();
    }
}
