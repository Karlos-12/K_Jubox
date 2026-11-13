using K_Jubox.Spotify;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<SpotifySettings>();
builder.Services.AddSingleton<SpotifyAuthManager>();
builder.Services.AddSingleton<SpotifyApiClient>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SpotifyTools>();

await builder.Build().RunAsync();

