#nullable disable
using System.Text.Json;

namespace SnapDoc.Views;

public partial class OneDrivePickerPage : ContentPage
{
    private readonly string accessToken;

    public OneDrivePickerPage(string graphAccessToken)
    {
        InitializeComponent();
        accessToken = graphAccessToken;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // 1. Wir bauen das HTML auf und setzen den Token DIREKT in den Code ein (Direct Injection)
        string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <script type='text/javascript' src='https://js.live.net/v7.2/OneDrive.js'></script>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background-color: #f3f2f1; }
        .loader { text-align: center; color: #0078D4; }
    </style>
</head>
<body>
    <div class='loader' id='statusUI'>
        <h2>OneDrive / SharePoint wird geladen...</h2>
        <p>Bitte warten.</p>
    </div>
    <script type='text/javascript'>
        var retryCount = 0;
        
        function startPicker() {
            // Der Token wird von C# direkt hier reingeschrieben
            var token = '___TOKEN_HERE___';

            // Warten, bis Microsofts Script geladen ist
            if (typeof OneDrive === 'undefined') {
                retryCount++;
                if (retryCount > 20) { // Nach ca. 10 Sekunden abbrechen
                    document.getElementById('statusUI').innerHTML = '<h3>Fehler</h3><p>Microsoft OneDrive-Skript konnte nicht aus dem Internet geladen werden.</p>';
                    return;
                }
                setTimeout(startPicker, 500);
                return;
            }

            document.getElementById('statusUI').innerHTML = '<h2>OneDrive wird geöffnet...</h2><p>Authentifizierung läuft.</p>';

            var odOptions = {
                clientId: '00fdac1d-aa0a-49c1-a238-a46a88f69ce6',
                action: 'share', 
                multiSelect: false,
                openInPopup: false,
                advanced: {
                    accessToken: token
                },
                success: function(files) {
                    var result = JSON.stringify(files);
                    window.location.href = 'maui-picker://result?data=' + encodeURIComponent(result);
                },
                cancel: function() {
                    var res = JSON.stringify({ status: 'cancelled' });
                    window.location.href = 'maui-picker://result?data=' + encodeURIComponent(res);
                },
                error: function(e) {
                    console.error(e);
                    document.getElementById('statusUI').innerHTML = '<h3>Fehler von Microsoft</h3><p>' + JSON.stringify(e) + '</p>';
                }
            };
            
            try {
                OneDrive.open(odOptions);
            } catch (err) {
                document.getElementById('statusUI').innerHTML = '<h3>Absturz beim Öffnen</h3><p>' + err.message + '</p>';
            }
        }

        // Automatisch starten, sobald die HTML-Seite vom WebView geladen wurde
        window.onload = startPicker;
    </script>
</body>
</html>";

        // Token im String ersetzen
        html = html.Replace("___TOKEN_HERE___", accessToken);

        // In Cache-Datei schreiben
        string filePath = Path.Combine(FileSystem.CacheDirectory, "picker.html");
        File.WriteAllText(filePath, html);

        // Ansicht laden
        string fileUri = $"file:///{filePath.Replace('\\', '/')}";
        PickerWebView.Source = fileUri;
    }

    // Navigated wird nicht mehr benötigt, da das JS über window.onload selbst startet
    // Wir fangen nur noch die Navigation nach draussen ab
    private async void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (e.Url != null && e.Url.StartsWith("maui-picker://result"))
        {
            e.Cancel = true;

            var uri = new Uri(e.Url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var jsonResult = query.Get("data");

            await HandlePickerResult(jsonResult);
        }
    }

    private async Task HandlePickerResult(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var status) && status.GetString() == "cancelled")
            {
                await MainThread.InvokeOnMainThreadAsync(async () => {
                    await Shell.Current.GoToAsync("..");
                });
                return;
            }

            if (root.TryGetProperty("value", out var files) && files.GetArrayLength() > 0)
            {
                var selectedFile = files[0];
                string fileName = selectedFile.GetProperty("name").GetString();

                await MainThread.InvokeOnMainThreadAsync(async () => {
                    await DisplayAlertAsync("Auswahl", $"Ordner/Datei gewählt: {fileName}", "OK");
                    await Shell.Current.GoToAsync("..");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Verarbeiten: {ex.Message}");
        }
    }
}