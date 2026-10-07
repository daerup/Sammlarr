using Harness;
using TUnit.Assertions.Enums;

namespace Integration.Tests.DB;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class StoredEnumNamesTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Names_OfEveryEnumARowStores_StayAsTheRowsHoldThem()
	{
		var stored = host
			.Context.Model.GetEntityTypes()
			.SelectMany(entity => entity.GetProperties())
			.Select(property => EnumOf(property.ClrType))
			.Where(type => type is not null)
			.Select(type => type!)
			.Distinct()
			.OrderBy(type => type.Name, StringComparer.Ordinal)
			.Select(type => $"{type.Name}: {string.Join(", ", Enum.GetNames(type))}");

		await Assert
			.That(stored)
			.IsEquivalentTo(
				[
					"AccountLinkKind: Invite, Reset",
					"ApiKeyProvider: Tmdb, Mdblist, TmdbReadToken",
					"ArrFeedTagSource: ListName, Id",
					"ArrUserTagSource: Name, Id",
					"AuthProvider: Plex, Local",
					"Conclusiveness: Conclusive, StillLoading, NoItems, NoMatches, NoneAdmitted",
					"FeedProvider: Mdblist, Tmdb",
					"FeedRunItemState: Added, Kept, Removed",
					"JobKind: FeedRun, WantlistSync",
					"JobState: Waiting, Queued, Running, Done, Failed, Cancelled",
					"ManualSource: Unknown, Search, Browse, FeedPreview",
					"MediaProvider: Tmdb, Imdb, RottenTomatoes, Tvdb, Plex, Mdblist, Metacritic, Letterboxd, Trakt, Mal, Tvmaze, AniList",
					"MediaType: Movie, Show, Unknown",
					"NotificationChannel: Webhook, Pushover, WebUi",
					"NotificationEventType: WantlistSyncCompleted, WantlistSyncFailed, TestPing, FeedRunCompleted, FeedRunFailed, ProviderRateLimited",
					"NotificationLevel: Info, Success, Warning, Error",
					"OutcomeKind: Delivered, Retrying, Failed",
					"PushoverPriority: Normal, High, Emergency, Lowest, Low",
					"RatingSource: Tmdb, Imdb, RottenTomatoesCritics, RottenTomatoesAudience, Metacritic, Letterboxd, Trakt, MyAnimeList",
					"RegistrationMode: Open, Closed",
					"RemoteService: Tmdb, Plex, Mdblist, Wikidata, Tvmaze, ImdbDatasets, AnimeLists, Radarr, Sonarr",
					"RunCondition: OnSuccess, OnFailure, OnCancelled, OnFailureOrCancelled, OnAnyCompletedStatus, InProgress",
					"SearchProvider: Tmdb, Arr, Plex",
					"SinkKind: Plex, Arr",
					"TickerStatus: Idle, Queued, InProgress, Done, DueDone, Failed, Cancelled, Skipped",
					"TrendingProvider: Tmdb",
					"UserRole: Admin, Member",
					"WantlistItemOrigin: Sink, Manual, Feed, Orphaned",
					"WantlistOperationAction: Add, Remove",
					"WantlistOutboxState: Pending, Failed",
				],
				CollectionOrdering.Matching
			);
	}

	private static Type? EnumOf(Type type)
	{
		var single = Nullable.GetUnderlyingType(type) ?? type;
		var element = single.IsGenericType ? single.GetGenericArguments()[0] : single;
		return element.IsEnum ? element : null;
	}
}
