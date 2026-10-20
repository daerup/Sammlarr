using System.Diagnostics.CodeAnalysis;
using API;
using Core;
using DB;
using DB.Scheduling;
using Domain.Identity;
using JasperFx;
using Messaging;
using Observability;
using Observability.Diagnostics;
using Shared.DI;
using TickerQ.DependencyInjection;
using UI;
using Web;
using Web.Auth;
using Web.Health;
using Web.Licences;
using Web.Localization;
using Web.Setup;
#if STAGING_SEED
using Web.Staging;
#endif

[assembly: SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Scope = "member",
	Target = "~M:Program.<Main>$(System.String[])",
	Justification = "the composition root: it wires every layer's module and the request pipeline"
)]

if (args is ["healthcheck"])
{
	Environment.ExitCode = await ContainerHealthProbe.CheckAsync(new HttpClientHandler());
	return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModule<SharedModule>();
builder.Services.AddModule<ObservabilityModule>();
builder.Services.AddModule<DbModule>();
builder.Services.AddModule<CoreModule>();
builder.Services.AddModule<ApiModule>();

#if STAGING_SEED
if (args is ["seed-staging", ..])
{
	builder.Services.AddStagingSeed(builder.Configuration);
	await using var seeding = builder.Build();
	await seeding.Services.GetRequiredService<StagingRun>().RunAsync(CancellationToken.None);
	return;
}
#endif

builder.Services.AddModule<MessagingModule>();
builder.Services.AddModule<UiModule>();
builder.Services.AddModule<WebModule>();
builder.Services.AddSammlarrScheduling(builder.Configuration);
builder.Host.UseSammlarrMessaging(builder.Configuration);

var app = builder.Build();

if (args is ["codegen", ..])
{
	Environment.ExitCode = await app.RunJasperFxCommands(args);
	return;
}

app.UseTickerQ();

app.UseRequestLocalization(BrowserLocalization.CreateOptions());

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseMiddleware<FirstRunRedirect>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRequestTimeouts();
app.UseOutputCache();

app.MapStaticAssets().DisableRateLimiting();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(UI.Shared.Layout.MainLayout).Assembly);
app.MapAuthEndpoints();
app.MapHealthEndpoints();
app.MapNoticesEndpoint(AppContext.BaseDirectory);

app.MapGet(
		"/diagnostics/export",
		(LogCaptureService capture, TimeProvider clock) =>
		{
			var stream = capture.ExportZip();
			var fileName = FormattableString.Invariant($"diagnostics-{clock.GetUtcNow():yyyyMMdd-HHmmss}.zip");
			return Results.File(stream, "application/zip", fileName);
		}
	)
	.RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

await app.RunAsync();
