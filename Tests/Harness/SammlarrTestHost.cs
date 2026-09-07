using System.Net;
using System.Security.Cryptography;
using System.Text;
using API;
using API.Mdblist;
using API.Plex;
using API.Tmdb;
using Contracts;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Core;
using DB;
using DB.Data.Identity;
using DB.Data.Media;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;

namespace Harness;

public sealed class SammlarrTestHost : IAsyncDisposable
{
	private const int DefaultYear = 2024;

	private static readonly SqliteConnection Template = BuildTemplate();
	private static readonly Lock BackupGate = new();

	private readonly SqliteConnection _connection = RestoreFromTemplate();
	private ServiceProvider? _dbOnlyProvider;
	private ServiceProvider? _fullGraphProvider;
	private IServiceScope? _fullGraphScope;
	private SammlarrDbContext? _context;
	private IDbContextFactory<SammlarrDbContext>? _contextFactory;
	private RawDbContext? _rawContext;
	private bool _plexAccountConfigured;
	private bool _tmdbAccountConfigured;

	private readonly RemoteWatchlistState _remote = new();

	internal TestCurrentUser CurrentUser { get; } = new();

	internal HttpStub HttpStub { get; } = new();

	private ServiceProvider DbOnlyProvider => _dbOnlyProvider ??= BuildDbOnlyProvider();

	internal SammlarrDbContext Context => _context ??= BuildContext(DbOnlyProvider, CurrentUser, _connection);

	internal IDbContextFactory<SammlarrDbContext> ContextFactory => _contextFactory ??= new SharedConnectionFactory(DbOnlyProvider, _connection, CurrentUser);

	internal RawDbContext RawContext => _rawContext ??= new RawDbContext(Context);

	private IServiceProvider FullGraph
	{
		get
		{
			if (_fullGraphScope is null)
			{
				_fullGraphProvider = BuildFullGraphProvider();
				_fullGraphScope = _fullGraphProvider.CreateScope();
			}

			return _fullGraphScope.ServiceProvider;
		}
	}

	internal T Resolve<T>()
		where T : notnull => FullGraph.GetRequiredService<T>();

	internal T ResolveKeyed<T>(object key)
		where T : notnull => FullGraph.GetRequiredKeyedService<T>(key);

	internal SammlarrDbContext ScopedDbContext => Resolve<SammlarrDbContext>();

	internal async Task<MediaId> SeedMediaAsync(params IExternalMediaId[] externals)
	{
		var context = ScopedDbContext;
		var media = new MediaRow();
		foreach (var external in externals)
		{
			await context.ExternalMediaIds.AddAsync(external.ToExternalIdEntity(media));
		}

		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	internal async Task WithPlexAccount(string token)
	{
		await Resolve<ICredentialRepository>().StoreAsync(AuthProvider.Plex, token, DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());
		HttpStub.RequireHeader(TestUrls.PlexApi, "X-Plex-Token", token);
	}

	internal async Task WithTmdbAccount(string apiKey)
	{
		await Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, apiKey));
		HttpStub.RequireQueryParam(TestUrls.TmdbApi, "api_key", apiKey);
	}

	internal async Task WithMdblistAccount(string apiKey)
	{
		await Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Mdblist, apiKey));
		HttpStub.RequireQueryParam(TestUrls.MdblistApi, "apikey", apiKey);
	}

	internal void TmdbUnreachable() => HttpStub.RespondForUrl(TestUrls.TmdbApi, _ => throw new HttpRequestException("The TMDB API is unreachable."));

	internal void PlexHolds(ResolvedVersion version) => PlexStub.Lookup(HttpStub, version);

	internal void PlexHolds(MediaSpec spec) => PlexStub.Lookup(HttpStub, FullVersion(spec));

	internal void TmdbHolds(ResolvedVersion version, double? rating, int votes) =>
		TmdbStub.Detail(HttpStub, version, rating ?? 0, votes, hasVoteData: rating.HasValue);

	internal async Task TmdbTrendingPage(int page, params MediaSpec[] items)
	{
		await EnsureTmdbAccount();
		HttpStub.RespondWithJsonForUrl($"{TmdbEndpoints.PageParam}={page}", TmdbStub.TrendingJson([.. items.Select(FullVersion)], page));
	}

	internal async Task PlexSearchReturns(string term, params MediaSpec[] hits)
	{
		await EnsurePlexAccount();
		PlexStub.Search(HttpStub, term, [.. hits.Select(PlexHit)]);
	}

	internal async Task PlexFindsNothing(string term)
	{
		await EnsurePlexAccount();
		PlexStub.Search(HttpStub, term, []);
	}

	internal void PlexLookupFails(MediaSpec spec) => PlexStub.LookupFails(HttpStub, FullVersion(spec));

	internal async Task RemoteWatchlistHas(params MediaSpec[] specs)
	{
		await EnsurePlexAccount();
		foreach (var spec in specs)
		{
			var version = FullVersion(spec);
			var plexId = version.Ids[MediaProvider.Plex];
			_remote.Known[plexId] = version;
			_remote.PlexIds.Add(plexId);
		}

		RemoteWatchlistStub.Install(HttpStub, _remote);
	}

	internal async Task RemoteWatchlistEmpty()
	{
		await EnsurePlexAccount();
		RemoteWatchlistStub.Install(HttpStub, _remote);
	}

	internal async Task RemoteWatchlistUnavailable()
	{
		await EnsurePlexAccount();
		HttpStub.RespondWithStatusForUrl(PlexEndpoints.WatchlistPath, HttpStatusCode.InternalServerError);
	}

	internal async Task ProviderRejectsPushes()
	{
		await EnsurePlexAccount();
		HttpStub.RespondWithStatusForUrl(PlexEndpoints.AddToWatchlistPath, HttpStatusCode.InternalServerError);
		HttpStub.RespondWithStatusForUrl(PlexEndpoints.RemoveFromWatchlistPath, HttpStatusCode.InternalServerError);
	}

	internal bool RemoteWatchlistContains(MediaSpec spec) => _remote.PlexIds.Contains(FullVersion(spec).Ids[MediaProvider.Plex]);

	internal IReadOnlyCollection<string> RemoteWatchlistPlexIds => _remote.PlexIds;

	private static ResolvedVersion FullVersion(MediaSpec spec) =>
		new(spec.Ids.ToDictionary(id => id.Provider, id => id.Value), spec.Type, DefaultYear, spec.Title, null, null, null);

	private static ResolvedVersion PlexHit(MediaSpec spec) =>
		new(
			new Dictionary<MediaProvider, string> { [MediaProvider.Plex] = spec.Ids.First(id => id.Provider == MediaProvider.Plex).Value },
			spec.Type,
			DefaultYear,
			spec.Title,
			null,
			null,
			null
		);

	internal MediaBuilder Movie(MediaSpec spec) => new(this, spec.Type, spec.Title, spec.Ids);

	internal MediaBuilder Show(MediaSpec spec) => new(this, spec.Type, spec.Title, spec.Ids);

	internal MediaBuilder Movie(string title, string? tmdb = null, string? plex = null, string? imdb = null, string? tvdb = null)
	{
		var ids = new List<ProviderId>();
		if (tmdb is not null)
		{
			ids.Add(Ids.Tmdb(tmdb));
		}

		if (plex is not null)
		{
			ids.Add(Ids.Plex(plex));
		}

		if (imdb is not null)
		{
			ids.Add(Ids.Imdb(imdb));
		}

		if (tvdb is not null)
		{
			ids.Add(Ids.Tvdb(tvdb));
		}

		return new MediaBuilder(this, MediaType.Movie, title, ids);
	}

	internal async Task EnsurePlexAccount()
	{
		if (_plexAccountConfigured)
		{
			return;
		}

		await WithPlexAccount("plex-token");
		_plexAccountConfigured = true;
	}

	internal async Task EnsureTmdbAccount()
	{
		if (_tmdbAccountConfigured)
		{
			return;
		}

		await WithTmdbAccount("tmdb-key");
		_tmdbAccountConfigured = true;
	}

	internal async Task SignInAs(string name)
	{
		CurrentUser.Id = UserId.From(new Guid(MD5.HashData(Encoding.UTF8.GetBytes(name))));
		CurrentUser.DisplayName = name;
		CurrentUser.Role = UserRole.Member;
		CurrentUser.IsAuthenticated = true;

		await using var context = Resolve<IDbContextFactory<SammlarrDbContext>>().CreateDbContext();
		if (!await context.Users.AnyAsync(user => user.Id == CurrentUser.Id.Value))
		{
			context.Users.Add(
				new UserRow
				{
					Id = CurrentUser.Id.Value,
					Provider = AuthProvider.Plex,
					ExternalUserId = CurrentUser.Id.Value.ToString(),
					DisplayName = CurrentUser.DisplayName,
					AvatarUrl = CurrentUser.AvatarUrl,
					Role = CurrentUser.Role,
					JoinedAt = DateTimeOffset.UnixEpoch,
				}
			);
			await context.SaveChangesAsync();
		}
	}

	internal void SignOut() => CurrentUser.IsAuthenticated = false;

	internal async Task<int> CountOwnedAsync<T>(Func<SammlarrDbContext, IQueryable<T>> selector)
	{
		await using var context = Resolve<IDbContextFactory<SammlarrDbContext>>().CreateDbContext();
		return await selector(context).CountAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if (_context is not null)
		{
			await _context.DisposeAsync();
		}

		_fullGraphScope?.Dispose();
		if (_fullGraphProvider is not null)
		{
			await _fullGraphProvider.DisposeAsync();
		}

		if (_dbOnlyProvider is not null)
		{
			await _dbOnlyProvider.DisposeAsync();
		}

		await _connection.DisposeAsync();
	}

	private static SqliteConnection RestoreFromTemplate()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		lock (BackupGate)
		{
			Template.BackupDatabase(connection);
		}

		return connection;
	}

	private static SammlarrDbContext BuildContext(IServiceProvider provider, ICurrentUser user, SqliteConnection connection) =>
		new(
			new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(connection).Options,
			new EntityAuditer(),
			new UserOwnershipStamper(),
			user,
			provider.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
			provider.GetRequiredService<IEnumerable<IModelConfiguration>>(),
			new FixedClock()
		);

	private static ServiceProvider BuildDbOnlyProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddModule<DbModule>();
		services.AddDataProtection().UseEphemeralDataProtectionProvider();
		return services.BuildServiceProvider();
	}

	private ServiceProvider BuildFullGraphProvider()
	{
		var services = new ServiceCollection();
		services.AddSingleton(CurrentUser);
		services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

		services.AddScoped<IDbContextFactory<SammlarrDbContext>>(sp => new SharedConnectionFactory(sp, _connection, CurrentUser));
		services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<SammlarrDbContext>>().CreateDbContext());

		services.AddModule<CoreModule>();
		services.AddModule<DbModule>();
		services.AddModule<ApiModule>();
		services.ConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(false));
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider>(new FixedClock());

		services.AddHttpClient<PlexClient>().ConfigurePrimaryHttpMessageHandler(_ => HttpStub);
		services.AddHttpClient(PlexClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(_ => HttpStub);
		services.AddHttpClient<TmdbClient>().ConfigurePrimaryHttpMessageHandler(_ => HttpStub);
		services.AddHttpClient(TmdbClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(_ => HttpStub);
		services.AddHttpClient<MdblistClient>().ConfigurePrimaryHttpMessageHandler(_ => HttpStub);
		services.AddHttpClient(MdblistClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(_ => HttpStub);

		services.AddSingleton(new TmdbOptions { BaseUrl = $"https://{TestUrls.TmdbApi}", ImageBaseUrl = "https://image.tmdb.org/t/p/original" });
		services.AddSingleton(new MdblistOptions { BaseUrl = $"https://{TestUrls.MdblistApi}" });
		services.AddSingleton(
			new PlexOptions
			{
				BaseUrl = $"https://{TestUrls.PlexApi}",
				Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
			}
		);
		services.AddScoped<IMessagePublisher, InlineWatchlistPushPublisher>();
		return services.BuildServiceProvider();
	}

	private static SqliteConnection BuildTemplate()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		using var provider = BuildDbOnlyProvider();
		var user = new TestCurrentUser();
		using var context = BuildContext(provider, user, connection);
		context.Database.Migrate();
		SeedUser(context, user);
		return connection;
	}

	private static void SeedUser(SammlarrDbContext context, TestCurrentUser user)
	{
		context.Users.Add(
			new UserRow
			{
				Id = user.Id.Value,
				Provider = AuthProvider.Plex,
				ExternalUserId = user.Id.Value.ToString(),
				DisplayName = user.DisplayName,
				AvatarUrl = user.AvatarUrl,
				Role = user.Role,
				JoinedAt = DateTimeOffset.UnixEpoch,
			}
		);
		context.SaveChanges();
	}

	private sealed class FixedClock : IDateTimeProvider
	{
		public DateTimeOffset Now { get; } = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
	}

	private sealed class SharedConnectionFactory(IServiceProvider provider, SqliteConnection connection, ICurrentUser user) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext() => BuildContext(provider, user, connection);
	}
}
