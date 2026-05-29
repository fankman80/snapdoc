#nullable disable
namespace SnapDoc.Controls;

public partial class CustomWebView : WebView
{
    public event EventHandler<string> JsMessageReceived;

    internal void OnJsMessageReceived(string message)
    {
        JsMessageReceived?.Invoke(this, message);
    }

    partial void ChangedHandler(object sender);
    partial void ChangingHandler(object sender, HandlerChangingEventArgs e);

    public CustomWebView()
    {
        this.HandlerChanged += (s, e) => ChangedHandler(s);
        this.HandlerChanging += (s, e) => ChangingHandler(s, e);
    }
}