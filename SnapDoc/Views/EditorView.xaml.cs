#nullable disable
using SnapDoc.Services;
using System.Text.Json;
using SnapDoc.Resources.Languages;

#if ANDROID
using Android.Webkit;
#elif IOS || MACCATALYST
using WebKit;
using Foundation;
#endif

namespace SnapDoc.Views;

public partial class EditorView : ContentPage, IQueryAttributable
{
    private string _filePath = null;
    private string _fileType = null;
    private string _stringTxt = null;
    private bool _isReadOnly = false;

#if ANDROID || WINDOWS || IOS || MACCATALYST
    private string _jsonString = string.Empty;
    private bool _editorReady = false;
#endif

    public EditorView()
    {
        InitializeComponent();

#if WINDOWS
        EditorWebView.HandlerChanged += OnWindowsHandlerChanged;
#elif ANDROID
        EditorWebView.HandlerChanged += OnAndroidHandlerChanged;
#elif IOS || MACCATALYST
        EditorWebView.HandlerChanged += OnIOSHandlerChanged;
#endif

        Loaded += OnLoaded;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("fileType", out var value0))
            _fileType = value0 as string;
        if (query.TryGetValue("file", out var value1))
            _filePath = value1 as string;
        if (query.TryGetValue("string", out var value2))
            _stringTxt = value2 as string;
        if (query.TryGetValue("fileMode", out object value3))
            if (value3 as string == "R")
                _isReadOnly = true;
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
                    
                    // Thema wechsel speichern
                    if (data.TryGetValue("theme", out var themeName))
                    {
                        SettingsService.Instance.EditorTheme = themeName;
                        SettingsService.Instance.SaveSettings();
                    }
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
        if (!string.IsNullOrEmpty(_stringTxt))
            _jsonString = _stringTxt;
        else if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            _jsonString = File.ReadAllText(_filePath);
        else
            _jsonString = "";

        EditorWebView.Source = new HtmlWebViewSource
        {
            Html = LoadHtmlFromFile(_isReadOnly)
        };
    }

    private static string LoadHtmlFromFile(bool isReadOnly = true)
    {
        var assembly = typeof(EditorView).Assembly;
        using var stream = assembly.GetManifestResourceStream("SnapDoc.Resources.Raw.editor.html")!;
        using var reader = new StreamReader(stream);
        string htmlContent = reader.ReadToEnd();
        htmlContent = htmlContent.Replace("#999999", ((Color)Application.Current.Resources["Primary"]).ToRgbaHex());
        htmlContent = htmlContent.Replace("#888888", ((Color)Application.Current.Resources["PrimaryDarkText"]).ToRgbaHex());
        htmlContent = htmlContent.Replace("@Formatieren@", AppResources.formatieren);
        htmlContent = htmlContent.Replace("@Validieren@", AppResources.validieren);
        htmlContent = htmlContent.Replace("@Abbrechen@", AppResources.abbrechen);
        htmlContent = htmlContent.Replace("@Speichern@", AppResources.speichern);

        // Neues Flag ersetzen
        htmlContent = htmlContent.Replace("#IS_READ_ONLY", isReadOnly.ToString().ToLowerInvariant());

        // Thema setzen
        htmlContent = htmlContent.Replace("#THEME_REPLACE", SettingsService.Instance.EditorTheme);

        return htmlContent;
    }

    #region JSON Helpers
    private static readonly JsonSerializerOptions JsSafeOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string ToJsSafeJson(string json) => JsonSerializer.Serialize(json, JsSafeOptions);

    // Zentrale Logik für alle Plattformen
    private async void HandleWebMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(msg);
            if (data == null) return;

            if (data.ContainsKey("cancel"))
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            if (data.TryGetValue("json", out var json))
                await SaveJsonAsync(json);

            if (data.TryGetValue("theme", out var themeName))
            {
                SettingsService.Instance.EditorTheme = themeName;
                SettingsService.Instance.SaveSettings();
            }
        }
        catch { /* Log error if needed */ }
    }

    public async Task SetJsonAsync(string json)
    {
        _jsonString = json;
        if (!_editorReady) return;

#if ANDROID
        if (EditorWebView.Handler?.PlatformView is Android.Webkit.WebView webView)
            webView.EvaluateJavascript($"window.setJsonText({ToJsSafeJson(json)});", null);
#elif WINDOWS
        if (EditorWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webview2)
            await webview2.ExecuteScriptAsync($"window.setJsonText({ToJsSafeJson(json)});");
#elif IOS || MACCATALYST
        if (EditorWebView.Handler?.PlatformView is WKWebView wkWebView)
            await wkWebView.EvaluateJavaScriptAsync($"window.setJsonText({ToJsSafeJson(json)});");
#endif
    }
    public async Task SaveJsonAsync(string json)
    {
        File.WriteAllText(_filePath, json);
        await DisplayAlertAsync(Path.GetFileName(_filePath), "Einstellungen gespeichert!", "OK");
        await Shell.Current.GoToAsync($"..?fileType={_fileType}");
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_weakRef.TryGetTarget(out var view))
                    view.HandleWebMessage(message); // Nutzt jetzt die zentrale Methode!
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

    #region iOS Setup
#if IOS || MACCATALYST
    private void OnIOSHandlerChanged(object sender, EventArgs e)
    {
        if (EditorWebView.Handler?.PlatformView is WKWebView wkWebView)
        {
            // Erlaubt JavaScript-Kommunikation
            wkWebView.Configuration.UserContentController.RemoveAllScriptMessageHandlers();
            
            // Wir registrieren einen Handler namens "webBridge"
            // WKScriptMessageHandler ist ein Interface, das wir implementieren müssen
            var handler = new ScriptMessageHandler(this);
            wkWebView.Configuration.UserContentController.AddScriptMessageHandler(handler, "webBridge");

            // Überprüfung, ob das Dokument geladen ist
            wkWebView.NavigationDelegate = new NavigationDelegate(this);
        }
    }

    private class NavigationDelegate(EditorView view) : WKNavigationDelegate
    {
        public override async void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            view._editorReady = true;
            if (!string.IsNullOrEmpty(view._jsonString))
            {
                await view.SetJsonAsync(view._jsonString);
            }
        }
    }

    private class ScriptMessageHandler(EditorView view) : NSObject, IWKScriptMessageHandler
    {
        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            var msg = message.Body.ToString();
            // Hier rufen wir die Logik auf, die du bereits für Android/Windows hast
            view.HandleWebMessage(msg);
        }
    }
#endif
    #endregion
}
