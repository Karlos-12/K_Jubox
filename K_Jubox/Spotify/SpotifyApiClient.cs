using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace K_Jubox.Spotify;

public sealed class SpotifyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SpotifyAuthManager _authManager;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SpotifyApiClient(SpotifySettings settings, SpotifyAuthManager authManager)
    {
        _authManager = authManager;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/")
        };
    }

    public async Task<JsonElement> GetAsync(string path, Dictionary<string, string>? queryParameters = null, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Get, path, null, queryParameters, cancellationToken);

    public async Task<JsonElement> PostAsync(string path, object? body = null, Dictionary<string, string>? queryParameters = null, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Post, path, body, queryParameters, cancellationToken);

    public async Task<JsonElement> PutAsync(string path, object? body = null, Dictionary<string, string>? queryParameters = null, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Put, path, body, queryParameters, cancellationToken);

    public async Task<JsonElement> DeleteAsync(string path, object? body = null, Dictionary<string, string>? queryParameters = null, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Delete, path, body, queryParameters, cancellationToken);

    public async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body = null, Dictionary<string, string>? queryParameters = null, CancellationToken cancellationToken = default)
    {
        var token = await _authManager.EnsureValidAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(method, BuildUrl(path, queryParameters));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Spotify API request failed: {response.StatusCode}. {payload}");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return JsonDocument.Parse("{}") .RootElement.Clone();
        }

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.Clone();
    }

    private static string BuildUrl(string path, Dictionary<string, string>? queryParameters)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path may not be empty.", nameof(path));
        }

        var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : path.TrimStart('/');

        if (queryParameters is null || queryParameters.Count == 0)
        {
            return url;
        }

        var builder = new StringBuilder(url);
        var prefix = url.Contains('?') ? '&' : '?';
        foreach (var pair in queryParameters)
        {
            builder.Append(prefix + Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value));
            prefix = '&';
        }

        return builder.ToString();
    }
}
