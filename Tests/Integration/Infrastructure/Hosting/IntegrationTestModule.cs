using API;
using API.Mdblist;
using API.Plex;
using API.Tmdb;
using Contracts;
using Contracts.Identity;
using Contracts.Messaging;
using Core;
using DB;
using Integration.Tests.Infrastructure.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks;

namespace Integration.Tests.Infrastructure.Hosting;

internal sealed class IntegrationTestModule : ITestServiceModule
{
	public HttpStub Handler { private get; set; } = null!;
	public SqliteConnection Connection { private get; set; } = null!;

	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<TestCurrentUser>();
		services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

		// Pre-register before DbModule so EF Core's TryAdd skips its IDbContextFactory registration
		services.AddScoped<IDbContextFactory<SammlarrDbContext>>(sp => new TestDbContextFactory(sp, Connection));
		services.AddScoped<SammlarrDbContext>(sp => sp.GetRequiredService<IDbContextFactory<SammlarrDbContext>>().CreateDbContext());

		services.AddModule<CoreModule>();
		services.AddModule<DbModule>();
		services.AddModule<ApiModule>();
		services.ConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(false));

		services.AddLogging();

		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
		services.AddSingleton<Mock<IDateTimeProvider>>(clock);
		services.AddSingleton(clock.Object);

		services.AddHttpClient<PlexClient>().ConfigurePrimaryHttpMessageHandler(_ => Handler);
		services.AddHttpClient(PlexClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(_ => Handler);
		services.AddHttpClient<TmdbClient>().ConfigurePrimaryHttpMessageHandler(_ => Handler);
		services.AddHttpClient(TmdbClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(_ => Handler);
		services.AddHttpClient<MdblistClient>().ConfigurePrimaryHttpMessageHandler(_ => Handler);
		services.AddHttpClient(MdblistClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(_ => Handler);

		services.AddSingleton(new TmdbOptions { BaseUrl = "https://api.test.com", ImageBaseUrl = "https://image.tmdb.org/t/p/original" });
		services.AddSingleton(new MdblistOptions { BaseUrl = "https://mdblist.test.com" });
		services.AddSingleton(
			new PlexOptions
			{
				BaseUrl = "https://plex.test.com",
				Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
			}
		);
		services.AddScoped<IMessagePublisher, InlineWatchlistPushPublisher>();
	}

	private sealed class TestDbContextFactory(IServiceProvider sp, SqliteConnection connection) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext()
		{
			return new SammlarrDbContext(
				new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(connection).Options,
				sp.GetRequiredService<EntityAuditer>(),
				sp.GetRequiredService<UserOwnershipStamper>(),
				sp.GetRequiredService<ICurrentUser>(),
				sp.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
				sp.GetRequiredService<IEnumerable<IModelConfiguration>>(),
				sp.GetRequiredService<IDateTimeProvider>()
			);
		}
	}
}
