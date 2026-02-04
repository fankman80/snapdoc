namespace SnapDoc.Views
{
    public partial class SquareView : ContentView
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            if (!double.IsInfinity(widthConstraint))
                return new Size(widthConstraint, widthConstraint);

            return base.MeasureOverride(widthConstraint, heightConstraint);
        }
    }
}