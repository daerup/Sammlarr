using API;
using Core;
using DB;
using Microsoft.AspNetCore.Authentication.Cookies;
using Observability.Diagnostics;
using Shared;
using Shared.DI;
using UI;
using UI.Features.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModule<CoreModule>();
builder.Services.AddModule<SharedModule>();
builder.Services.AddModule<DbModule>();
builder.Services.AddModule<ApiModule>();
builder.Services.AddModule<UiModule>();

builder.Services.AddCascadingAuthenticationState();
builder
	.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.Cookie.Name = "sammlarr.auth";
		options.LoginPath = "/auth/login";
		options.ExpireTimeSpan = TimeSpan.FromDays(30);
		options.SlidingExpiration = true;
	});
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAuthEndpoints();

app.MapGet(
	"/diagnostics/export",
	(LogCaptureService capture) =>
	{
		var stream = capture.ExportZip();
		var fileName = $"diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip";
		return Results.File(stream, "application/zip", fileName);
	}
);

app.Run();
