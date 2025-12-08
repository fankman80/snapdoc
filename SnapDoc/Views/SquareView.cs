namespace SnapDoc.Views
{
    public partial class SquareView : ContentView
    {
        protected override async void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            await Task.Yield();
            HeightRequest = Width;
        }
    }
}
