#nullable disable
using Microsoft.Maui.Layouts;

namespace SnapDoc.Controls;

public partial class WrapLayout : Layout
{
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(
        nameof(Spacing), typeof(double), typeof(WrapLayout), 8.0,
        propertyChanged: (b, _, _) => ((WrapLayout)b).InvalidateMeasure());

    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(
        nameof(RowSpacing), typeof(double), typeof(WrapLayout), 8.0,
        propertyChanged: (b, _, _) => ((WrapLayout)b).InvalidateMeasure());

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override ILayoutManager CreateLayoutManager() => new WrapLayoutManager(this);

    private sealed class WrapLayoutManager(WrapLayout layout) : LayoutManager(layout)
    {
        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var p = layout.Padding;
            double available = widthConstraint - p.HorizontalThickness;
            double x = 0, y = 0, rowH = 0, maxW = 0;

            foreach (var child in layout)
            {
                if (child.Visibility == Visibility.Collapsed) continue;

                var size = child.Measure(double.PositiveInfinity, double.PositiveInfinity);
                if (x > 0 && x + size.Width > available)
                {
                    y += rowH + layout.RowSpacing;
                    x = 0;
                    rowH = 0;
                }
                maxW = Math.Max(maxW, x + size.Width);
                x += size.Width + layout.Spacing;
                rowH = Math.Max(rowH, size.Height);
            }

            double w = double.IsInfinity(widthConstraint) ? maxW + p.HorizontalThickness : widthConstraint;
            return new Size(w, y + rowH + p.VerticalThickness);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var p = layout.Padding;
            double available = bounds.Width - p.HorizontalThickness;
            double x = 0, y = 0, rowH = 0;

            foreach (var child in layout)
            {
                if (child.Visibility == Visibility.Collapsed) continue;

                var size = child.DesiredSize;
                if (x > 0 && x + size.Width > available)
                {
                    y += rowH + layout.RowSpacing;
                    x = 0;
                    rowH = 0;
                }
                child.Arrange(new Rect(bounds.X + p.Left + x, bounds.Y + p.Top + y, size.Width, size.Height));
                x += size.Width + layout.Spacing;
                rowH = Math.Max(rowH, size.Height);
            }

            return bounds.Size;
        }
    }
}