using System.Net;
using System.Security.Cryptography;
using System.Text;
using API;
using API.Http;
using API.Plex;
using API.Tmdb;
using Contracts;
using Contracts.Announcing;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Wantlist;
using Core;
using DB;
using DB.Data;
using DB.Data.Identity;
using DB.Data.Media;
using Domain.Announcing;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;

namespace Harness;

public sealed class SammlarrTestHost : IAsyncDisposable
{
	public const string TmdbApiKey = "e2e-tmdb-key";

	public const string MdblistApiKey = "e2e-mdblist-key";

	public const string PlexToken = "e2e-plex-token";

	public static readonly SeededUser SeededAdmin = Project(new TestCurrentUser());

	public static readonly SeededUser SeededMember = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Member User", UserRole.Member);

	public static readonly SeededUser SeededRemovable = new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Removable User", UserRole.Member);

	private static SeededUser Project(TestCurrentUser user) => new(user.Id.Value, user.DisplayName, user.Role);

	internal const int DefaultYear = 2024;

	private static readonly SqliteConnection Template = BuildTemplate();
	private static readonly Lock BackupGate = new();

	private const string DataProtectionApplicationName = "Sammlarr";

	private readonly SqliteConnection _connection = RestoreFromTemplate();
	private ServiceProvider? _dbOnlyProvider;
	private ServiceProvider? _fullGraphProvider;
	private IServiceScope? _fullGraphScope;
	private SammlarrDbContext? _context;
	private IDbContextFactory<SammlarrDbContext>? _contextFactory;
	private RawDbContext? _rawContext;
	private bool _plexAccountConfigured;
	private Func<MediaType, Optional<MediaProvider>>? _sinkAddressing;
	private bool _tmdbAccountConfigured;

	private readonly RemoteWantlistState _remote = new();
	private readonly MutableClock _clock = new();
	private readonly OAuthStub _oauth = new();
	private readonly SessionInvalidatorStub _sessionInvalidator = new();

	internal TestCurrentUser CurrentUser { get; } = new();

	internal SqliteConnection Connection => _connection;

	internal HttpStub HttpStub { get; } = new();

	private ServiceProvider DbOnlyProvider => _dbOnlyProvider ??= BuildDbOnlyProvider();

	internal SammlarrDbContext Context => _context ??= BuildContext(DbOnlyProvider, CurrentUser, _connection);

	internal SammlarrDbContext ContextOn(SqliteConnection connection) => BuildContext(DbOnlyProvider, CurrentUser, connection);

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

	internal IReadOnlyList<T> Published<T>() => Resolve<PublishedMessages>().Recorded<T>();

	internal List<IEvent> Heard()
	{
		List<IEvent> heard = [];
		Resolve<IEventPublisher>().Published += heard.Add;
		return heard;
	}

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

	internal void SetCurrentTime(DateTimeOffset now) => _clock.Now = now;

	internal void SinkAddresses(Func<MediaType, Optional<MediaProvider>> addressing) => _sinkAddressing = addressing;

	internal void SinkAddresses(MediaProvider provider) => SinkAddresses(_ => Some.Of(provider));

	internal async Task<MediaId> SeedWithDetailsAsync(ResolvedVersion resolved, DateTimeOffset fetched, IReadOnlyCollection<RemoteService> answeredBy)
	{
		var context = ScopedDbContext;
		var media = new MediaRow();
		foreach (var (provider, id) in resolved.Ids)
		{
			await context.ExternalMediaIds.AddAsync(new ProviderId(provider, id).ToExternal(resolved.Type).ToExternalIdEntity(media));
		}

		await context.SaveChangesAsync();
		await context.MediaDetails.AddAsync(BuildDetails(media.Id, resolved, fetched, answeredBy));
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private MediaDetailsRow BuildDetails(Guid mediaId, ResolvedVersion resolved, DateTimeOffset fetched, IReadOnlyCollection<RemoteService> answeredBy)
	{
		MediaDetailsRow details =
			resolved.Type == MediaType.Show
				? new ShowDetailsRow
				{
					Seasons = 1,
					Episodes = 1,
					RuntimePerEpisode = TimeSpan.FromMinutes(30),
				}
				: new MovieDetailsRow { Runtime = TimeSpan.FromHours(2) };

		details.MediaId = mediaId;
		details.Title = resolved.Title;
		details.Tagline = "Tagline";
		details.Summary = resolved.Summary ?? "An overview";
		details.PosterUrl = resolved.Poster is null ? "/poster.jpg" : resolved.Poster.NullIfBlank();
		details.BackdropUrl = "/backdrop.jpg";
		details.ReleaseDate = new DateOnly(resolved.Year, 1, 15);
		details.Genres = resolved.Genres is null ? ["Action"] : [.. resolved.Genres];
		details.Ratings =
		[
			new MediaRatingRow
			{
				Source = RatingSource.Tmdb,
				NormalizedValue = 7.5f,
				VoteCount = 100,
			},
		];
		details.RefreshedAt = fetched;
		details.AskedAt = fetched;
		var seeded = new MediaDetailsFactory().ToCached(details).Details;
		var enrichers = Resolve<IKeyedServiceSet<RemoteService, IDetailsEnricher>>();
		details.Answers =
		[
			.. answeredBy
				.Select(remote => enrichers.Find(remote).Map(enricher => ProviderAnswer.After(remote, enricher.Fills, seeded, fetched)))
				.WhereSome()
				.Select(answer => new ProviderAnswerRow
				{
					Provider = answer.Provider,
					Unfilled = answer.Unfilled,
					AnsweredAt = answer.AnsweredAt,
				}),
		];
		return details;
	}

	internal async Task WithPlexAccount(string token)
	{
		await Resolve<ICredentialService>().StoreAsync(AuthProvider.Plex, new StoredCredential(token, DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid()));
		HttpStub.RequireHeader(TestUrls.PlexApi, "X-Plex-Token", token);
	}

	internal async Task WithTmdbAccount(string apiKey)
	{
		await Resolve<IApiKeyService>().StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, apiKey));
		HttpStub.RequireQueryParam(TestUrls.TmdbApi, "api_key", apiKey);
	}

	internal async Task WithMdblistAccount(string apiKey)
	{
		await Resolve<IApiKeyService>().StoreAsync(new TestApiKey(ApiKeyProvider.Mdblist, apiKey));
		HttpStub.RequireQueryParam(TestUrls.MdblistApi, "apikey", apiKey);
	}

	internal void WikidataHolds(string imdb, params string[] paths)
	{
		var bindings = string.Join(',', paths.Select(path => $$$"""{"rt":{"type":"literal","value":"{{{path}}}"}}"""));
		HttpStub.RespondWithJsonForUrl(WikidataQueryFor(imdb), $$$"""{"results":{"bindings":[{{{bindings}}}]}}""");
	}

	internal void WikidataFails(string imdb) => HttpStub.RespondWithStatusForUrl(WikidataQueryFor(imdb), HttpStatusCode.ServiceUnavailable);

	internal int WikidataAsked(string imdb) => HttpStub.Requests(WikidataQueryFor(imdb));

	private static string WikidataQueryFor(string imdb) => Uri.EscapeDataString($"\"{imdb}\"");

	internal void TmdbUnreachable() => HttpStub.RespondForUrl(TestUrls.TmdbApi, _ => throw new HttpRequestException("The TMDB API is unreachable."));

	internal void MdblistFeedContains(string identifier, params FeedItemSpec[] items) => MdblistFeedStub.Contains(HttpStub, identifier, items);

	internal void MdblistFeedReturnsStatus(string identifier, HttpStatusCode status) => MdblistFeedStub.ReturnsStatus(HttpStub, identifier, status);

	internal void MdblistFeedReturnsInvalidJson(string identifier) => MdblistFeedStub.ReturnsInvalidJson(HttpStub, identifier);

	internal void MdblistFeedEmpty(string identifier) => MdblistFeedStub.Empty(HttpStub, identifier);

	internal HeldRequest HoldMdblistFeed(string identifier) => MdblistFeedStub.Hold(HttpStub, identifier);

	internal HeldRequest HoldRemoteWantlist() => HttpStub.Hold(PlexEndpoints.WatchlistPath);

	internal void MdblistFeedUnavailable(string identifier) => MdblistFeedStub.Unavailable(HttpStub, identifier);

	internal void MdblistFeedPartialThenUnavailable(string identifier, params FeedItemSpec[] items) =>
		MdblistFeedStub.PartialThenUnavailable(HttpStub, identifier, items);

	internal void MdblistFeedPaginated(string identifier, params FeedPage[] pages) => MdblistFeedStub.Paginated(HttpStub, identifier, pages);

	internal void PlexHolds(ResolvedVersion version, double? imdbRating = null, int imdbVotes = 0) => PlexStub.Lookup(HttpStub, version, imdbRating, imdbVotes);

	internal void PlexHolds(MediaSpec spec) => PlexStub.Lookup(HttpStub, FullVersion(spec));

	internal void TmdbHolds(ResolvedVersion version, double? rating, int votes) =>
		TmdbStub.Detail(HttpStub, version, rating ?? 0, votes, hasVoteData: rating.HasValue);

	internal async Task TmdbFinds(MediaSpec spec)
	{
		await EnsureTmdbAccount();
		TmdbStub.Find(HttpStub, FullVersion(spec));
	}

	internal async Task TmdbKnowsTheExternalIdsOf(MediaSpec spec)
	{
		await EnsureTmdbAccount();
		TmdbStub.Detail(HttpStub, FullVersion(spec), withExternalIds: true);
	}

	internal async Task TmdbDescribes(MediaSpec spec)
	{
		await EnsureTmdbAccount();
		TmdbStub.Detail(HttpStub, FullVersion(spec));
	}

	internal IReadOnlyCollection<RemoteService> Enrichers => Resolve<IKeyedServiceSet<RemoteService, IDetailsEnricher>>().Keys;

	internal IDetailsEnricher Enricher(RemoteService remote) =>
		Resolve<IEnumerable<Keyed<RemoteService, IDetailsEnricher>>>().Single(enricher => enricher.Key == remote).Capability;

	internal void RateLimited(RemoteService remote) => Resolve<RemoteCapacity>().Block(remote, TimeSpan.FromMinutes(1));

	internal void TmdbHasNoDetail(MediaSpec spec)
	{
		var tmdbId = spec.Ids.First(id => id.Provider == MediaProvider.Tmdb).Value;
		HttpStub.RespondWithJsonForUrl(string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, tmdbId), """{ "id": 0 }""");
	}

	internal void TmdbDetailFails(MediaSpec spec, HttpStatusCode status)
	{
		var tmdbId = spec.Ids.First(id => id.Provider == MediaProvider.Tmdb).Value;
		HttpStub.RespondForUrl(string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, tmdbId), _ => new HttpResponseMessage(status));
	}

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

	internal async Task PlexSearchFailsOnceThenReturns(string term, params MediaSpec[] hits)
	{
		await EnsurePlexAccount();
		PlexStub.SearchFailsOnce(HttpStub, term, [.. hits.Select(PlexHit)]);
	}

	internal async Task PlexFindsNothing(string term)
	{
		await EnsurePlexAccount();
		PlexStub.Search(HttpStub, term, []);
	}

	internal void PlexLookupFails(MediaSpec spec) => PlexStub.LookupFails(HttpStub, FullVersion(spec));

	internal void PlexHasNoMetadataFor(MediaSpec spec) => PlexStub.LookupHasNoMetadata(HttpStub, FullVersion(spec));

	internal async Task RemoteWantlistHas(params MediaSpec[] specs)
	{
		await EnsurePlexAccount();
		foreach (var spec in specs)
		{
			var version = FullVersion(spec);
			var plexId = version.Ids[MediaProvider.Plex];
			_remote.Known[plexId] = version;
			_remote.PlexIds.Add(plexId);
		}

		RemoteWantlistStub.Install(HttpStub, _remote);
	}

	internal async Task RemoteWantlistEmpty()
	{
		await EnsurePlexAccount();
		RemoteWantlistStub.Install(HttpStub, _remote);
	}

	internal async Task RemoteWantlistUnavailable()
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

	internal bool RemoteWantlistContains(MediaSpec spec) => _remote.PlexIds.Contains(FullVersion(spec).Ids[MediaProvider.Plex]);

	internal IReadOnlyCollection<string> RemoteWantlistPlexIds => _remote.PlexIds;

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
					TagName = CurrentUser.DisplayName,
					AvatarUrl = CurrentUser.AvatarUrl,
					Role = CurrentUser.Role,
					JoinedAt = DateTimeOffset.UnixEpoch,
				}
			);
			await context.SaveChangesAsync();
		}
	}

	internal void OAuthReturns(string externalUserId, string displayName, string avatarUrl = "avatar") =>
		_oauth.Returns(new ExternalLogin(externalUserId, displayName, avatarUrl, new OAuthTokenGrant("plex-token", _clock.Now.AddDays(30), Guid.NewGuid())));

	internal void OAuthFails(string reason) => _oauth.Fails(reason);

	internal IReadOnlyList<UserId> InvalidatedSessions => _sessionInvalidator.Invalidated;

	internal async Task<bool> HasStoredCredential(AuthProvider provider, UserId owner)
	{
		ScopedDbContext.ChangeTracker.Clear();
		return await ScopedDbContext
			.Credentials.IgnoreQueryFilters()
			.AnyAsync(credential => credential.Provider == provider && EF.Property<Guid>(credential, UserOwnedConfigurationExtensions.UserIdColumn) == owner.Value);
	}

	internal async Task SetDatabaseRole(UserId userId, UserRole role)
	{
		var context = ScopedDbContext;
		var user = await context.Users.SingleAsync(candidate => candidate.Id == userId.Value);
		user.Role = role;
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
	}

	internal async Task NoUsers()
	{
		var context = ScopedDbContext;
		await context.Users.ExecuteDeleteAsync();
		context.ChangeTracker.Clear();
	}

	internal async Task SetupUnfinished()
	{
		var context = ScopedDbContext;
		await context.ProviderSettings.ExecuteUpdateAsync(rows => rows.SetProperty(row => row.OnboardingCompletedAt, (DateTimeOffset?)null));
		context.ChangeTracker.Clear();
	}

	internal async Task RegistrationClosed()
	{
		var context = ScopedDbContext;
		var row = await context.ProviderSettings.SingleAsync();
		row.RegistrationMode = RegistrationMode.Closed;
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
	}

	internal async Task<UserId> SeedExistingUser(string externalUserId, UserRole role, string displayName)
	{
		var id = Guid.NewGuid();
		var context = ScopedDbContext;
		context.Users.Add(
			new UserRow
			{
				Id = id,
				Provider = AuthProvider.Plex,
				ExternalUserId = externalUserId,
				DisplayName = displayName,
				TagName = displayName,
				AvatarUrl = "avatar",
				Role = role,
				JoinedAt = DateTimeOffset.UnixEpoch,
			}
		);
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
		return UserId.From(id);
	}

	internal async Task SignsInWithSammlarrAccounts()
	{
		var context = ScopedDbContext;
		var row = await context.ProviderSettings.SingleAsync();
		row.AuthProvider = AuthProvider.Local;
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
	}

	internal async Task<UserId> SeedLocalUser(string username, string password, UserRole role)
	{
		var id = Guid.NewGuid();
		var context = ScopedDbContext;
		context.Users.Add(
			new UserRow
			{
				Id = id,
				Provider = AuthProvider.Local,
				ExternalUserId = Usernames.Normalize(username),
				DisplayName = username,
				TagName = username,
				AvatarUrl = string.Empty,
				Role = role,
				JoinedAt = DateTimeOffset.UnixEpoch,
				PasswordHash = new PasswordHasher<string>().HashPassword(string.Empty, password),
			}
		);
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
		return UserId.From(id);
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

	private static ServiceProvider BuildDbOnlyProvider(string? keyRingPath = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddModule<DbModule>();
		if (keyRingPath is null)
		{
			services.AddDataProtection().UseEphemeralDataProtectionProvider();
		}
		else
		{
			services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyRingPath)).SetApplicationName(DataProtectionApplicationName);
		}

		return services.BuildServiceProvider();
	}

	private AddressingSink AddressedPlexSink(PlexSink plex) => new(plex, type => (_sinkAddressing ?? plex.AddressedBy)(type));

	private ServiceProvider BuildFullGraphProvider()
	{
		var services = new ServiceCollection();
		services.AddSingleton(CurrentUser);
		services.AddScoped(sp => new OverrideAwareCurrentUser(sp.GetRequiredService<CurrentUserOverride>(), CurrentUser));
		services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<OverrideAwareCurrentUser>());

		services.AddScoped<IDbContextFactory<SammlarrDbContext>>(sp => new SharedConnectionFactory(
			sp,
			_connection,
			sp.GetRequiredService<OverrideAwareCurrentUser>()
		));
		services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<SammlarrDbContext>>().CreateDbContext());

		services.AddSingleton(new SchedulerOptions());
		services.AddSingleton(new AccountsOptions());
		services.AddModule<CoreModule>();
		services.AddModule<DbModule>();
		services.AddModule<ApiModule>();
		services.AddKeyedScoped<IMediaSink>(SinkKind.Plex, (sp, _) => AddressedPlexSink(ActivatorUtilities.CreateInstance<PlexSink>(sp)));
		services.AddSingleton<RecordingFeedScheduler>();
		services.AddSingleton<IFeedScheduler>(sp => sp.GetRequiredService<RecordingFeedScheduler>());
		services.AddKeyedSingleton<IOAuthProvider>(AuthProvider.Plex, _oauth);
		services.AddSingleton<ISessionInvalidator>(_sessionInvalidator);
		services.ConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(false));
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider>(_clock);

		services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = HttpStub));

		services.AddSingleton(
			new TmdbOptions
			{
				BaseUrl = $"https://{TestUrls.TmdbApi}",
				V4BaseUrl = $"https://{TestUrls.TmdbApi}/4",
				ImageBaseUrl = "https://image.tmdb.org/t/p",
			}
		);
		services.AddSingleton(new MdblistOptions { BaseUrl = $"https://{TestUrls.MdblistApi}" });
		services.AddSingleton(new WikidataOptions { BaseUrl = $"https://{TestUrls.WikidataApi}", UserAgent = "Sammlarr tests" });
		services.AddSingleton(new TvmazeOptions { BaseUrl = $"https://{TestUrls.TvmazeApi}", UserAgent = "Sammlarr tests" });
		services.AddSingleton(new ImdbDatasetsOptions { BaseUrl = $"https://{TestUrls.ImdbDatasets}", UserAgent = "Sammlarr tests" });
		services.AddSingleton(new AnimeListsOptions { BaseUrl = $"https://{TestUrls.AnimeLists}", UserAgent = "Sammlarr tests" });
		services.AddSingleton(new AniListOptions { BaseUrl = $"https://{TestUrls.AniList}", UserAgent = "Sammlarr tests" });
		services.AddSingleton(new RadarrOptions());
		services.AddSingleton(new SonarrOptions());
		services.AddSingleton(new PictureOptions());
		services.AddSingleton(
			new PlexOptions
			{
				BaseUrl = $"https://{TestUrls.PlexApi}",
				Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
			}
		);
		services.AddSingleton<PublishedMessages>();
		services.AddScoped<IMessagePublisher, RecordingMessagePublisher>();
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
		SeedRows.SearchWith(context, SearchProvider.Plex);
		SeedRows.SetupFinished(context);
		SeedUser(context, user);
		SeedRows.KeyPrompted(context);
		return connection;
	}

	public static SeededUser SeedDatabaseFile(string path, string keyRingPath) =>
		SeedFile(
			path,
			keyRingPath,
			context =>
			{
				SeedRows.ApiKey(context, ApiKeyProvider.Tmdb, TmdbApiKey);
				SeedRows.ApiKey(context, ApiKeyProvider.Mdblist, MdblistApiKey);
				SeedRows.Credential(context, AuthProvider.Plex, PlexToken);
			}
		);

	public static SeededUser SeedRecordingDatabase(
		string path,
		string keyRingPath,
		IReadOnlyDictionary<ApiKeyProvider, string> apiKeys,
		IReadOnlyDictionary<AuthProvider, string> tokens
	) =>
		SeedFile(
			path,
			keyRingPath,
			context =>
			{
				foreach (var (provider, apiKey) in apiKeys)
				{
					SeedRows.ApiKey(context, provider, apiKey);
				}

				foreach (var (provider, token) in tokens)
				{
					SeedRows.Credential(context, provider, token);
				}
			}
		);

	private static SeededUser SeedFile(string path, string keyRingPath, Action<SammlarrDbContext> seed)
	{
		using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
		connection.Open();
		using var provider = BuildDbOnlyProvider(keyRingPath);
		var user = new TestCurrentUser();
		using var context = BuildContext(provider, user, connection);
		context.Database.Migrate();
		SeedRows.SearchWith(context, SearchProvider.Plex);
		SeedRows.SetupFinished(context);
		SeedUser(context, user);
		SeedSecondaryUsers(context);
		SeedRows.KeyPrompted(context);
		seed(context);
		SeedRows.WriteAheadLogging(connection);
		return Project(user);
	}

	private static void SeedSecondaryUsers(SammlarrDbContext context)
	{
		foreach (var ((id, displayName, role), hue) in new[] { (SeededMember, 177.5), (SeededRemovable, 315.0) })
		{
			SeedRows.PlexUser(context, id, displayName, role, string.Empty, hue);
		}
	}

	private static void SeedUser(SammlarrDbContext context, TestCurrentUser user)
	{
		SeedRows.PlexUser(context, user.Id.Value, user.DisplayName, user.Role, user.AvatarUrl, 40);
	}

	private sealed class FixedClock : IDateTimeProvider
	{
		public DateTimeOffset Now { get; } = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
	}

	private sealed class MutableClock : IDateTimeProvider
	{
		public DateTimeOffset Now { get; set; } = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
	}

	private sealed class SharedConnectionFactory(IServiceProvider provider, SqliteConnection connection, ICurrentUser user) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext() => BuildContext(provider, user, connection);
	}
}
