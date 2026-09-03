using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ENRLLauncher.MVVM.View.Adorners;

public class WireframeDragAdorner : Adorner
{
    private Point _location;
    private readonly Size _cardSize;
    private readonly Pen _dashedPen;
    private readonly Brush _fillBrush;

    public WireframeDragAdorner(UIElement adornedElement, Size cardSize) : base(adornedElement)
    {
        _cardSize = cardSize;
        IsHitTestVisible = false;
        ClipToBounds = false;

        var strokeColor = (Color)ColorConverter.ConvertFromString("#F97316");
        _dashedPen = new Pen(new SolidColorBrush(strokeColor), 3.5)
        {
            DashStyle = new DashStyle([6, 3], 0)
        };
        _dashedPen.Freeze();

        _fillBrush = new SolidColorBrush(Color.FromArgb(90, 249, 115, 22));
        _fillBrush.Freeze();
    }

    public void UpdatePosition(Point location)
    {
        _location = location;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(
            _location.X - (_cardSize.Width / 2),
            _location.Y - (_cardSize.Height / 2),
            _cardSize.Width,
            _cardSize.Height);

        dc.DrawRoundedRectangle(_fillBrush, _dashedPen, rect, 12, 12);
    }
}