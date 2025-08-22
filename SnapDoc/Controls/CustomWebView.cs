#nullable disable

namespace SnapDoc.Controls;

public partial class CustomWebView : WebView
{
    // Event, auf das deine Pages hören können
    public event EventHandler<string> JsMessageReceived;

    // Wird von JsBridge aufgerufen (internal, damit nur innerhalb der Assembly sichtbar)
    internal void OnJsMessageReceived(string message)
    {
        JsMessageReceived?.Invoke(this, message);
    }

    // partial method declarations
    partial void ChangedHandler(object sender);
    partial void ChangingHandler(object sender, HandlerChangingEventArgs e);

    public CustomWebView()
    {
        this.HandlerChanged += (s, e) => ChangedHandler(s);
        this.HandlerChanging += (s, e) => ChangingHandler(s, e);
    }
}