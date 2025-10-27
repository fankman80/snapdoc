#nullable disable
#if ANDROID
using Android.Webkit;
#endif
using SnapDoc.Services;
using System.Text.Json;

namespace SnapDoc.Views;

public partial class EditorView : ContentPage, IQueryAttributable
{
    private string _jsonString = string.Empty;
    private string _filePath;
    private bool _editorReady = false;

    public EditorView()
    {
        InitializeComponent();

#if WINDOWS
        EditorWebView.HandlerChanged += OnWindowsHandlerChanged;
#endif

#if ANDROID
        EditorWebView.HandlerChanged += OnAndroidHandlerChanged;
#endif

        Loaded += OnLoaded;
    }

    #region Handler Setup

#if WINDOWS
    private async void OnWindowsHandlerChanged(object sender, EventArgs e)
    {
        if (EditorWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webview2)
        {
            if (webview2.CoreWebView2 == null)
                await webview2.EnsureCoreWebView2Async();

            webview2.CoreWebView2.DOMContentLoaded += async (s2, e2) =>
            {
                _editorReady = true;
                if (!string.IsNullOrEmpty(_jsonString))
                    await SetJsonAsync(_jsonString);
            };

            webview2.WebMessageReceived += async (s2, e2) =>
            {
                var msg = e2.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(msg)) return;

                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(msg);
                    if (data == null) return;

                    // Abbrechen
                    if (data.ContainsKey("cancel"))
                    {
                        await Shell.Current.GoToAsync("..");
                        return;
                    }

                    // Speichern
                    if (data.TryGetValue("json", out var json))
                        await SaveJsonAsync(json);
                }
                catch { }
            };
        }
    }
#endif

#if ANDROID
    private void OnAndroidHandlerChanged(object sender, EventArgs e)
    {
        if (EditorWebView.Handler?.PlatformView is Android.Webkit.WebView nativeWebView)
        {
            nativeWebView.Settings.JavaScriptEnabled = true;
            nativeWebView.Settings.DomStorageEnabled = true;

            nativeWebView.AddJavascriptInterface(new JsBridge(this), "jsBridge");
            nativeWebView.SetWebViewClient(new CustomWebViewClient(this));
        }
    }
#endif

    #endregion

    private async void OnLoaded(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            _jsonString = File.ReadAllText(_filePath);

        EditorWebView.Source = new HtmlWebViewSource
        {
            Html = LoadHtmlFromFile()
        };
    }

    private static string LoadHtmlFromFile()
    {
        var assembly = typeof(MapView).Assembly;
        using var stream = assembly.GetManifestResourceStream("SnapDoc.Resources.Raw.editor.html")!;
        using var reader = new StreamReader(stream);
        string htmlContent = reader.ReadToEnd();
        htmlContent = htmlContent.Replace("#999999", ((Color)Application.Current.Resources["Primary"]).ToRgbaHex());
        htmlContent = htmlContent.Replace("#888888", ((Color)Application.Current.Resources["PrimaryDarkText"]).ToRgbaHex());

        return htmlContent;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("file", out var value))
            _filePath = value as string;
    }

    private static async Task<string> LoadHtmlAsync(string fileName)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    #region JSON Helpers

    private static readonly JsonSerializerOptions JsSafeOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string ToJsSafeJson(string json)
    {
        return JsonSerializer.Serialize(json, JsSafeOptions);
    }

    public async Task SetJsonAsync(string json)
    {
        _jsonString = json;

#if ANDROID
        if (EditorWebView.Handler?.PlatformView is Android.Webkit.WebView webView && _editorReady)
            webView.EvaluateJavascript($"window.setJsonText({ToJsSafeJson(json)});", null);
#elif WINDOWS
        if (EditorWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2 && _editorReady)
            await webView2.ExecuteScriptAsync($"window.setJsonText({EditorView.ToJsSafeJson(json)});");
#endif
    }

    public async Task SaveJsonAsync(string json)
    {
        File.WriteAllText(_filePath, json);
        await DisplayAlertAsync("Erfolg", "Einstellungen gespeichert!", "OK");
        SettingsService.Instance.LoadSettings();
        await Shell.Current.GoToAsync("..");
    }

    #endregion

#if ANDROID
    #region Android Bridge

    public class JsBridge(EditorView view) : Java.Lang.Object
    {
        private readonly WeakReference<EditorView> _weakRef = new(view);

        [JavascriptInterface]
        [Java.Interop.Export("invokeAction")]
        public void InvokeAction(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!_weakRef.TryGetTarget(out var view)) return;

                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                if (data == null) return;

                // Abbrechen
                if (data.ContainsKey("cancel"))
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                // Speichern
                if (data.TryGetValue("json", out var json))
                    await view.SaveJsonAsync(json);
            });
        }
    }

    public class CustomWebViewClient(EditorView editorView) : WebViewClient
    {
        private readonly EditorView _editorView = editorView;

        public override void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            base.OnPageFinished(view, url);

            view.EvaluateJavascript("function sendToCSharp(msg) { jsBridge.invokeAction(msg); }", null);
            _editorView._editorReady = true;

            if (!string.IsNullOrEmpty(_editorView._jsonString))
                view.EvaluateJavascript($"window.setJsonText({ToJsSafeJson(_editorView._jsonString)});", null);
        }
    }

    #endregion
#endif
}