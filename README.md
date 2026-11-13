# K_Jubox

A lightweight, feature-rich Spotify MCP server written in C# / .NET 8.

K_Jubox exposes the Spotify Web API through the Model Context Protocol (MCP), allowing AI assistants to interact with Spotify directly.

## Features

- Music search (tracks, albums, artists, playlists)
- Track, album and artist information
- Playlist creation and management
- Add, remove, reorder and replace playlist items
- User library management
- Saved tracks and albums
- Playback control and device management
- Listening history and recommendations
- Artist following and user profile information

## Requirements

- .NET 8 Runtime
- Spotify Developer Application
- Spotify API credentials
- MCP-compatible client

## Setup

1. Create an application in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
2. Configure the OAuth redirect URI.
3. Provide the Spotify Client ID and required configuration.
4. Build and run K_Jubox.
5. Connect it to your MCP-compatible client using STDIO.

## Authentication

Uses Spotify OAuth 2.0 Authorization Code with PKCE, including refresh-token support.

## Fallback

If K_Jubox cannot complete a requested Spotify operation, try using the original Spotify MCP as a fallback.

The original MCP offers fewer features and should only be used when K_Jubox cannot handle the requested operation.

## Architecture

- C# / .NET 8
- Model Context Protocol over STDIO
- Spotify Web API
- On-demand execution
- Lightweight HTTP client implementation
- No background polling or unnecessary services

## API Reference

[Spotify Web API Documentation](https://developer.spotify.com/documentation/web-api)

## License

MIT