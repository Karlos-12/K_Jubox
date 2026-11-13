using System.Text.Json;

namespace K_Jubox.Spotify;

public sealed class SpotifySettings
{
    public string ClientId { get; } = GetRequiredEnvironmentVariable("SPOTIFY_CLIENT_ID");
    public string ClientSecret { get; } = GetRequiredEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
    public string RedirectUri { get; } = GetEnvironmentVariable("SPOTIFY_REDIRECT_URI", "http://localhost:3000/callback");
    public string TokenStoragePath { get; } = GetEnvironmentVariable("SPOTIFY_TOKEN_PATH", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".k_jubox", "spotify-tokens.json"));
    public string AuthorizationEndpoint { get; } = "https://accounts.spotify.com/authorize";
    public string TokenEndpoint { get; } = "https://accounts.spotify.com/api/token";
    public string ApiBaseUrl { get; } = "https://api.spotify.com/v1";

    public static string GetEnvironmentVariable(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) ?? fallback;

    public static string GetRequiredEnvironmentVariable(string name)
        => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Missing required environment variable: {name}");

    public static string GetRequiredJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new InvalidOperationException($"Expected '{propertyName}' to be a non-empty string.");
    }
}
