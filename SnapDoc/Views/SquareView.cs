namespace SnapDoc.Views
{
    public partial class SquareView : ContentView
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            if (!double.IsInfinity(widthConstraint))
            {
                var size = widthConstraint;
                Content?.Measure(size, size);
                return new Size(size, size);
            }
            return base.MeasureOverride(widthConstraint, heightConstraint);
        }
    }
}