using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Macro;

public partial class MainWindow : Window
{
    private readonly Views.FarmingPage _farmingPage;
    private readonly Views.DailyPage _dailyPage;
    private readonly Views.TimerPage _timerPage;

    public MainWindow()
    {
        InitializeComponent();
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
            new Uri("pack://application:,,,/Assets/app-v2.ico", UriKind.Absolute));
        _farmingPage = new Views.FarmingPage();
        _dailyPage = new Views.DailyPage();
        _timerPage = new Views.TimerPage();
        MainFrame.Navigate(_timerPage);
    }

    private void BtnFarming_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_farmingPage);
    private void BtnTimer_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_timerPage);
    private void BtnDaily_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_dailyPage);

    private void MainFrame_Navigated(object sender, NavigationEventArgs e)
    {
        DeactivateButton(BtnFarming);
        DeactivateButton(BtnTimer);
        DeactivateButton(BtnDaily);
        if (e.Content is Views.FarmingPage) ActivateButton(BtnFarming);
        else if (e.Content is Views.TimerPage) ActivateButton(BtnTimer);
        else if (e.Content is Views.DailyPage) ActivateButton(BtnDaily);
    }

    private static void ActivateButton(Button button)
    {
        button.Background = new SolidColorBrush(Color.FromRgb(0x49, 0x3E, 0x30));
        button.BorderThickness = new Thickness(3, 0, 0, 0);
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xC1, 0x8A));
    }

    private static void DeactivateButton(Button button)
    {
        button.Background = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.BorderBrush = Brushes.Transparent;
    }
}
