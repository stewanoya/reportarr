using JellyfinReporter;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration);

builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient();

builder.Services.AddSingleton(serviceCollection => serviceCollection.GetRequiredService<IOptions<AppSettings>>().Value);
builder.Services.AddSingleton<IJellyfinClient, JellyfinClient>();
builder.Services.AddSingleton<IJellyfinReporterManager, JellyfinReporterManager>();

var host = builder.Build();

host.Run();
