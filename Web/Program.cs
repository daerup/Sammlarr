using API;
using Core;
using DB;
using Domain.Identity;
using Messaging;
using Observability.Diagnostics;
using Shared;
using Shared.DI;
using Web;
using Westwind.AspNetCore.LiveReload;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModule<CoreModule>();
builder.Services.AddModule<SharedModule>();
builder.Services.AddModule<DbModule>();
builder.Services.AddModule<ApiModule>();
builder.Services.AddModule<UiModule>();
builder.Services.AddModule<MessagingModule>();
builder.Host.UseSammlarrMessaging(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
	builder.Services.AddLiveReload();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseLiveReload();
}

app.UseRequestLocalization(BrowserLocalization.CreateOptions());

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
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
