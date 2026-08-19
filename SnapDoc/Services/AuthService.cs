using Microsoft.Identity.Client;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SnapDoc.Services;

public class AuthService
{
    private const string ClientId = "00fdac1d-aa0a-49c1-a238-a46a88f69ce6";
    private const string Authority = "https://login.microsoftonline.com/common";
    private readonly string[] _scopes = ["User.Read", "Files.ReadWrite.All"];
    private readonly IPublicClientApplication _pca;

    public AuthService()
    {
        var builder = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(Authority);

#if WINDOWS
        builder = builder.WithRedirectUri("http://localhost");
#else
        builder = builder.WithRedirectUri($"msal{ClientId}://auth");
#endif

        _pca = builder.Build();
    }

    public async Task<(bool Success, string Message)> LoginAndFetchUserAsync()
    {
        try
        {
            var builder = _pca.AcquireTokenInteractive(_scopes);

#if ANDROID
            builder = builder.WithParentActivityOrWindow(Platform.CurrentActivity);
#elif WINDOWS
            // Das Windows-Hauptfenster als Aufhängungspunkt für den Browser-Dialog setzen
            var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window != null)
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                builder = builder.WithParentActivityOrWindow(handle);
            }
#endif
            AuthenticationResult authResult = await builder.ExecuteAsync();

            var tokenProvider = new TokenProvider(authResult.AccessToken);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
            var graphClient = new GraphServiceClient(authProvider);

            var me = await graphClient.Me.GetAsync();
            return (true, me?.DisplayName ?? "Unbekannter Nutzer");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

public class TokenProvider : IAccessTokenProvider
{
    private readonly string _accessToken;

    public TokenProvider(string accessToken)
    {
        _accessToken = accessToken;
    }

    public Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }

    public AllowedHostsValidator AllowedHostsValidator => new();
}