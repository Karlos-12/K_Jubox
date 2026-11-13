# K_Jubox

K_Jubox is a lightweight, feature-rich Spotify MCP server written in C# / .NET 8. It exposes Spotify Web API functionality to MCP clients over STDIO so AI assistants can search Spotify, manage playlists, control playback, access user library data, and more.

## What it covers

- Search for tracks, albums, artists and playlists
- Fetch track, album, artist and user profile data
- Access playlists and playlist tracks
- Create and update playlists
- Manage saved tracks and albums
- Work with playback and devices
- Read recently played and listening-history endpoints
- Generate recommendations and top tracks/artists
- Generic Spotify API requests for endpoints not yet wrapped by dedicated tools
- OAuth 2.0 Authorization Code with PKCE and automatic refresh

## Requirements

- .NET 8 SDK or runtime
- Spotify Developer App credentials
- An MCP-compatible client such as VS Code/Copilot or another MCP host

## Spotify Developer App setup

1. Open the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
2. Create a new app.
3. Copy the Client ID and Client Secret.
4. Add a redirect URI such as `http://localhost:3000/callback`.
5. Ensure the app has the required scopes for the operations you want to use.

The server reads its configuration from environment variables:

- `SPOTIFY_CLIENT_ID`
- `SPOTIFY_CLIENT_SECRET`
- `SPOTIFY_REDIRECT_URI` (defaults to `http://localhost:3000/callback`)
- `SPOTIFY_TOKEN_PATH` (optional; defaults to `~/.k_jubox/spotify-tokens.json`)

## Local setup

```bash
git clone https://github.com/Karlos-12/K_Jubox.git
cd K_Jubox
export SPOTIFY_CLIENT_ID="your-client-id"
export SPOTIFY_CLIENT_SECRET="your-client-secret"
export SPOTIFY_REDIRECT_URI="http://localhost:3000/callback"

dotnet restore
 dotnet build
```

On Windows PowerShell:

```powershell
$env:SPOTIFY_CLIENT_ID = "your-client-id"
$env:SPOTIFY_CLIENT_SECRET = "your-client-secret"
$env:SPOTIFY_REDIRECT_URI = "http://localhost:3000/callback"

dotnet restore
dotnet build
```

## Authentication flow

K_Jubox uses Spotify OAuth 2.0 Authorization Code with PKCE.

The typical flow is:

1. Start the K_Jubox server.
2. Invoke the MCP tool to generate the authorization URL.
3. Browser opens the authorization URL and the user approves access.
4. The callback returns `code` and `state`.
5. K_Jubox exchanges the code against Spotify and stores the access/refresh tokens locally.
6. Token refresh happens automatically when the access token expires.

The token file is stored at the location specified by `SPOTIFY_TOKEN_PATH` or at `~/.k_jubox/spotify-tokens.json` by default.

## MCP client configuration

Use the server via STDIO in an MCP client. Example `mcpServers` stanza:

```json
{
  "mcpServers": {
    "k_jubox": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/K_Jubox/K_Jubox/K_Jubox.csproj"
      ],
      "env": {
        "SPOTIFY_CLIENT_ID": "your-client-id",
        "SPOTIFY_CLIENT_SECRET": "your-client-secret",
        "SPOTIFY_REDIRECT_URI": "http://localhost:3000/callback"
      }
    }
  }
}
```

If you prefer a published build, run the compiled DLL instead of `dotnet run`.

## Available tool patterns

The server exposes dedicated tools for common Spotify actions, including:

- `begin_spotify_authorization`
- `complete_spotify_authorization`
- `refresh_spotify_token`
- `search_spotify`
- `get_track`
- `get_album`
- `get_artist`
- `get_playlist`
- `create_playlist`
- `get_current_playback`
- `get_available_devices`
- `start_playback`
- `pause_playback`
- `skip_to_next`
- `skip_to_previous`
- `get_recently_played`
- `get_recommendations`
- `get_generic_request`

The generic request tool is intended for Spotify endpoints that do not have a dedicated wrapper tool yet.

## Fallback behavior

If K_Jubox cannot handle a requested Spotify operation, the original Spotify MCP should be used as a fallback.

The original MCP is intentionally less capable, so K_Jubox remains the primary implementation for Spotify tasks.

## Build and CI

The repository includes a GitHub Actions workflow that restores dependencies and builds the solution.

## Platform notes

- The server runs over STDIO.
- It is designed to be lightweight and modular.
- No background polling loop is required; token refresh and API calls happen on demand.

## License

MIT