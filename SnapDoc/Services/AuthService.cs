using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

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
#else
        builder = builder.WithRedirectUri($"msal{ClientId}://auth");
#endif

        _pca = builder.Build();
    }

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
                var window = windows[0]?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (window != null)
                {
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    builder = builder.WithParentActivityOrWindow(handle);
                }
            }
#endif

            AuthenticationResult authResult = await builder.ExecuteAsync();

            var tokenProvider = new TokenProvider(authResult.AccessToken);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);

            GraphClient = new GraphServiceClient(authProvider);

            // Abruf des Benutzerprofils (inklusive Mail und UserPrincipalName)
            var me = await GraphClient.Me.GetAsync();

            CurrentUserName = me?.DisplayName ?? "Unbekannter Nutzer";
            CurrentUserEmail = me?.Mail ?? me?.UserPrincipalName ?? string.Empty;

            return (true, CurrentUserName, CurrentUserEmail);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, ex.Message);
        }
    }
}

public class TokenProvider(string accessToken) : IAccessTokenProvider
{
    private readonly string _accessToken = accessToken;

    public Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }

    public AllowedHostsValidator AllowedHostsValidator => new();
}