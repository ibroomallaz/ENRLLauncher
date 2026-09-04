using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.View.Adorners;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher.MVVM.View;

public partial class HomeView : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragging;
    private Border? _draggedCard;
    private LaunchItem? _draggedItem;
    private Border? _hoveredTargetBorder;
    private AdornerLayer? _adornerLayer;
    private WireframeDragAdorner? _dragAdorner;

    public HomeView()
    {
        InitializeComponent();
    }

    // --- External Windows Explorer File Drop ---

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            DataContext is HomeViewModel vm)
        {
            foreach (var file in files)
            {
                if (File.Exists(file) || Directory.Exists(file))
                {
                    vm.AddDroppedFile(file);
                }
            }
        }
    }

    // --- Internal Card & Separator Drag Reordering ---

    private void HomeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel { IsEditMode: true }) return;

        // Prevent dragging when clicking buttons (e.g. Delete) or editing text in section headers
        if (e.OriginalSource is DependencyObject dep)
        {
            if (FindParent<Button>(dep) != null || FindParent<TextBox>(dep) != null)
                return;
        }

        var cardBorder = FindCardBorder(e.OriginalSource as DependencyObject);
        if (cardBorder?.DataContext is LaunchItem item)
        {
            _dragStartPoint = e.GetPosition(this);
            _draggedCard = cardBorder;
            _draggedItem = item;
        }
    }

    private void HomeView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedCard == null || _draggedItem == null)
            return;

        Point currentPos = e.GetPosition(this);

        if (!_isDragging)
        {
            Vector diff = _dragStartPoint - currentPos;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                _draggedCard.Opacity = 0.35;

                _adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (_adornerLayer != null)
                {
                    var adornerSize = new Size(_draggedCard.ActualWidth, _draggedCard.ActualHeight);
                    _dragAdorner = new WireframeDragAdorner(this, adornerSize);
                    _adornerLayer.Add(_dragAdorner);
                }

                CaptureMouse();
            }
        }

        if (_isDragging && _dragAdorner != null)
        {
            _dragAdorner.UpdatePosition(currentPos);

            var hoveredBorder = FindCardBorderUnderMouse(currentPos);
            if (hoveredBorder != null && hoveredBorder != _draggedCard)
            {
                if (_hoveredTargetBorder != hoveredBorder)
                {
                    ClearTargetHighlight();
                    SetTargetHighlight(hoveredBorder);
                }
            }
            else
            {
                ClearTargetHighlight();
            }
        }
    }

    private void HomeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && DataContext is HomeViewModel vm && _draggedItem != null)
        {
            if (_hoveredTargetBorder?.DataContext is LaunchItem targetItem)
            {
                int oldIdx = vm.Items.IndexOf(_draggedItem);
                int newIdx = vm.Items.IndexOf(targetItem);
                if (oldIdx != -1 && newIdx != -1 && oldIdx != newIdx)
                {
                    vm.Reorder(oldIdx, newIdx);
                }
            }
        }

        EndDrag();
    }

    private void EndDrag()
    {
        if (_isDragging)
        {
            ReleaseMouseCapture();
            if (_adornerLayer != null && _dragAdorner != null)
            {
                _adornerLayer.Remove(_dragAdorner);
            }
            _draggedCard?.Opacity = 1.0;
        }

        ClearTargetHighlight();
        _isDragging = false;
        _draggedCard = null;
        _draggedItem = null;
        _dragAdorner = null;
    }

    // --- Inline Title Edit Persistence ---

    private void SectionTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            _ = vm.SaveCurrentLayoutAsync();
        }
    }

    // --- Visual Tree & Hit Testing Helpers ---

    private Border? FindCardBorder(DependencyObject? element)
    {
        while (element != null && element != this)
        {
            if (element is Border b && b.Tag as string == "CardBorder")
                return b;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private Border? FindCardBorderUnderMouse(Point point)
    {
        HitTestResult hit = VisualTreeHelper.HitTest(this, point);
        return FindCardBorder(hit?.VisualHit);
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent) return typedParent;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void SetTargetHighlight(Border border)
    {
        _hoveredTargetBorder = border;
        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316"));
        border.BorderThickness = new Thickness(2.5);
        border.Effect = new DropShadowEffect
        {
            Color = (Color)ColorConverter.ConvertFromString("#EA580C"),
            BlurRadius = 14,
            ShadowDepth = 0,
            Opacity = 0.65
        };
    }

    private void ClearTargetHighlight()
    {
        if (_hoveredTargetBorder != null)
        {
            _hoveredTargetBorder.ClearValue(Border.BorderBrushProperty);
            _hoveredTargetBorder.ClearValue(Border.BorderThicknessProperty);
            _hoveredTargetBorder.ClearValue(UIElement.EffectProperty);
            _hoveredTargetBorder = null;
        }
    }
}