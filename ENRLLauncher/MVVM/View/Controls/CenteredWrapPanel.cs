using System.Windows;
using System.Windows.Controls;

namespace ENRLLauncher.MVVM.View.Controls;

public class CenteredWrapPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        Size curLineSize = new();
        Size panelSize = new();

        foreach (UIElement child in InternalChildren)
        {
            if (child == null || child.Visibility == Visibility.Collapsed)
                continue;

            child.Measure(availableSize);
            Size sz = child.DesiredSize;

            if (curLineSize.Width + sz.Width > availableSize.Width && curLineSize.Width > 0)
            {
                panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
                panelSize.Height += curLineSize.Height;
                curLineSize = sz;
            }
            else
            {
                curLineSize.Width += sz.Width;
                curLineSize.Height = Math.Max(curLineSize.Height, sz.Height);
            }
        }

        panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
        panelSize.Height += curLineSize.Height;

        return new Size(
            double.IsInfinity(availableSize.Width) ? panelSize.Width : availableSize.Width,
            panelSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int firstInLine = 0;
        Size curLineSize = new();
        double accumulatedHeight = 0;

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            UIElement child = InternalChildren[i];
            if (child == null || child.Visibility == Visibility.Collapsed)
                continue;

            Size sz = child.DesiredSize;

            if (curLineSize.Width + sz.Width > finalSize.Width && curLineSize.Width > 0)
            {
                ArrangeLine(accumulatedHeight, curLineSize, finalSize.Width, firstInLine, i);
                accumulatedHeight += curLineSize.Height;
                curLineSize = sz;
                firstInLine = i;
            }
            else
            {
                curLineSize.Width += sz.Width;
                curLineSize.Height = Math.Max(curLineSize.Height, sz.Height);
            }
        }

        if (firstInLine < InternalChildren.Count)
        {
            ArrangeLine(accumulatedHeight, curLineSize, finalSize.Width, firstInLine, InternalChildren.Count);
            accumulatedHeight += curLineSize.Height;
        }

        return finalSize;
    }

    private void ArrangeLine(double y, Size lineSize, double finalWidth, int start, int end)
    {
        double x = Math.Max(0, (finalWidth - lineSize.Width) / 2.0);

        for (int i = start; i < end; i++)
        {
            UIElement child = InternalChildren[i];
            if (child == null || child.Visibility == Visibility.Collapsed)
                continue;

            child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineSize.Height));
            x += child.DesiredSize.Width;
        }
    }
}