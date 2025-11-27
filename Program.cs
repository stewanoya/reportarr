using JellyfinReporter;
using Microsoft.Extensions.Options;
using NetCord.Hosting.Gateway;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration);

builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient();

builder.Services.AddDiscordGateway();

builder.Services.AddSingleton(serviceCollection => serviceCollection.GetRequiredService<IOptions<AppSettings>>().Value);
builder.Services.AddSingleton<IJellyfinClient, JellyfinClient>();
builder.Services.AddSingleton<IJellyfinReporterManager, JellyfinReporterManager>();
builder.Services.AddSingleton<IChatBot, ChatBot>();
builder.Services.AddSingleton<IRemoteSupportManager, RemoteSupportManager>();

var host = builder.Build();

host.Run();
