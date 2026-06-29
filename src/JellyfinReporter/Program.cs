using JellyfinReporter.Configuration;
using JellyfinReporter.Discord;
using JellyfinReporter.Discord.Interactions;
using JellyfinReporter.Health;
using JellyfinReporter.MediaManager;
using JellyfinReporter.QueueReporting;
using JellyfinReporter.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration);

builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient();

builder.Services.AddDiscordGateway();

builder.Services.AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();

builder.Services.AddSingleton(serviceCollection => serviceCollection.GetRequiredService<IOptions<AppSettings>>().Value);
builder.Services.AddSingleton<IJellyfinClient, JellyfinClient>();
builder.Services.AddSingleton<IJellyfinReporterManager, JellyfinReporterManager>();
builder.Services.AddSingleton<IChatBot, ChatBot>();

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("AppSettings could not be bound from configuration.");

if (settings.Sonarr is { Enabled: true })
    RegisterQueueMonitor(builder.Services, ArrServiceKind.Sonarr,
        settings.Sonarr.BaseUrl, settings.Sonarr.ApiKey, settings.Sonarr.RefreshInterval);

if (settings.Radarr is { Enabled: true })
    RegisterQueueMonitor(builder.Services, ArrServiceKind.Radarr,
        settings.Radarr.BaseUrl, settings.Radarr.ApiKey, settings.Radarr.RefreshInterval);

if (settings.Sonarr?.Enabled == true || settings.Radarr?.Enabled == true)
    builder.Services.AddHostedService<QueuesWorker>();

var host = builder.Build();

host.AddComponentInteractionModule<QueueDmButtonModule>();

host.Run();

static void RegisterQueueMonitor(IServiceCollection services, ArrServiceKind kind, string baseUrl, string apiKey, int refreshInterval)
{
    var serviceConfig = new ArrServiceConfig(kind, baseUrl, apiKey, refreshInterval);
    services.AddSingleton(serviceConfig);
    services.AddSingleton<IArrClient>(sp => new ArrClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), serviceConfig));
    services.AddSingleton<IQueueMonitor, QueueMonitor>();
}
