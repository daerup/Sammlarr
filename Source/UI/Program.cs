using API;
using DB;
using Shared;
using Shared.DI;
using UI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddModule<SharedModule>();
builder.Services.AddModule<DbModule>();
builder.Services.AddModule<ApiModule>();
builder.Services.AddModule<UiModule>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
