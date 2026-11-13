using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace K_Jubox.Spotify;

public sealed class SpotifyTools
{
    private readonly SpotifyAuthManager _authManager;
    private readonly SpotifyApiClient _apiClient;

    public SpotifyTools(SpotifyAuthManager authManager, SpotifyApiClient apiClient)
    {
        _authManager = authManager;
        _apiClient = apiClient;
    }

    [McpServerTool]
    public string GetServerStatus()
        => "K_Jubox is running and ready to serve Spotify API requests over STDIO.";

    [McpServerTool]
    public Dictionary<string, string> BeginSpotifyAuthorization()
    {
        var url = _authManager.CreateAuthorizationUrl();
        return new Dictionary<string, string>
        {
            ["authorization_url"] = url,
            ["redirect_uri"] = Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI") ?? "http://localhost:3000/callback",
            ["message"] = "Open the authorization URL in a browser, approve access, and then call complete_spotify_authorization with the returned code and state."
        };
    }

    [McpServerTool]
    public Dictionary<string, object> CompleteSpotifyAuthorization(string code, string state)
    {
        var token = _authManager.ExchangeCodeForTokensAsync(code, state).GetAwaiter().GetResult();
        return new Dictionary<string, object>
        {
            ["status"] = "authorized",
            ["token_type"] = token.TokenType,
            ["expires_in"] = token.ExpiresIn,
            ["expires_at_utc"] = token.ExpiresAtUtc.UtcDateTime.ToString("O"),
            ["scope"] = token.Scope ?? string.Empty
        };
    }

    [McpServerTool]
    public Dictionary<string, object> GetSpotifyTokenStatus()
    {
        var token = _authManager.LoadToken();
        if (token is null)
        {
            return new Dictionary<string, object> { ["status"] = "not_configured" };
        }

        return new Dictionary<string, object>
        {
            ["status"] = DateTimeOffset.UtcNow.AddMinutes(1) < token.ExpiresAtUtc ? "valid" : "refresh_required",
            ["expires_at_utc"] = token.ExpiresAtUtc.UtcDateTime.ToString("O"),
            ["token_type"] = token.TokenType,
            ["scope"] = token.Scope ?? string.Empty
        };
    }

    [McpServerTool]
    public Dictionary<string, object> RefreshSpotifyToken()
    {
        var token = _authManager.EnsureValidAccessTokenAsync().GetAwaiter().GetResult();
        return new Dictionary<string, object>
        {
            ["status"] = "refreshed",
            ["expires_at_utc"] = token.ExpiresAtUtc.UtcDateTime.ToString("O")
        };
    }

    [McpServerTool]
    public JsonElement SearchSpotify(string query, string type, int limit = 20, string? market = null, string? includeExternal = null)
    {
        var q = new Dictionary<string, string>
        {
            ["q"] = query,
            ["type"] = type,
            ["limit"] = limit.ToString()
        };
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        if (!string.IsNullOrWhiteSpace(includeExternal)) q["include_external"] = includeExternal;
        return _apiClient.GetAsync("search", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetTrack(string id, string? market = null)
    {
        var q = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        return _apiClient.GetAsync($"tracks/{id}", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetSeveralTracks(string ids)
    {
        var payload = new Dictionary<string, string> { ["ids"] = ids };
        return _apiClient.GetAsync("tracks", payload).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetAlbum(string id, string? market = null)
    {
        var q = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        return _apiClient.GetAsync($"albums/{id}", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetArtist(string id)
        => _apiClient.GetAsync($"artists/{id}").GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetArtistTopTracks(string id, string market)
        => _apiClient.GetAsync($"artists/{id}/top-tracks", new Dictionary<string, string> { ["market"] = market }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetArtistAlbums(string id, string? includeGroups = null, int limit = 20, int offset = 0)
    {
        var q = new Dictionary<string, string> { ["limit"] = limit.ToString(), ["offset"] = offset.ToString() };
        if (!string.IsNullOrWhiteSpace(includeGroups)) q["include_groups"] = includeGroups;
        return _apiClient.GetAsync($"artists/{id}/albums", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetUserProfile(string userId)
        => _apiClient.GetAsync($"users/{userId}").GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetCurrentUserProfile()
        => _apiClient.GetAsync("me").GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetCurrentUserPlaylists(int limit = 20, int offset = 0)
        => _apiClient.GetAsync("me/playlists", new Dictionary<string, string> { ["limit"] = limit.ToString(), ["offset"] = offset.ToString() }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetPlaylist(string playlistId, string? fields = null, string? market = null)
    {
        var q = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(fields)) q["fields"] = fields;
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        return _apiClient.GetAsync($"playlists/{playlistId}", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetPlaylistTracks(string playlistId, int limit = 100, int offset = 0, string? market = null)
    {
        var q = new Dictionary<string, string> { ["limit"] = limit.ToString(), ["offset"] = offset.ToString() };
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        return _apiClient.GetAsync($"playlists/{playlistId}/tracks", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement CreatePlaylist(string name, string? description = null, bool isPublic = true)
    {
        var user = _apiClient.GetAsync("me").GetAwaiter().GetResult();
        var userId = user.GetProperty("id").GetString() ?? throw new InvalidOperationException("Spotify user ID not available.");
        var body = new { name, description, isPublic = isPublic };
        return _apiClient.PostAsync($"users/{userId}/playlists", body).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement AddTracksToPlaylist(string playlistId, string uris, int position = -1)
    {
        var body = new { uris = uris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), position = position };
        return _apiClient.PostAsync($"playlists/{playlistId}/tracks", body).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement RemoveTracksFromPlaylist(string playlistId, string trackUris)
    {
        var tracks = trackUris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => new { uri = u })
            .ToArray();
        return _apiClient.DeleteAsync($"playlists/{playlistId}/tracks", new { tracks }).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetSavedTracks(int limit = 20, int offset = 0, string? market = null)
    {
        var q = new Dictionary<string, string> { ["limit"] = limit.ToString(), ["offset"] = offset.ToString() };
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        return _apiClient.GetAsync("me/tracks", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement SaveTracks(string trackIds)
    {
        var ids = trackIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _apiClient.PutAsync("me/tracks", new { ids }).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement RemoveSavedTracks(string trackIds)
    {
        var ids = trackIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _apiClient.DeleteAsync("me/tracks", new { ids }).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetCurrentPlayback(string? market = null, string? additionalTypes = null)
    {
        var q = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(market)) q["market"] = market;
        if (!string.IsNullOrWhiteSpace(additionalTypes)) q["additional_types"] = additionalTypes;
        return _apiClient.GetAsync("me/player", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetAvailableDevices()
        => _apiClient.GetAsync("me/player/devices").GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement TransferPlayback(string deviceId, bool play = false)
        => _apiClient.PutAsync("me/player", new { device_ids = new[] { deviceId }, play }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement StartPlayback(string? deviceId = null, string? contextUri = null, string? uris = null, int? offset = null, int positionMs = 0)
    {
        var body = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(deviceId)) body["device_id"] = deviceId;
        if (!string.IsNullOrWhiteSpace(contextUri)) body["context_uri"] = contextUri;
        if (!string.IsNullOrWhiteSpace(uris)) body["uris"] = uris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (offset is not null) body["offset"] = new { position = offset.Value };
        if (positionMs > 0) body["position_ms"] = positionMs;
        return _apiClient.PutAsync("me/player/play", body, deviceId is null ? null : new Dictionary<string, string> { ["device_id"] = deviceId }).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement PausePlayback(string? deviceId = null)
        => _apiClient.PutAsync("me/player/pause", null, deviceId is null ? null : new Dictionary<string, string> { ["device_id"] = deviceId }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement ResumePlayback(string? deviceId = null)
        => _apiClient.PutAsync("me/player/play", null, deviceId is null ? null : new Dictionary<string, string> { ["device_id"] = deviceId }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement SkipToNext(string? deviceId = null)
        => _apiClient.PostAsync("me/player/next", null, deviceId is null ? null : new Dictionary<string, string> { ["device_id"] = deviceId }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement SkipToPrevious(string? deviceId = null)
        => _apiClient.PostAsync("me/player/previous", null, deviceId is null ? null : new Dictionary<string, string> { ["device_id"] = deviceId }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement SetRepeatMode(string state, string? deviceId = null)
        => _apiClient.PutAsync("me/player/repeat", new { state }, deviceId is null ? null : new Dictionary<string, string> { ["device_id"] = deviceId }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement SetPlaybackVolume(int volumePercent, string? deviceId = null)
        => _apiClient.PutAsync("me/player/volume", null, new Dictionary<string, string> { ["volume_percent"] = volumePercent.ToString(), ["device_id"] = deviceId ?? string.Empty }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetRecentlyPlayed(int limit = 20, string? after = null, string? before = null)
    {
        var q = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (!string.IsNullOrWhiteSpace(after)) q["after"] = after;
        if (!string.IsNullOrWhiteSpace(before)) q["before"] = before;
        return _apiClient.GetAsync("me/player/recently-played", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetRecommendations(string seedArtists, string seedGenres, string seedTracks, int limit = 20)
    {
        var q = new Dictionary<string, string>
        {
            ["seed_artists"] = seedArtists,
            ["seed_genres"] = seedGenres,
            ["seed_tracks"] = seedTracks,
            ["limit"] = limit.ToString()
        };
        return _apiClient.GetAsync("recommendations", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetMyTopArtists(string timeRange = "medium_term", int limit = 20)
        => _apiClient.GetAsync("me/top/artists", new Dictionary<string, string> { ["time_range"] = timeRange, ["limit"] = limit.ToString() }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetMyTopTracks(string timeRange = "medium_term", int limit = 20)
        => _apiClient.GetAsync("me/top/tracks", new Dictionary<string, string> { ["time_range"] = timeRange, ["limit"] = limit.ToString() }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement GetSavedAlbums(int limit = 20, int offset = 0)
        => _apiClient.GetAsync("me/albums", new Dictionary<string, string> { ["limit"] = limit.ToString(), ["offset"] = offset.ToString() }).GetAwaiter().GetResult();

    [McpServerTool]
    public JsonElement SaveAlbums(string albumIds)
    {
        var ids = albumIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _apiClient.PutAsync("me/albums", new { ids }).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement RemoveSavedAlbums(string albumIds)
    {
        var ids = albumIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _apiClient.DeleteAsync("me/albums", new { ids }).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetFeaturedPlaylists(string? locale = null, string? country = null, string? timestamp = null, int limit = 20)
    {
        var q = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (!string.IsNullOrWhiteSpace(locale)) q["locale"] = locale;
        if (!string.IsNullOrWhiteSpace(country)) q["country"] = country;
        if (!string.IsNullOrWhiteSpace(timestamp)) q["timestamp"] = timestamp;
        return _apiClient.GetAsync("browse/featured-playlists", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetNewReleases(string? country = null, int limit = 20)
    {
        var q = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (!string.IsNullOrWhiteSpace(country)) q["country"] = country;
        return _apiClient.GetAsync("browse/new-releases", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetCategory(string categoryId, string? country = null, string? locale = null)
    {
        var q = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(country)) q["country"] = country;
        if (!string.IsNullOrWhiteSpace(locale)) q["locale"] = locale;
        return _apiClient.GetAsync($"browse/categories/{categoryId}", q).GetAwaiter().GetResult();
    }

    [McpServerTool]
    public JsonElement GetGenericRequest(string method, string endpoint, string? body = null, string? query = null)
    {
        var httpMethod = new HttpMethod(method.ToUpperInvariant());
        var queryParameters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var section in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = section.Split('=', 2);
                queryParameters[Uri.UnescapeDataString(parts[0])] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }
        }

        object? requestBody = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            requestBody = JsonDocument.Parse(body).RootElement;
        }

        return httpMethod.Method switch
        {
            "GET" => _apiClient.GetAsync(endpoint, queryParameters).GetAwaiter().GetResult(),
            "POST" => _apiClient.PostAsync(endpoint, requestBody, queryParameters).GetAwaiter().GetResult(),
            "PUT" => _apiClient.PutAsync(endpoint, requestBody, queryParameters).GetAwaiter().GetResult(),
            "DELETE" => _apiClient.DeleteAsync(endpoint, requestBody, queryParameters).GetAwaiter().GetResult(),
            _ => throw new InvalidOperationException($"Unsupported HTTP method '{method}'. Supported methods: GET, POST, PUT, DELETE.")
        };
    }
}
