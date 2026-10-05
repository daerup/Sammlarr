using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Seeding;
using StagingSeeder;

var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });
builder.Configuration.AddSammlarrDefaults().AddEnvironmentVariables();
builder.Logging.AddSimpleConsole(console => console.SingleLine = true).AddFilter("Microsoft", LogLevel.Warning);
builder.Services.AddSeeding(builder.Configuration);
builder.Services.AddSingleton<StagingRun>();
using var host = builder.Build();
await host.Services.GetRequiredService<StagingRun>().RunAsync(CancellationToken.None);
