using System.Net;
using System.Security.Cryptography;
using System.Text;
using API;
using API.Http.Lanes;
using API.Mal;
using API.Mdblist;
using API.Plex;
using API.Tmdb;
using Contracts.Announcing;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Providers;
using Contracts.Wantlist;
using Core;
using Core.Jobs;
using DB;
using DB.Data;
using DB.Data.Feed;
using DB.Data.Identity;
using DB.Data.Media;
using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
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

	public static readonly SeedKeys ReplayKeys = new(
		new Dictionary<ApiKeyProvider, string> { [ApiKeyProvider.Tmdb] = TmdbApiKey, [ApiKeyProvider.Mdblist] = MdblistApiKey },
		new Dictionary<AuthProvider, string> { [AuthProvider.Plex] = PlexToken }
	);

	private static SeededUser Project(TestCurrentUser user) => new(user.Id.Value, user.DisplayName, user.Role);

	internal const int DefaultYear = 2024;

	private static readonly string Scratch = Path.Combine(Path.GetTempPath(), "sammlarr-tests", Guid.NewGuid().ToString("N"));
	private static readonly Lazy<string> Template = new(BuildTemplate);

	private const string DataProtectionApplicationName = "Sammlarr";

	private readonly string _path = Path.Combine(Scratch, $"{Guid.NewGuid():N}.db");
	private string? _connectionString;
	private SqliteConnection? _connection;
	private ServiceProvider? _dbOnlyProvider;
	private ServiceProvider? _fullGraphProvider;
	private IServiceScope? _fullGraphScope;
	private SammlarrDbContext? _context;
	private bool _plexAccountConfigured;
	private Func<MediaType, Optional<MediaProvider>>? _sinkAddressing;
	private bool _tmdbAccountConfigured;

	private readonly RemoteWantlistState _remote = new();
	private readonly MutableClock _clock = new();

	private Optional<FakeTimeProvider> _time = new None();
	private Optional<TelemetryCapture> _capture = new None();
	private bool _cachesOnTheClock;

	private TimeProvider Clock => _time.Match<FakeTimeProvider, TimeProvider>(time => time, () => _clock);
	private readonly OAuthStub _oauth = new();
	private readonly SessionInvalidatorStub _sessionInvalidator = new();

	internal TestCurrentUser CurrentUser { get; } = new();

	private string ConnectionString => _connectionString ??= CopiedTemplate(_path);

	internal SqliteConnection Connection => _connection ??= Opened(ConnectionString);

	internal HttpStub HttpStub { get; } = new();

	private ServiceProvider DbOnlyProvider => _dbOnlyProvider ??= BuildDbOnlyProvider(ConnectionString);

	internal SammlarrDbContext Context => _context ??= BuildContext(DbOnlyProvider, CurrentUser, ConnectionString);

	internal IDbContextFactory<SammlarrDbContext> ContextFactory => field ??= new FileContextFactory(DbOnlyProvider, ConnectionString, CurrentUser);

	internal RawDbContext RawContext => field ??= new RawDbContext(Context);

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

	internal async Task<FeedId> SeedFeedAsync(
		string identifier = "hdlists/comedy",
		FeedStrategy? strategy = null,
		QueryFilter? filter = null,
		QueryLimit? limit = null,
		string name = "Comedy"
	)
	{
		await using var context = await ContextFactory.CreateDbContextAsync();
		var feed = new FeedRow
		{
			Name = name,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = strategy ?? new Append(),
			Identifier = identifier,
			Filter = filter,
			Limit = limit,
		};
		context.Feeds.Add(feed);
		await context.SaveChangesAsync();
		return FeedId.From(feed.Id);
	}

	internal JobRunner BuildJobRunner() =>
		new(
			Resolve<IServiceScopeFactory>(),
			Resolve<JobSignal>(),
			Resolve<JobAlarm>(),
			Resolve<JobRunnerStatus>(),
			Resolve<JobTurn>(),
			Resolve<TimeProvider>(),
			NullLogger<JobRunner>.Instance
		);

	internal Task<bool> RunNextJobAsync() => BuildJobRunner().TryRunNextAsync(CancellationToken.None);

	internal static async Task EventuallyAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		while (!condition())
		{
			await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
		}
	}

	internal static HttpResponseMessage MdblistAllowance(long remaining, DateTimeOffset reset)
	{
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		answered.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		return answered;
	}

	internal void MdblistKeyHasLeft(string key, long remaining)
	{
		var budget = Resolve<MdblistBudget>();
		budget.Record(key, MdblistAllowance(30, Clock.GetUtcNow().AddHours(-1)));
		budget.Record(key, MdblistAllowance(1000, Clock.GetUtcNow().AddDays(1)));
		budget.Record(key, MdblistAllowance(remaining, Clock.GetUtcNow().AddDays(1)));
	}

	internal async Task<MediaId> SeedMediaAsync(params IIdentityKey[] externals)
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

	internal void SetCurrentTime(DateTimeOffset now)
	{
		_clock.Now = now;
		_time.Apply(time => time.SetUtcNow(now));
	}

	internal void ExpireCachesOnTheClock() => _cachesOnTheClock = true;

	internal FakeTimeProvider UseFakeTime()
	{
		var time = new FakeTimeProvider(_clock.Now);
		_time = Some.Of(time);
		return time;
	}

	internal void SinkAddresses(Func<MediaType, Optional<MediaProvider>> addressing) => _sinkAddressing = addressing;

	internal void SinkAddresses(MediaProvider provider) => SinkAddresses(_ => Some.Of(provider));

	internal async Task<MediaId> SeedWithDetailsAsync(ResolvedVersion resolved, DateTimeOffset fetched, IReadOnlyCollection<RemoteService> answeredBy)
	{
		var context = ScopedDbContext;
		var media = new MediaRow();
		foreach (var (provider, id) in resolved.Ids)
		{
			await context.ExternalMediaIds.AddAsync(new ProviderId(provider, id).ToKey(resolved.Type).ToExternalIdEntity(media));
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
		await Resolve<ICredentialService>()
			.StoreAsync(AuthProvider.Plex, new StoredCredential(new Secret(token), DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid()));
		HttpStub.RequireHeader(TestUrls.PlexApi, "X-Plex-Token", token);
		HttpStub.RespondWithStatusForUrl($"{TestUrls.PlexApi}/{PlexEndpoints.UserPath}", HttpStatusCode.OK);
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
		foreach (var source in new[] { "imdb", "tmdb", "tvdb", "trakt", "mal", "mdblist" })
		{
			foreach (var type in new[] { "movie", "show", "any" })
			{
				HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/{source}/{type}", "[]");
			}
		}
	}

	internal async Task WithMalAccount(string clientId)
	{
		await Resolve<IApiKeyService>().StoreAsync(new TestApiKey(ApiKeyProvider.Mal, clientId));
		HttpStub.RequireHeader(TestUrls.Mal, MalClient.ClientIdHeader, clientId);
	}

	internal async Task WithTraktAccount(string clientId)
	{
		await Resolve<IApiKeyService>().StoreAsync(new TestApiKey(ApiKeyProvider.Trakt, clientId));
		HttpStub.RequireHeader(TestUrls.Trakt, "trakt-api-key", clientId);
	}

	internal void WikidataHolds(string imdb, params string[] paths)
	{
		Wikidata.Holds(imdb, [.. paths.Select(path => $$$"""{"rt":{"type":"literal","value":"{{{path}}}"}}""")]);
	}

	internal void WikidataMaps(string imdb, string tmdb)
	{
		Wikidata.Holds(imdb, [$$$"""{"tmdb":{"type":"literal","value":"{{{tmdb}}}"}}"""]);
	}

	internal void WikidataHoldsPages(string imdb, string rottenTomatoes, string metacritic)
	{
		Wikidata.Holds(imdb, [$$$"""{"rt":{"type":"literal","value":"{{{rottenTomatoes}}}"},"metacritic":{"type":"literal","value":"{{{metacritic}}}"}}"""]);
	}

	internal void WikidataFails(string imdb) => Wikidata.Fails(imdb);

	internal int WikidataAsked(string imdb) => Wikidata.Asked(imdb);

	private WikidataStub Wikidata => field ??= new WikidataStub(HttpStub);

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

	internal void MdblistFeedPages(string identifier, params IReadOnlyList<FeedItemSpec>[] pages) => MdblistFeedStub.Pages(HttpStub, identifier, pages);

	internal void MalSeasonHolds(string identifier, params FeedItemSpec[] items) => MalFeedStub.Season(HttpStub, identifier, items);

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

	internal Task RateLimitedAsync(RemoteService remote, int minutes = 1) => RateLimitedAsync(remote, TimeSpan.FromMinutes(minutes));

	internal async Task RateLimitedAsync(RemoteService remote, TimeSpan block)
	{
		var gate = Resolve<Lanes>().Gate(new LaneName(remote, Lanes.Rest), new None());
		var entered = await gate.EnterAsync(LanePriority.Interactive, TimeSpan.Zero, CancellationToken.None);
		entered
			.GetOrThrow(_ => new InvalidOperationException($"{remote}'s lane refused the permit that throttles it."))
			.Release(new AttemptThrottled(Some.Of(Clock.GetUtcNow() + block)));
	}

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

	internal async Task PlexMatches(MediaSpec spec)
	{
		await EnsurePlexAccount();
		PlexStub.Match(HttpStub, FullVersion(spec));
	}

	internal async Task PlexMatchesNothing(MediaSpec spec)
	{
		await EnsurePlexAccount();
		PlexStub.MatchNothing(HttpStub, FullVersion(spec));
	}

	internal async Task PlexMatchFails(MediaSpec spec, HttpStatusCode status)
	{
		await EnsurePlexAccount();
		PlexStub.MatchFails(HttpStub, FullVersion(spec), status);
	}

	internal async Task PlexMatchFailsOnceThenFinds(MediaSpec spec)
	{
		await EnsurePlexAccount();
		PlexStub.MatchFailsOnce(HttpStub, FullVersion(spec));
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

	internal MediaBuilder Movie(string title, string? tmdb = null, string? plex = null)
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
			var id = CurrentUser.Id.Value;
			SeedRows.Member(context, SeedRows.User(id, $"plex-{id}", CurrentUser.DisplayName, CurrentUser.AvatarUrl), CurrentUser.Role, Some.Of(id.ToString()));
		}
	}

	internal void OAuthReturns(string externalUserId, string displayName, string avatarUrl = "avatar") =>
		_oauth.Returns(
			new ExternalLogin(externalUserId, displayName, avatarUrl, new OAuthTokenGrant(new Secret("plex-token"), Clock.GetUtcNow().AddDays(30), Guid.NewGuid()))
		);

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
		await context.UserRoles.Where(membership => membership.UserId == userId.Value).ExecuteDeleteAsync();
		context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId.Value, RoleId = SeedRows.RoleId(context, role) });
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

	internal Task<UserId> SeedExistingUser(string externalUserId, UserRole role, string displayName)
	{
		var id = Guid.NewGuid();
		var context = ScopedDbContext;
		SeedRows.Member(context, SeedRows.User(id, $"plex-{externalUserId}", displayName, "avatar"), role, Some.Of(externalUserId));
		context.ChangeTracker.Clear();
		return Task.FromResult(UserId.From(id));
	}

	internal async Task SignsInWithSammlarrAccounts()
	{
		var context = ScopedDbContext;
		var row = await context.ProviderSettings.SingleAsync();
		row.AuthProvider = AuthProvider.Local;
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
	}

	internal Task<UserId> SeedLocalUser(string username, Secret password, UserRole role)
	{
		var id = Guid.NewGuid();
		var context = ScopedDbContext;
		var user = SeedRows.User(id, Usernames.Normalize(username), username, string.Empty);
		user.PasswordHash = new PasswordHasher<UserRow>().HashPassword(user, password.Reveal());
		SeedRows.Member(context, user, role, new None());
		context.ChangeTracker.Clear();
		return Task.FromResult(UserId.From(id));
	}

	internal void SignOut() => CurrentUser.IsAuthenticated = false;

	internal TelemetryCapture CaptureTelemetry()
	{
		var capture = new TelemetryCapture();
		_capture = Some.Of(capture);
		return capture;
	}

	internal bool DatabaseHolds(string text)
	{
		var needle = Encoding.UTF8.GetBytes(text);
		return new[] { _path, $"{_path}-wal" }.Where(File.Exists).Any(file => Bytes(file).AsSpan().IndexOf(needle) >= 0);
	}

	private static byte[] Bytes(string file)
	{
		using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var copy = new MemoryStream();
		stream.CopyTo(copy);
		return copy.ToArray();
	}

	internal async Task<string?> SecurityStampOf(UserId id)
	{
		await using var context = Resolve<IDbContextFactory<SammlarrDbContext>>().CreateDbContext();
		return await context.Users.Where(user => user.Id == id.Value).Select(user => user.SecurityStamp).SingleAsync();
	}

	internal async Task<int> CountOwnedAsync<T>(Func<SammlarrDbContext, IQueryable<T>> selector)
	{
		await using var context = Resolve<IDbContextFactory<SammlarrDbContext>>().CreateDbContext();
		return await selector(context).CountAsync();
	}

	public async ValueTask DisposeAsync()
	{
		_capture.Apply(capture => capture.Dispose());
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

		if (_connection is not null)
		{
			await _connection.DisposeAsync();
		}

		if (_connectionString is not null)
		{
			SqliteConnection.ClearPool(new SqliteConnection(_connectionString));
			File.Delete(_path);
		}

		if (HttpStub.Unexpected.Count > 0)
		{
			throw new InvalidOperationException($"Unstubbed calls: {string.Join(", ", HttpStub.Unexpected.Distinct())}");
		}
	}

	internal static void RemoveScratch()
	{
		if (Template.IsValueCreated)
		{
			SqliteConnection.ClearAllPools();
			Directory.Delete(Scratch, recursive: true);
		}
	}

	private static string CopiedTemplate(string path)
	{
		File.Copy(Template.Value, path);
		return $"Data Source={path}";
	}

	private static SqliteConnection Opened(string connectionString)
	{
		var connection = new SqliteConnection(connectionString);
		connection.Open();
		return connection;
	}

	private static SammlarrDbContext BuildContext(IServiceProvider provider, ICurrentUser user, string connectionString)
	{
		return BuildContext(
			provider,
			user,
			new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(connectionString).AddInterceptors(new SqlitePragmaInterceptor()).Options
		);
	}

	private static SammlarrDbContext BuildContext(IServiceProvider provider, ICurrentUser user, SqliteConnection connection)
	{
		return BuildContext(provider, user, new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite(connection).Options);
	}

	private static SammlarrDbContext BuildContext(IServiceProvider provider, ICurrentUser user, DbContextOptions<SammlarrDbContext> options)
	{
		return new(
			options,
			new EntityAuditer(),
			new UserOwnershipStamper(),
			user,
			provider.GetRequiredService<IEnumerable<IPropertyAddedConvention>>(),
			provider.GetRequiredService<IEnumerable<IModelConfiguration>>(),
			new FakeTimeProvider(MutableClock.Start)
		);
	}

	private static ServiceProvider BuildDbOnlyProvider(string connectionString, string? keyRingPath = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new SammlarOptions { ConnectionString = connectionString });
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

		services.AddScoped<IDbContextFactory<SammlarrDbContext>>(sp => new FileContextFactory(
			sp,
			ConnectionString,
			sp.GetRequiredService<OverrideAwareCurrentUser>()
		));
		services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<SammlarrDbContext>>().CreateDbContext());

		services.AddSingleton(new SammlarOptions { ConnectionString = ConnectionString });
		services.AddSingleton(new SchedulerOptions());
		services.AddSingleton(new FeedLoadOptions());
		services.AddSingleton(new HousekeepingOptions());
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
		services.AddLogging(logging => _capture.Apply(capture => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(new FakeLoggerProvider(capture.Logs))));
		services.AddSingleton(Clock);
		if (_cachesOnTheClock)
		{
			services.Configure<MemoryCacheOptions>(options => options.Clock = new CacheClock(Clock));
		}

		services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = HttpStub));

		services.AddSingleton(
			new TmdbOptions
			{
				BaseUrl = $"https://{TestUrls.TmdbApi}",
				V4BaseUrl = $"https://{TestUrls.TmdbApi}/4",
				ImageBaseUrl = "https://image.tmdb.org/t/p",
				Http = Unretried(),
			}
		);
		services.AddSingleton(
			new MdblistOptions
			{
				BaseUrl = $"https://{TestUrls.MdblistApi}",
				Http = Unretried() with { Lanes = new Dictionary<string, LaneOptions> { ["titles"] = new LaneOptions { MaxBatch = 200 } } },
			}
		);
		services.AddSingleton(
			new WikidataOptions
			{
				BaseUrl = $"https://{TestUrls.WikidataApi}",
				UserAgent = "Sammlarr tests",
				Http = Unretried() with { Lanes = new Dictionary<string, LaneOptions> { ["sparql"] = new LaneOptions { MaxBatch = 100 } } },
			}
		);
		services.AddSingleton(
			new TvmazeOptions
			{
				BaseUrl = $"https://{TestUrls.TvmazeApi}",
				UserAgent = "Sammlarr tests",
				Http = Unretried(),
			}
		);
		services.AddSingleton(
			new ImdbDatasetsOptions
			{
				BaseUrl = $"https://{TestUrls.ImdbDatasets}",
				UserAgent = "Sammlarr tests",
				Http = Unretried(),
			}
		);
		services.AddSingleton(
			new AnimeListsOptions
			{
				BaseUrl = $"https://{TestUrls.AnimeLists}",
				UserAgent = "Sammlarr tests",
				Http = Unretried(),
			}
		);
		services.AddSingleton(
			new AniListOptions
			{
				BaseUrl = $"https://{TestUrls.AniList}",
				UserAgent = "Sammlarr tests",
				Http = Unretried() with
				{
					Lanes = new Dictionary<string, LaneOptions>
					{
						["titles"] = new LaneOptions { MaxBatch = 50 },
						["prequels"] = new LaneOptions { MaxBatch = 50 },
					},
				},
			}
		);
		services.AddSingleton(new MalOptions { BaseUrl = $"https://{TestUrls.Mal}", Http = Unretried() });
		services.AddSingleton(new PushoverOptions { BaseUrl = $"https://{TestUrls.Pushover}", Http = Unretried() });
		services.AddSingleton(new WebhookOptions { Http = Unretried() });
		services.AddSingleton(
			new TraktOptions
			{
				BaseUrl = $"https://{TestUrls.Trakt}",
				UserAgent = "Sammlarr-tests",
				Http = Unretried(),
			}
		);
		services.AddSingleton(new RadarrOptions());
		services.AddSingleton(new SonarrOptions());
		services.AddSingleton(new PictureOptions());
		services.AddSingleton(
			new PlexOptions
			{
				BaseUrl = $"https://{TestUrls.PlexApi}",
				Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
				Http = Unretried(),
			}
		);
		services.AddSingleton<PublishedMessages>();
		services.AddScoped<IMessagePublisher, RecordingMessagePublisher>();
		services.AddDataProtection().UseEphemeralDataProtectionProvider();
		return services.BuildServiceProvider();
	}

	private static ProviderHttpOptions Unretried() => new() { RetryAttempts = 0 };

	private static string BuildTemplate()
	{
		var path = Path.Combine(Directory.CreateDirectory(Scratch).FullName, "template.db");
		using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
		connection.Open();
		using var provider = BuildDbOnlyProvider(connection.ConnectionString);
		var user = new TestCurrentUser();
		using var context = BuildContext(provider, user, connection);
		context.Database.Migrate();
		SeedRows.SearchWith(context, SearchProvider.Plex);
		SeedRows.SetupFinished(context);
		SeedUser(context, user);
		SeedRows.WriteAheadLogging(connection);
		return path;
	}

	public static void SeedDatabaseFile(string path, string keyRingPath, SeedKeys keys)
	{
		using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
		connection.Open();
		using var provider = BuildDbOnlyProvider(connection.ConnectionString, keyRingPath);
		var user = new TestCurrentUser();
		using var context = BuildContext(provider, user, connection);
		context.Database.Migrate();
		SeedRows.SearchWith(context, SearchProvider.Plex);
		SeedRows.SetupFinished(context);
		SeedUser(context, user);
		SeedRows.PlexUser(context, SeededMember.Id, SeededMember.DisplayName, SeededMember.Role, string.Empty, 177.5);
		SeedMemberTmdbKey(context, user, keys.ApiKeys[ApiKeyProvider.Tmdb]);
		SeedCredentials(context, keys);
		SeedRows.WriteAheadLogging(connection);
	}

	public static void SeedPerson(string path, string keyRingPath, SeededUser person, SeedKeys keys)
	{
		using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
		connection.Open();
		using var provider = BuildDbOnlyProvider(connection.ConnectionString, keyRingPath);
		using var context = BuildContext(provider, new TestCurrentUser { Id = UserId.From(person.Id) }, connection);
		SeedRows.PlexUser(context, person.Id, person.DisplayName, person.Role, string.Empty, 220);
		SeedCredentials(context, keys);
	}

	public static string SecurityStampIn(string path, Guid id)
	{
		using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
		connection.Open();
		using var provider = BuildDbOnlyProvider(connection.ConnectionString);
		using var context = BuildContext(provider, new TestCurrentUser(), connection);
		return context.Users.Where(user => user.Id == id).Select(user => user.SecurityStamp).Single()!;
	}

	private static void SeedCredentials(SammlarrDbContext context, SeedKeys keys)
	{
		foreach (var (provider, apiKey) in keys.ApiKeys)
		{
			SeedRows.ApiKey(context, provider, apiKey);
		}

		foreach (var (provider, token) in keys.Tokens)
		{
			SeedRows.Credential(context, provider, token);
		}
	}

	private static void SeedMemberTmdbKey(SammlarrDbContext context, TestCurrentUser user, string apiKey)
	{
		var owner = user.Id;
		user.Id = UserId.From(SeededMember.Id);
		SeedRows.ApiKey(context, ApiKeyProvider.Tmdb, apiKey);
		user.Id = owner;
	}

	private static void SeedUser(SammlarrDbContext context, TestCurrentUser user)
	{
		SeedRows.PlexUser(context, user.Id.Value, user.DisplayName, user.Role, user.AvatarUrl, 40);
	}

	private sealed class MutableClock : TimeProvider
	{
		internal static readonly DateTimeOffset Start = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

		public DateTimeOffset Now { get; set; } = Start;

		public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

		public override DateTimeOffset GetUtcNow() => Now;
	}

	private sealed class CacheClock(TimeProvider clock) : ISystemClock
	{
		public DateTimeOffset UtcNow => clock.GetUtcNow();
	}

	private sealed class FileContextFactory(IServiceProvider provider, string connectionString, ICurrentUser user) : IDbContextFactory<SammlarrDbContext>
	{
		public SammlarrDbContext CreateDbContext() => BuildContext(provider, user, connectionString);
	}
}
