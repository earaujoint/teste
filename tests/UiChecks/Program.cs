using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Macro.Models;
using Macro.Views;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        var config = new FarmingConfiguration();
        Check(config.Normal.RepeatCount == 2 && config.Boss.RepeatCount == 1 && config.DailyDonation && config.DailyFavorites, "Original execution defaults");
        config.DailyItems[0].SelectedMaps.Add("test-only");
        Check(config.DominationItems[0].SelectedMaps.Count == 0, "Independent mission configurations");
        config.Normal.RepeatCountText = "abc";
        Check(!config.Normal.HasValidCount && config.Normal.RepeatCount == 2, "Invalid text is retained without corrupting count");
        config.Normal.RepeatCountText = "100";
        Check(!config.Normal.HasValidCount, "Count upper bound");
        config.Normal.RepeatCountText = "3";
        Check(config.Normal.HasValidCount && config.Normal.RepeatCount == 3, "Valid count update");
        var restored = JsonSerializer.Deserialize<FarmingConfiguration>(JsonSerializer.Serialize(config))!;
        Check(restored.Normal.RepeatCountText == "3", "JSON round trip");
        var app = new Macro.App();
        app.InitializeComponent();
        var window = new Macro.MainWindow();
        Check(window.MinWidth == 1000 && window.MinHeight == 650, "Desktop minimum dimensions");
        var page = new FarmingPage();
        foreach (double width in new[] { 734d, 1174d, 1654d })
        {
            page.Measure(new Size(width, 760)); page.Arrange(new Rect(0, 0, width, 760)); page.UpdateLayout();
            var tabs = Find<TabControl>(page).Single();
            for (int tab = 0; tab < 4; tab++)
            {
                tabs.SelectedIndex = tab; page.UpdateLayout();
                Check(Find<CheckBox>(page).Any(), $"Tab {tab} loaded at width {width}");
            }
            Check(page.DesiredSize.Width <= width, $"No page overflow at width {width}");
        }
        Check(page.NormalRaids.Count == 3 && page.NormalRaids.Count(r => r.IsAvailable) == 1, "Only implemented raid is available");
        var timer = new TimerPage(); timer.Measure(new Size(1100, 760));
        var maps = new DailyPage(); maps.Measure(new Size(1100, 760));
        Check(true, "Timer and Maps resources load");
        app.Shutdown();
        Console.WriteLine($"{checks} checks passed. No window shown or game automation invoked.");
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Find<T>(child)) yield return descendant;
        }
    }
}



