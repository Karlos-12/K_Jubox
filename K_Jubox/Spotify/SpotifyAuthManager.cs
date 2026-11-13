using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace K_Jubox.Spotify;

public sealed class SpotifyAuthManager
{
    private const string TokenFileName = "spotify-token-store.json";
    private readonly SpotifySettings _settings;
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<string, string> _pendingVerifiers = new();

    public SpotifyAuthManager(SpotifySettings settings)
    {
        _settings = settings;
        Directory.CreateDirectory(Path.GetDirectoryName(_settings.TokenStoragePath) ?? Directory.GetCurrentDirectory());
    }

    public string CreateAuthorizationUrl()
    {
        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = CreateCodeVerifier();
        _pendingVerifiers[state] = codeVerifier;

        var query = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _settings.RedirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = Base64UrlEncode(Sha256(codeVerifier)),
            ["state"] = state,
            ["scope"] = "user-read-email user-read-private user-read-playback-state user-modify-playback-state user-read-currently-playing user-read-recently-played user-read-playback-position app-remote-control streaming user-top-read user-library-read user-library-modify user-follow-read user-follow-modify playlist-modify-public playlist-modify-private playlist-read-private playlist-read-collaborative playlist-read-collaborative ugc-image-upload"
        };

        return BuildUri(_settings.AuthorizationEndpoint, query).ToString();
    }

    public async Task<SpotifyTokenSet> ExchangeCodeForTokensAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        if (!_pendingVerifiers.TryRemove(state, out var codeVerifier))
        {
            throw new InvalidOperationException("No pending PKCE state was found for the supplied authorization code. Start a fresh flow.");
        }

        using var http = new HttpClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", _settings.RedirectUri),
            new KeyValuePair<string, string>("client_id", _settings.ClientId),
            new KeyValuePair<string, string>("code_verifier", codeVerifier)
        });

        if (!string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", _settings.RedirectUri),
                new KeyValuePair<string, string>("client_id", _settings.ClientId),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("client_secret", _settings.ClientSecret)
            });
        }

        var response = await http.PostAsync(_settings.TokenEndpoint, form, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Spotify token exchange failed: {response.StatusCode} {payload}");
        }

        var tokenSet = ParseTokenSet(payload);
        tokenSet.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(tokenSet.ExpiresIn);
        SaveToken(tokenSet);
        return tokenSet;
    }

    public async Task<SpotifyTokenSet> EnsureValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokenSet = LoadToken();
        if (tokenSet is null)
        {
            throw new InvalidOperationException("No Spotify tokens are currently stored. Run the OAuth flow first.");
        }

        if (DateTimeOffset.UtcNow.AddMinutes(1) < tokenSet.ExpiresAtUtc)
        {
            return tokenSet;
        }

        if (string.IsNullOrWhiteSpace(tokenSet.RefreshToken))
        {
            throw new InvalidOperationException("The Spotify refresh token is missing. Re-authorize the app.");
        }

        using var http = new HttpClient();
        var payload = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", tokenSet.RefreshToken),
            new KeyValuePair<string, string>("client_id", _settings.ClientId)
        });

        if (!string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            payload = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", tokenSet.RefreshToken),
                new KeyValuePair<string, string>("client_id", _settings.ClientId),
                new KeyValuePair<string, string>("client_secret", _settings.ClientSecret)
            });
        }

        var response = await http.PostAsync(_settings.TokenEndpoint, payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Spotify refresh failed: {response.StatusCode} {content}");
        }

        var refreshed = ParseTokenSet(content);
        refreshed.RefreshToken ??= tokenSet.RefreshToken;
        refreshed.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);
        SaveToken(refreshed);
        return refreshed;
    }

    public SpotifyTokenSet? LoadToken()
    {
        if (!File.Exists(_settings.TokenStoragePath))
        {
            return null;
        }

        var json = File.ReadAllText(_settings.TokenStoragePath);
        var root = JsonDocument.Parse(json).RootElement;
        var tokenSet = new SpotifyTokenSet
        {
            AccessToken = SpotifySettings.GetRequiredJsonString(root, "access_token"),
            TokenType = SpotifySettings.GetRequiredJsonString(root, "token_type"),
            Scope = root.TryGetProperty("scope", out var scope) ? scope.GetString() : string.Empty,
            RefreshToken = root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? string.Empty : string.Empty,
            ExpiresIn = root.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 0,
            ExpiresAtUtc = root.TryGetProperty("expires_at_utc", out var expiresAt) && DateTimeOffset.TryParse(expiresAt.GetString(), out var parsed) ? parsed : DateTimeOffset.UtcNow.AddSeconds(3600)
        };

        return tokenSet;
    }

    public void SaveToken(SpotifyTokenSet tokenSet)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_settings.TokenStoragePath) ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);

            var payload = new Dictionary<string, object>
            {
                ["access_token"] = tokenSet.AccessToken,
                ["token_type"] = tokenSet.TokenType,
                ["scope"] = tokenSet.Scope ?? string.Empty,
                ["refresh_token"] = tokenSet.RefreshToken ?? string.Empty,
                ["expires_in"] = tokenSet.ExpiresIn,
                ["expires_at_utc"] = tokenSet.ExpiresAtUtc.UtcDateTime.ToString("O")
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settings.TokenStoragePath, json);
        }
    }

    public void ClearToken()
    {
        lock (_sync)
        {
            if (File.Exists(_settings.TokenStoragePath))
            {
                File.Delete(_settings.TokenStoragePath);
            }
        }
    }

    public string GetCurrentAccessTokenOrThrow()
    {
        var token = LoadToken() ?? throw new InvalidOperationException("No Spotify access token is available. Authenticate first.");
        return token.AccessToken;
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static byte[] Sha256(string value)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static Uri BuildUri(string endpoint, IReadOnlyDictionary<string, string> queryValues)
    {
        var builder = new StringBuilder(endpoint);
        var isFirst = !endpoint.Contains('?');

        foreach (var pair in queryValues)
        {
            builder.Append(isFirst ? '?' : '&');
            builder.Append(Uri.EscapeDataString(pair.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(pair.Value));
            isFirst = false;
        }

        return new Uri(builder.ToString());
    }

    private static SpotifyTokenSet ParseTokenSet(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var tokenSet = new SpotifyTokenSet
        {
            AccessToken = SpotifySettings.GetRequiredJsonString(root, "access_token"),
            TokenType = SpotifySettings.GetRequiredJsonString(root, "token_type"),
            Scope = root.TryGetProperty("scope", out var scope) ? scope.GetString() : string.Empty,
            RefreshToken = root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : string.Empty,
            ExpiresIn = root.TryGetProperty("expires_in", out var expiry) ? expiry.GetInt32() : 0
        };
        return tokenSet;
    }

}

public sealed class SpotifyTokenSet
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
