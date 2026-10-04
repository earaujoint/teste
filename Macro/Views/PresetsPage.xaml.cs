using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using Macro.Services;

namespace Macro.Views;

public partial class PresetsPage : Page
{
    private readonly FarmingPresetService _store = new();
    private Point _dragStart;
    private PresetStep? _dragStep;
    private FrameworkElement? _dragCard;
    private CardDragAdorner? _dragPreview;
    private sealed record StepDrag(FarmingPreset Preset, PresetStep Step);

    private static FrameworkElement? FindStep(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: PresetStep } row) return row;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void Steps_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragStep = FindStep(e.OriginalSource as DependencyObject)?.DataContext as PresetStep;
        _dragCard = FindCard(e.OriginalSource as DependencyObject);
    }

    private void Steps_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStep is null || sender is not ContentControl { Content: FarmingPreset preset } control) return;
        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var step = _dragStep;
        _dragStep = null;
        var card = _dragCard;
        var layer = AdornerLayer.GetAdornerLayer(this);
        var originalOpacity = card?.Opacity ?? 1;
        try
        {
            if (card is not null && layer is not null && card.ActualWidth > 0 && card.ActualHeight > 0)
            {
                var dpi = VisualTreeHelper.GetDpi(card);
                var snapshot = new RenderTargetBitmap((int)Math.Ceiling(card.ActualWidth * dpi.DpiScaleX),
                    (int)Math.Ceiling(card.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                snapshot.Render(card);
                snapshot.Freeze();
                _dragPreview = new CardDragAdorner(this, snapshot, new Size(card.ActualWidth, card.ActualHeight),
                    _dragStart - card.TranslatePoint(new Point(), this));
                layer.Add(_dragPreview);
                _dragPreview.Move(position, null);
                card.Opacity = 0.35;
            }
            DragDrop.DoDragDrop(control, new StepDrag(preset, step), DragDropEffects.Move);
        }
        finally
        {
            if (card is not null) card.Opacity = originalOpacity;
            if (_dragPreview is not null) layer?.Remove(_dragPreview);
            _dragPreview = null;
            _dragCard = null;
        }
    }

    private static FrameworkElement? FindCard(DependencyObject? element)
    {
        FrameworkElement? card = null;
        while (element is not null)
        {
            if (element is Border { DataContext: PresetStep } border) card = border;
            element = VisualTreeHelper.GetParent(element);
        }
        return card;
    }

    private void PreviewCardDrag(object sender, DragEventArgs e)
    {
        if (_dragPreview is null) return;
        Rect? destination = null;
        if (e.Data.GetData(typeof(StepDrag)) is StepDrag drag && FindCard(e.OriginalSource as DependencyObject) is { } card &&
            card.DataContext is PresetStep target && target.Title != drag.Step.Title)
        {
            DependencyObject? parent = card;
            while (parent is not null && parent is not ContentControl { Content: FarmingPreset }) parent = VisualTreeHelper.GetParent(parent);
            if (parent is ContentControl owner && ReferenceEquals(owner.Content, drag.Preset))
            {
                var steps = drag.Preset.Steps.Select(s => s.Title).ToList();
                var below = steps.IndexOf(drag.Step.Title) < steps.IndexOf(target.Title);
                var origin = card.TranslatePoint(new Point(0, below ? card.ActualHeight : 0), this);
                destination = new Rect(origin.X, origin.Y - 2, card.ActualWidth, 3);
            }
        }
        _dragPreview.Move(e.GetPosition(this), destination);
    }

    private sealed class CardDragAdorner : Adorner
    {
        private readonly ImageSource _image;
        private readonly Size _size;
        private readonly Vector _offset;
        private Point _position;
        private Rect? _destination;
        public CardDragAdorner(UIElement owner, ImageSource image, Size size, Vector offset) : base(owner)
        {
            _image = image;
            _size = size;
            _offset = offset;
            IsHitTestVisible = false;
        }
        public void Move(Point position, Rect? destination)
        {
            _position = position;
            _destination = destination;
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext drawingContext)
        {
            var accent = new SolidColorBrush(Color.FromRgb(228, 193, 138));
            if (_destination is Rect line) drawingContext.DrawRoundedRectangle(accent, null, line, 1.5, 1.5);
            var bounds = new Rect(_position - _offset + new Vector(10, 10), _size);
            drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), null,
                new Rect(bounds.X + 4, bounds.Y + 6, bounds.Width, bounds.Height), 8, 8);
            drawingContext.PushOpacity(0.90);
            drawingContext.DrawImage(_image, bounds);
            drawingContext.DrawRoundedRectangle(null, new Pen(accent, 1.5), bounds, 8, 8);
            drawingContext.Pop();
        }
    }

    private void Steps_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetData(typeof(StepDrag)) is StepDrag drag && sender is ContentControl control &&
            ReferenceEquals(control.Content, drag.Preset) && FindStep(e.OriginalSource as DependencyObject) is not null
            ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Steps_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(typeof(StepDrag)) is not StepDrag drag || sender is not ContentControl control ||
            !ReferenceEquals(control.Content, drag.Preset) || FindStep(e.OriginalSource as DependencyObject)?.DataContext is not PresetStep target || target.Title == drag.Step.Title) return;
        var order = drag.Preset.Steps.Select(step => step.Title).ToList();
        var from = order.IndexOf(drag.Step.Title);
        var to = order.IndexOf(target.Title);
        if (from < 0 || to < 0) return;
        order.RemoveAt(from);
        order.Insert(to, drag.Step.Title);
        drag.Preset.Configuration.ActionOrder = order.Concat(drag.Preset.Configuration.OrderedActions).Distinct().ToList();
        control.Content = null;
        control.Content = drag.Preset;
        Message.Text = $"Ordem de '{drag.Preset.Name}' alterada. Clique em Salvar ordem para aplicar.";
        drag.Preset.OrderStatus = "● Ordem alterada — ainda não salva";
        SaveNotification.Show($"Ordem de '{drag.Preset.Name}' alterada. Clique em Salvar ordem.", pending: true);
    }

    private void SaveOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FarmingPreset preset }) return;
        try
        {
            var presets = _store.Load();
            var saved = presets.FirstOrDefault(p => p.Name == preset.Name);
            if (saved is null) { Message.Text = "O preset foi excluído. Reabra a página para atualizar a lista."; return; }
            saved.Configuration.ActionOrder = preset.Configuration.OrderedActions.ToList();
            _store.Save(presets);
            Message.Text = $"Ordem de '{preset.Name}' salva.";
            preset.OrderStatus = "✓ Ordem salva";
            SaveNotification.Show($"Ordem de '{preset.Name}' salva com sucesso.");
        }
        catch (Exception ex)
        {
            Message.Text = $"Não foi possível salvar a ordem: {ex.Message}";
            SaveNotification.Show("Não foi possível salvar a ordem.", error: true);
        }
    }

    public PresetsPage()
    {
        InitializeComponent();
        AddHandler(DragDrop.PreviewDragOverEvent, new DragEventHandler(PreviewCardDrag), true);
        AddHandler(DragDrop.PreviewDragEnterEvent, new DragEventHandler(PreviewCardDrag), true);
        Loaded += (_, _) => RefreshPresets();
    }

    private void RefreshPresets()
    {
        try
        {
            var presets = _store.Load();
            PresetList.ItemsSource = presets;
            Message.Text = presets.Count == 0 ? "Nenhum preset salvo." : $"{presets.Count} preset(s) salvo(s).";
        }
        catch (Exception ex) { PresetList.ItemsSource = null; Message.Text = $"Não foi possível carregar: {ex.Message}"; }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FarmingPreset preset }) return;
        if (MessageBox.Show($"Excluir o preset '{preset.Name}'?", "Excluir preset", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var presets = _store.Load();
            presets.RemoveAll(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            _store.Save(presets);
            RefreshPresets();
        }
        catch (Exception ex) { Message.Text = $"Não foi possível excluir: {ex.Message}"; }
    }
}
