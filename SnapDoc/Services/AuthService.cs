using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using SnapDoc.Resources.Languages;

#if WINDOWS
using Microsoft.Identity.Client.Broker;
#endif

namespace SnapDoc.Services;

public class AuthService
{
    private const string ClientId = "00fdac1d-aa0a-49c1-a238-a46a88f69ce6";
    private const string Authority = "https://login.microsoftonline.com/common";

    private readonly string[] _scopes = ["User.Read", "Files.ReadWrite.All"];
    private readonly IPublicClientApplication _pca;

    public bool IsLoggedIn => GraphClient != null;
    public GraphServiceClient? GraphClient { get; private set; }

    public string CurrentUserName { get; private set; } = string.Empty;
    public string CurrentUserEmail { get; private set; } = string.Empty;

    public AuthService()
    {
        var builder = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(Authority);

#if WINDOWS
        builder = builder
            .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows));
#elif IOS
        builder = builder
            .WithRedirectUri($"msal{ClientId}://auth")
            .WithIosKeychainSecurityGroup("com.microsoft.adalcache");
#else
        builder = builder.WithRedirectUri($"msal{ClientId}://auth");
#endif

        _pca = builder.Build();
    }

    // ---------------------------------------------------------------
    // Stiller Login (beim App-Start)
    // ---------------------------------------------------------------

    /// <summary>
    /// Versucht, den Nutzer ohne Dialog aus dem MSAL-Cache anzumelden.
    /// Beim App-Start aufrufen, bevor ein interaktiver Login angeboten wird.
    /// </summary>
    public async Task<bool> TrySilentLoginAsync()
    {
        try
        {
            var accounts = await _pca.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
                return false;

            // Wirft MsalUiRequiredException, wenn ein Dialog noetig ist
            await _pca.AcquireTokenSilent(_scopes, account).ExecuteAsync();

            BuildGraphClient();
            await FetchUserInfoAsync();

            return true;
        }
        catch (MsalUiRequiredException)
        {
            // Token abgelaufen oder widerrufen - interaktiver Login noetig
            await ClearSessionAsync();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stiller Login fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------------
    // Interaktiver Login
    // ---------------------------------------------------------------

    public async Task<(bool Success, string userName, string userEmail)> LoginAndFetchUserAsync()
    {
        try
        {
            var builder = _pca.AcquireTokenInteractive(_scopes);

#if ANDROID
            builder = builder.WithParentActivityOrWindow(Platform.CurrentActivity);
#elif IOS
            builder = builder.WithParentActivityOrWindow(Platform.GetCurrentUIViewController());
#elif WINDOWS
            var windows = Application.Current?.Windows;
            if (windows != null && windows.Count > 0)
            {
                if (windows[0]?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window)
                {
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    builder = builder.WithParentActivityOrWindow(handle);
                }
            }
#endif

            await builder.ExecuteAsync();

            BuildGraphClient();
            await FetchUserInfoAsync();

            return (true, CurrentUserName, CurrentUserEmail);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == "authentication_canceled")
        {
            // Der User hat den Vorgang absichtlich abgebrochen
            return (false, nameof(DualPopupResult.Cancel), string.Empty);
        }
        catch (Exception ex)
        {
            // Echte Fehler weiterhin zurueckgeben
            return (false, ex.Message, ex.Message);
        }
    }

    // ---------------------------------------------------------------
    // Logout
    // ---------------------------------------------------------------

    /// <summary>
    /// Meldet den Nutzer vollstaendig ab und leert den MSAL-Token-Cache.
    /// Ohne diesen Aufruf bleiben Tokens auf dem Geraet gespeichert.
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            var accounts = await _pca.GetAccountsAsync();

            foreach (var account in accounts)
                await _pca.RemoveAsync(account);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Abmelden: {ex.Message}");
        }
        finally
        {
            await ClearSessionAsync();
        }
    }

    /// <summary>Setzt nur den lokalen Zustand zurueck (ohne Cache-Loeschung).</summary>
    private Task ClearSessionAsync()
    {
        GraphClient = null;
        CurrentUserName = string.Empty;
        CurrentUserEmail = string.Empty;
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // Sitzungspruefung
    // ---------------------------------------------------------------

    /// <summary>
    /// Prueft, ob die Sitzung noch wirklich gueltig ist (z.B. nach Passwortwechsel
    /// oder Token-Widerruf). Setzt den Zustand bei Bedarf zurueck.
    /// </summary>
    public async Task<bool> EnsureValidSessionAsync()
    {
        if (GraphClient == null)
            return false;

        try
        {
            var accounts = await _pca.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                await ClearSessionAsync();
                return false;
            }

            await _pca.AcquireTokenSilent(_scopes, account).ExecuteAsync();
            return true;
        }
        catch (MsalUiRequiredException)
        {
            await ClearSessionAsync();
            return false;
        }
        catch
        {
            // Netzwerkfehler: Sitzung nicht verwerfen, offline weiterarbeiten
            return true;
        }
    }

    // ---------------------------------------------------------------
    // Infrastruktur
    // ---------------------------------------------------------------

    private void BuildGraphClient()
    {
        // Provider nutzt den MSAL-Cache und erneuert Tokens selbstaendig
        var tokenProvider = new MsalAccessTokenProvider(_pca, _scopes);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);

        GraphClient = new GraphServiceClient(authProvider);
    }

    private async Task FetchUserInfoAsync()
    {
        if (GraphClient == null) return;

        var me = await GraphClient.Me.GetAsync();

        CurrentUserName = me?.DisplayName ?? AppResources.unbekannter_nutzer;
        CurrentUserEmail = me?.Mail ?? me?.UserPrincipalName ?? string.Empty;
    }
}
