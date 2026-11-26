using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SnapDoc.Services.Cloud;

public class MsalAccessTokenProvider(IPublicClientApplication pca, string[] scopes) : IAccessTokenProvider
{
    private readonly IPublicClientApplication _pca = pca;
    private readonly string[] _scopes = scopes;

    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault() ?? throw new InvalidOperationException("Kein gespeichertes Konto verfuegbar.");
        try
        {
            // Versucht das Token im Hintergrund aus dem Cache zu laden oder stumm zu erneuern
            var result = await _pca.AcquireTokenSilent(_scopes, account)
                .ExecuteAsync(cancellationToken);

            return result.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            // Refresh-Token abgelaufen oder Widerruf -> Nutzer muss sich neu anmelden
            throw new InvalidOperationException("Sitzung abgelaufen. Bitte neu anmelden.", ex);
        }
    }
}