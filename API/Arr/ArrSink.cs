using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using API.Arr.Types;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace API.Arr;

internal sealed class ArrSink(
	RadarrClient radarr,
	SonarrClient sonarr,
	IArrSinkConfigService configs,
	IMediaService mediaService,
	ICurrentUser currentUser,
	ILogger<ArrSink> logger
) : IMediaSink
{
	public SinkKind Kind => SinkKind.Arr;

	private ArrTagUser User => new(currentUser.Id.Value, currentUser.DisplayName);

	public Optional<MediaProvider> AddressedBy(MediaType type)
	{
		return type switch
		{
			MediaType.Movie => Some.Of(MediaProvider.Tmdb),
			MediaType.Show => Some.Of(MediaProvider.Tvdb),
			MediaType.Unknown => new None(),
		};
	}

	public async Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default)
	{
		var config = await configs.GetAsync(cancellationToken);
		List<FoundMedia> items = [.. await MineAsync(radarr, config, cancellationToken), .. await MineAsync(sonarr, config, cancellationToken)];
		var requests = items.Select(item => new IdentifyRequest([.. item.Externals], new None()));
		List<MediaId> matched = [];
		List<UnmatchedMedia> unmatched = [];
		await foreach (var (result, item) in mediaService.IdentifyAsync(requests, cancellationToken).ZipAwait(items, cancellationToken))
		{
			result.Apply(identity => matched.Add(identity.Id)).ApplyError(_ => unmatched.Add(item.Unmatched()));
		}

		return new SinkSnapshot(matched, unmatched);
	}

	public async IAsyncEnumerable<SinkSyncAck> ApplyAsync(
		WantlistOperationAction action,
		IReadOnlyList<SinkPush> pushes,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var config = await configs.GetAsync(cancellationToken);
		var sinkIds = await mediaService.GetSinkIdsAsync([.. pushes.Select(push => push.MediaId)], cancellationToken);
		var sessions = new Dictionary<ArrApp, Result<ArrSession>>();
		foreach (var push in pushes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var outcome = await sinkIds
				.GetValueOrNone(push.MediaId)
				.Match(id => PushAsync(action, push, id, config, sessions, cancellationToken), () => Task.FromResult(SinkSyncOutcome.Unaddressable));
			yield return new SinkSyncAck(push.MediaId, outcome);
		}
	}

	private async Task<IReadOnlyList<FoundMedia>> MineAsync(ArrClient client, ArrSinkConfig config, CancellationToken cancellationToken)
	{
		var opened = await ArrSession.OpenAsync(client, ArrSession.Connect(client.App, Settings(config, client.App)), cancellationToken);
		var session = opened.Match(open => open, error => throw new InvalidOperationException(error.Reason));
		var mine = session.Mine(config.Tags, User);
		return [.. session.Titles.Where(title => title.Tags.Any(mine.Contains)).Select(title => Found(client.App, title))];
	}

	private async Task<SinkSyncOutcome> PushAsync(
		WantlistOperationAction action,
		SinkPush push,
		IExternalMediaId id,
		ArrSinkConfig config,
		Dictionary<ArrApp, Result<ArrSession>> sessions,
		CancellationToken cancellationToken
	)
	{
		var app = id.Type is MediaType.Show ? ArrApp.Sonarr : ArrApp.Radarr;
		if (!sessions.TryGetValue(app, out var session))
		{
			session = await ArrSession.OpenAsync(Client(app), ArrSession.Connect(app, Settings(config, app)), cancellationToken);
			sessions[app] = session;
		}

		var pushed = await session.Match(open => PushToAsync(open, action, push, id, config, cancellationToken), Failed);
		return pushed
			.ApplyError(error => logger.LogWarning("Pushing {Action} for {ExternalId} to {App} failed: {Reason}", action, id, app.Name, error.Reason))
			.GetValueOr(SinkSyncOutcome.Failed);
	}

	private Task<Result<SinkSyncOutcome>> PushToAsync(
		ArrSession session,
		WantlistOperationAction action,
		SinkPush push,
		IExternalMediaId id,
		ArrSinkConfig config,
		CancellationToken cancellationToken
	)
	{
		return action switch
		{
			WantlistOperationAction.Add => AddAsync(session, push, id, config, cancellationToken),
			WantlistOperationAction.Remove => RemoveAsync(session, id, config.Tags, cancellationToken),
		};
	}

	private async Task<Result<SinkSyncOutcome>> AddAsync(
		ArrSession session,
		SinkPush push,
		IExternalMediaId id,
		ArrSinkConfig config,
		CancellationToken cancellationToken
	)
	{
		var feed = push.AddedBy.Map(added => new ArrTagFeed(added.Id.Value, added.Name, added.Identifier));
		var tags = await session.EnsureTagsAsync(config.Tags.Labels(User, feed), cancellationToken);
		return await tags.Match(ids => PlaceAsync(session, id, Settings(config, session.App), ids, cancellationToken), Failed);
	}

	private static Task<Result<SinkSyncOutcome>> PlaceAsync(
		ArrSession session,
		IExternalMediaId id,
		ArrInstanceConfig settings,
		IReadOnlyList<int> tags,
		CancellationToken cancellationToken
	)
	{
		return session
			.Find(id)
			.Match(existing => TagAsync(session, existing, tags, cancellationToken), () => CreateAsync(session, id, settings, tags, cancellationToken));
	}

	private static async Task<Result<SinkSyncOutcome>> TagAsync(ArrSession session, ArrTitle title, IReadOnlyList<int> tags, CancellationToken cancellationToken)
	{
		var tagged = await session.RetagAsync(title, tags, ArrTagChange.Add, cancellationToken);
		return tagged.Map(_ => SinkSyncOutcome.Applied);
	}

	private static Task<Result<SinkSyncOutcome>> CreateAsync(
		ArrSession session,
		IExternalMediaId id,
		ArrInstanceConfig settings,
		IReadOnlyList<int> tags,
		CancellationToken cancellationToken
	)
	{
		return settings
			.QualityProfileId.Map(profile => settings.RootFolderPath.Map(folder => new ArrAddition(profile, folder, tags)))
			.Match(
				addition => LookUpAndAddAsync(session, id, settings, addition, cancellationToken),
				() => Failed(Error.From($"{session.App.Name} has no quality profile or root folder chosen yet."))
			);
	}

	private static async Task<Result<SinkSyncOutcome>> LookUpAndAddAsync(
		ArrSession session,
		IExternalMediaId id,
		ArrInstanceConfig settings,
		ArrAddition addition,
		CancellationToken cancellationToken
	)
	{
		var looked = await session.LookupAsync(id, cancellationToken);
		return await looked.Match(
			found =>
				found.Match(
					title => AddedAsync(session, ArrAdd.Prepare(session.App, title, settings, addition), cancellationToken),
					() => Done(SinkSyncOutcome.Unaddressable)
				),
			Failed
		);
	}

	private static async Task<Result<SinkSyncOutcome>> AddedAsync(ArrSession session, JsonObject title, CancellationToken cancellationToken)
	{
		var added = await session.AddAsync(title, cancellationToken);
		return added.Map(_ => SinkSyncOutcome.Applied);
	}

	private Task<Result<SinkSyncOutcome>> RemoveAsync(ArrSession session, IExternalMediaId id, ArrTagScheme scheme, CancellationToken cancellationToken)
	{
		return session.Find(id).Match(title => UntagAsync(session, title, scheme, cancellationToken), () => Done(SinkSyncOutcome.Applied));
	}

	private async Task<Result<SinkSyncOutcome>> UntagAsync(ArrSession session, ArrTitle title, ArrTagScheme scheme, CancellationToken cancellationToken)
	{
		var mine = session.Mine(scheme, User);
		var wantedByOthers = scheme.PerUser && title.Tags.Where(tag => !mine.Contains(tag)).Any(tag => scheme.IsUserTag(session.Label(tag)));
		var removed = wantedByOthers
			? await session.RetagAsync(title, [.. title.Tags.Where(mine.Contains)], ArrTagChange.Remove, cancellationToken)
			: await session.DeleteAsync(title, cancellationToken);
		return removed.Map(_ => SinkSyncOutcome.Applied);
	}

	private ArrClient Client(ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => radarr,
			ArrApp.Sonarr => sonarr,
		};
	}

	private static ArrInstanceConfig Settings(ArrSinkConfig config, ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => config.Radarr,
			ArrApp.Sonarr => config.Sonarr,
		};
	}

	private static FoundMedia Found(ArrApp app, ArrTitle title)
	{
		Optional<IExternalMediaId>[] ids =
		[
			Optional.When(app is ArrApp.Radarr, () => (IExternalMediaId)TmdbId.From(title.TmdbId.ToString(), MediaType.Movie)),
			Optional.When(app is ArrApp.Sonarr, () => (IExternalMediaId)TvdbId.From(title.TvdbId.ToString(), MediaType.Show)),
			Optional.When(app is ArrApp.Sonarr && title.TmdbId > 0, () => (IExternalMediaId)TmdbId.From(title.TmdbId.ToString(), MediaType.Show)),
			Optional.Of(title.ImdbId).When(imdb => imdb.Length > 0).Map(imdb => (IExternalMediaId)ImdbId.From(imdb, app.Type)),
		];
		return new FoundMedia([.. ids.WhereSome()], MediaDetails.Empty(app.Type) with { Title = title.Title, ReleaseDate = ProviderValues.YearStart(title.Year) });
	}

	private static Task<Result<SinkSyncOutcome>> Done(SinkSyncOutcome outcome) => Task.FromResult<Result<SinkSyncOutcome>>(Success.Of(outcome));

	private static Task<Result<SinkSyncOutcome>> Failed(Error error) => Task.FromResult<Result<SinkSyncOutcome>>(error);
}
