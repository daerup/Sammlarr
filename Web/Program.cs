using API;
using Core;
using DB;
using DB.Scheduling;
using Domain.Identity;
using Messaging;
using Observability;
using Observability.Diagnostics;
using Shared.DI;
using TickerQ.DependencyInjection;
using UI;
using Web;
using Web.Auth;
using Web.Health;
using Web.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModule<SharedModule>();
builder.Services.AddModule<ObservabilityModule>();
builder.Services.AddModule<DbModule>();
builder.Services.AddModule<CoreModule>();
builder.Services.AddModule<ApiModule>();
builder.Services.AddModule<MessagingModule>();
builder.Services.AddModule<UiModule>();
builder.Services.AddModule<WebModule>();
builder.Services.AddSammlarrScheduling(builder.Configuration);
builder.Host.UseSammlarrMessaging(builder.Configuration);

var app = builder.Build();
app.UseTickerQ();

app.UseRequestLocalization(BrowserLocalization.CreateOptions());

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.UseRequestTimeouts();
app.UseOutputCache();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(UI.Shared.Layout.MainLayout).Assembly);
app.MapAuthEndpoints();
app.MapHealthEndpoints();

app.MapGet(
		"/diagnostics/export",
		(LogCaptureService capture) =>
		{
			var stream = capture.ExportZip();
			var fileName = $"diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip";
			return Results.File(stream, "application/zip", fileName);
		}
	)
	.RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

app.Run();
