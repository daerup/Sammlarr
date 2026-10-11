using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using API.Arr.Types;
using Contracts.Datasets;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;

namespace API.Arr;

internal sealed class ArrSink(
	ArrApis apis,
	IArrSinkConfigService configs,
	IUserRepository users,
	IMediaService mediaService,
	IAnimeIds animeIds,
	ICurrentUser currentUser,
	ILogger<ArrSink> logger
) : IMediaSink
{
	private const string Animation = "Animation";

	public SinkKind Kind => SinkKind.Arr;

	public Optional<MediaProvider> AddressedBy(MediaType type) => apis.All.FirstOrNone(api => api.App.Type == type).Map(api => api.AddressedBy);

	public async Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default)
	{
		var config = await configs.GetAsync(cancellationToken);
		var viewer = await ViewerAsync(cancellationToken);
		var claims = (await ClaimantAsync(config.Tags, cancellationToken)).Match(first => first.Id == viewer.Id, () => false);
		List<FoundMedia> items = [];
		List<(ArrApp App, string Reason)> failed = [];
		foreach (var api in apis.All)
		{
			var mine = await MineAsync(api, config, viewer, claims, cancellationToken);
			mine.Apply(items.AddRange).ApplyError(error => failed.Add((api.App, error.Reason)));
		}

		var reason = string.Join(" ", failed.Select(app => app.Reason));
		if (failed.Count == apis.All.Count)
		{
			throw new ArrAppsUnreachableException(reason, [.. failed.Select(app => app.App)]);
		}

		var down = Optional.When(failed.Count > 0, () => new WantlistSyncSinkUnreachable(reason, [.. failed.Select(app => app.App)]));

		var requests = items.Select(item => new IdentifyRequest([.. item.Externals], new None())
		{
			Sources = [item.Details.Type is MediaType.Show ? RemoteService.Sonarr : RemoteService.Radarr],
		});
		List<MediaId> matched = [];
		List<UnmatchedMedia> unmatched = [];
		await foreach (var (result, item) in mediaService.IdentifyAsync(requests, cancellationToken).ZipAwait(items, cancellationToken))
		{
			result.Apply(identity => matched.Add(identity.Id)).ApplyError(_ => unmatched.Add(item.Unmatched()));
		}

		return new SinkSnapshot(matched, unmatched) { Down = down };
	}

	public async IAsyncEnumerable<SinkSyncAck> ApplyAsync(
		WantlistOperationAction action,
		IReadOnlyList<SinkPush> pushes,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var config = await configs.GetAsync(cancellationToken);
		List<MediaId> ids = [.. pushes.Select(push => push.MediaId)];
		var sinkIds = await mediaService.GetSinkIdsAsync(ids, cancellationToken);
		var setup = new Setup(
			config,
			await ClaimantAsync(config.Tags, cancellationToken),
			await ViewerAsync(cancellationToken),
			await AnimeAsync(action, config, sinkIds, cancellationToken),
			await StoredAsync(action, sinkIds, cancellationToken)
		);
		var sessions = new Dictionary<ArrApp, Result<ArrSession>>();
		foreach (var push in pushes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return await sinkIds
				.GetValueOrNone(push.MediaId)
				.Match(
					id => PushAsync(action, push, id, setup, sessions, cancellationToken),
					() => Unaddressable(push.MediaId, "Sammlarr has no TMDB or TVDB id for this title, the ids Radarr and Sonarr add by.")
				);
		}
	}

	public async Task<Result<SinkHoldings>> HoldingsOfAsync(User person, CancellationToken cancellationToken = default)
	{
		var config = await configs.GetAsync(cancellationToken);
		var owner = Owner(person);
		var held = await HeldAsync(config, owner, cancellationToken);
		return held.Map(apps => new SinkHoldings(apps.Sum(app => app.Sole.Count), apps.Sum(app => app.Staying.Count), config.Tags.UserTag(owner)));
	}

	public async Task<Result<int>> DeleteTitlesOfAsync(User person, CancellationToken cancellationToken = default)
	{
		var config = await configs.GetAsync(cancellationToken);
		var held = await HeldAsync(config, Owner(person), cancellationToken);
		return await held.Map(apps => ClearAllAsync(apps, cancellationToken));
	}

	private static ArrTagUser Owner(User person) => new(person.Id.Value, person.TagName);

	private async Task<ArrTagUser> ViewerAsync(CancellationToken cancellationToken) => Owner(await users.GetAsync(currentUser.Id, cancellationToken));

	private async Task<Result<IReadOnlyList<Held>>> HeldAsync(ArrSinkConfig config, ArrTagUser owner, CancellationToken cancellationToken)
	{
		if (!config.Tags.PerUser)
		{
			return Error.From("Radarr + Sonarr tags titles without naming the person here, so their titles cannot be told apart.");
		}

		var others = (await users.GetAllAsync(cancellationToken)).Where(user => user.Id.Value != owner.Id).Select(Owner).ToList();
		Result<IReadOnlyList<Held>> held = Success.Of<IReadOnlyList<Held>>([]);
		foreach (var api in apis.All)
		{
			var session = await ArrSession.OpenAsync(api, ArrSession.Connect(api.App, config.Of(api.App)), cancellationToken);
			held = held.Map(found => session.Map(open => (IReadOnlyList<Held>)[.. found, Holdings(open, config.Tags, owner, others)]));
		}

		return held;
	}

	private static Held Holdings(ArrSession session, ArrTagScheme scheme, ArrTagUser owner, IReadOnlyList<ArrTagUser> others)
	{
		var theirs = session.Mine(scheme, owner);
		ValueSet<int> taken = [.. others.SelectMany(other => session.Mine(scheme, other))];
		ValueSet<int> only = [.. theirs.Where(tag => !taken.Contains(tag))];
		var titles = session.Titles.Where(title => title.Tags.Any(theirs.Contains)).ToList();
		var sole = titles
			.Where(title => title.Tags.Any(only.Contains) && !session.IsProtected(title, scheme) && !session.WantedByOthers(title, scheme, only))
			.ToList();
		return new Held(session, sole, [.. titles.Except(sole)], only);
	}

	private static async Task<Result<int>> ClearAllAsync(IReadOnlyList<Held> apps, CancellationToken cancellationToken)
	{
		Result<int> deleted = Success.Of(0);
		foreach (var app in apps)
		{
			deleted = await deleted.Map(count => ClearAsync(app, count, cancellationToken));
		}

		return deleted;
	}

	private static async Task<Result<int>> ClearAsync(Held app, int before, CancellationToken cancellationToken)
	{
		Result<int> deleted = app.Sole.Count == 0 ? Success.Of(0) : await app.Session.DeleteAsync(app.Sole, cancellationToken);
		return await deleted.Match(gone => UntagAsync(app, before + gone, cancellationToken), error => AlreadyDeleted(error, before));
	}

	private static async Task<Result<int>> UntagAsync(Held app, int deleted, CancellationToken cancellationToken)
	{
		Result<int> untagged =
			app.Staying.Count == 0 || app.Only.Count == 0 ? Success.Of(0) : await app.Session.UntagAsync(app.Staying, [.. app.Only], cancellationToken);
		return untagged.Map(_ => deleted).MapError(error => AlreadyDeleted(error, deleted));
	}

	private static Error AlreadyDeleted(Error error, int deleted)
	{
		return deleted == 0 ? error : Error.From($"{error.Reason} {deleted} of their titles were already deleted.");
	}

	private static async Task<Result<IReadOnlyList<FoundMedia>>> MineAsync(
		ArrApi api,
		ArrSinkConfig config,
		ArrTagUser viewer,
		bool claims,
		CancellationToken cancellationToken
	)
	{
		var opened = await ArrSession.OpenAsync(api, ArrSession.Connect(api.App, config.Of(api.App)), cancellationToken);
		return opened.Map(session => Mine(api, session, config, viewer, claims));
	}

	private static IReadOnlyList<FoundMedia> Mine(ArrApi api, ArrSession session, ArrSinkConfig config, ArrTagUser viewer, bool claims)
	{
		var mine = session.Mine(config.Tags, viewer);
		return
		[
			.. session
				.Titles.Where(title => title.Tags.Any(mine.Contains) || (claims && session.IsUnclaimed(title, config.Tags)))
				.Select(title => ArrFound.Of(api, title)),
		];
	}

	private Task<SinkSyncAck> PushAsync(
		WantlistOperationAction action,
		SinkPush push,
		IExternalMediaId id,
		Setup setup,
		Dictionary<ArrApp, Result<ArrSession>> sessions,
		CancellationToken cancellationToken
	)
	{
		return apis.Taking(id)
			.Match(
				async api => Ack(push.MediaId, await PushWithAsync(api, action, push, id, setup, sessions, cancellationToken)),
				() => Unaddressable(push.MediaId, $"Neither Radarr nor Sonarr adds a {$"{id.Type}".ToLowerInvariant()} by its {id.Source.GetDescription()} id.")
			);
	}

	private static SinkSyncAck Ack(MediaId id, Result<SinkSyncOutcome> pushed)
	{
		return pushed.Match(outcome => new SinkSyncAck(id, outcome), error => new SinkSyncAck(id, SinkSyncOutcome.Failed) { Reason = Some.Of(error.Reason) });
	}

	private static SinkSyncAck Unaddressable(MediaId id, string reason) => new(id, SinkSyncOutcome.Unaddressable) { Reason = Some.Of(reason) };

	private async Task<Result<SinkSyncOutcome>> PushWithAsync(
		ArrApi api,
		WantlistOperationAction action,
		SinkPush push,
		IExternalMediaId id,
		Setup setup,
		Dictionary<ArrApp, Result<ArrSession>> sessions,
		CancellationToken cancellationToken
	)
	{
		if (!sessions.TryGetValue(api.App, out var session))
		{
			session = await ArrSession.OpenAsync(api, ArrSession.Connect(api.App, setup.Config.Of(api.App)), cancellationToken);
			sessions[api.App] = session;
		}

		var pushed = await session.Map(open => PushToAsync(open, action, push, id, setup, cancellationToken));
		return pushed.ApplyError(error => logger.LogWarning("Pushing {Action} for {ExternalId} to {App} failed: {Reason}", action, id, api.App.Name, error.Reason));
	}

	private Task<Result<SinkSyncOutcome>> PushToAsync(
		ArrSession session,
		WantlistOperationAction action,
		SinkPush push,
		IExternalMediaId id,
		Setup setup,
		CancellationToken cancellationToken
	)
	{
		return action switch
		{
			WantlistOperationAction.Add => AddAsync(session, push, id, setup, cancellationToken),
			WantlistOperationAction.Remove => RemoveAsync(session, id, setup, cancellationToken),
		};
	}

	private async Task<Result<SinkSyncOutcome>> AddAsync(ArrSession session, SinkPush push, IExternalMediaId id, Setup setup, CancellationToken cancellationToken)
	{
		var feed = push.AddedBy.Map(added => new ArrTagFeed(added.Id.Value, added.Name));
		var labels = setup.Config.Tags.Labels(setup.Viewer, feed);
		return await session
			.Find(id)
			.Match(
				existing => TagAsync(session, existing, Labeling(session, existing, setup, labels), cancellationToken),
				() => CreateAsync(session, id, setup.Config.Of(session.App), labels, setup.About(push.MediaId), cancellationToken)
			);
	}

	private static IReadOnlyList<string> Labeling(ArrSession session, ArrTitle title, Setup setup, IReadOnlyList<string> labels)
	{
		var scheme = setup.Config.Tags;
		if (session.Owned(title, scheme).Count == 0)
		{
			return [.. labels, scheme.ProtectedTag];
		}

		return session.IsUnclaimed(title, scheme)
			? setup.Claimant.Match(claimant => [.. labels.Append(scheme.UserTag(claimant)).Distinct()], () => labels)
			: labels;
	}

	private static async Task<Result<SinkSyncOutcome>> TagAsync(
		ArrSession session,
		ArrTitle title,
		IReadOnlyList<string> labels,
		CancellationToken cancellationToken
	)
	{
		var tags = await session.EnsureTagsAsync(labels, cancellationToken);
		return await tags.Map(ids => RetaggedAsync(session, title, ids, cancellationToken));
	}

	private static async Task<Result<SinkSyncOutcome>> RetaggedAsync(
		ArrSession session,
		ArrTitle title,
		IReadOnlyList<int> tags,
		CancellationToken cancellationToken
	)
	{
		var tagged = await session.RetagAsync(title, tags, ArrTagChange.Add, cancellationToken);
		return tagged.Map(_ => SinkSyncOutcome.Applied);
	}

	private static async Task<Result<SinkSyncOutcome>> CreateAsync(
		ArrSession session,
		IExternalMediaId id,
		ArrInstanceConfig settings,
		IReadOnlyList<string> labels,
		Known known,
		CancellationToken cancellationToken
	)
	{
		var tags = await session.EnsureTagsAsync(labels, cancellationToken);
		return await tags.Map(ids =>
			settings
				.QualityProfileId.Map(profile => settings.RootFolderPath.Map(folder => new ArrAddition(profile, folder, ids)))
				.Match(
					addition => AddNewAsync(session, id, settings, addition, known, cancellationToken),
					() => Error.From($"{session.App.Name} has no quality profile or root folder chosen yet.")
				)
		);
	}

	private static Task<Result<SinkSyncOutcome>> AddNewAsync(
		ArrSession session,
		IExternalMediaId id,
		ArrInstanceConfig settings,
		ArrAddition addition,
		Known known,
		CancellationToken cancellationToken
	)
	{
		var asks = settings.RoutesAnime && !known.Anime && known.MayBeAnimated;
		return known
			.Title.When(_ => !asks)
			.Match(
				title => AddedAsync(session, session.Body(id, title), settings, addition, known.Anime, cancellationToken),
				() => LookUpAndAddAsync(session, id, settings, addition, known.Anime, cancellationToken)
			);
	}

	private static async Task<Result<SinkSyncOutcome>> LookUpAndAddAsync(
		ArrSession session,
		IExternalMediaId id,
		ArrInstanceConfig settings,
		ArrAddition addition,
		bool anime,
		CancellationToken cancellationToken
	)
	{
		var looked = await session.LookupAsync(id, cancellationToken);
		return await looked.Map(found =>
			found.Match(
				title => AddedAsync(session, title, settings, addition, anime || session.IsAnime(title), cancellationToken),
				() => Success.Of(SinkSyncOutcome.Unaddressable)
			)
		);
	}

	private static async Task<Result<SinkSyncOutcome>> AddedAsync(
		ArrSession session,
		JsonObject title,
		ArrInstanceConfig settings,
		ArrAddition addition,
		bool anime,
		CancellationToken cancellationToken
	)
	{
		var route = settings.RoutesAnime && anime ? ArrRoute.Anime : ArrRoute.Default;
		var added = await session.AddAsync(session.Prepare(title, settings, addition.Routed(route, settings)), cancellationToken);
		return added.Map(found => found.Match(SinkSyncOutcome.Applied, SinkSyncOutcome.Unaddressable));
	}

	private Task<Result<SinkSyncOutcome>> RemoveAsync(ArrSession session, IExternalMediaId id, Setup setup, CancellationToken cancellationToken)
	{
		return session.Find(id).Match(title => UntagAsync(session, title, setup, cancellationToken), () => Success.Of(SinkSyncOutcome.Applied));
	}

	private async Task<Result<SinkSyncOutcome>> UntagAsync(ArrSession session, ArrTitle title, Setup setup, CancellationToken cancellationToken)
	{
		var scheme = setup.Config.Tags;
		var mine = session.Mine(scheme, setup.Viewer);
		var wantedByOthers = scheme.PerUser && session.WantedByOthers(title, scheme, mine);
		var removed = await (
			(wantedByOthers, session.IsProtected(title, scheme)) switch
			{
				(true, _) => session.RetagAsync(title, [.. title.Tags.Where(mine.Contains)], ArrTagChange.Remove, cancellationToken),
				(false, true) => session.RetagAsync(title, session.Owned(title, scheme), ArrTagChange.Remove, cancellationToken),
				(false, false) => session.DeleteAsync(title, cancellationToken),
			}
		);
		return removed.Map(_ => SinkSyncOutcome.Applied);
	}

	private async Task<IReadOnlySet<MediaId>> AnimeAsync(
		WantlistOperationAction action,
		ArrSinkConfig config,
		IReadOnlyDictionary<MediaId, IExternalMediaId> addressed,
		CancellationToken cancellationToken
	)
	{
		if (action is not WantlistOperationAction.Add || !config.RoutesAnime)
		{
			return new HashSet<MediaId>();
		}

		var aniList = await mediaService.GetIdsAsync(addressed.Keys, MediaProvider.AniList, cancellationToken);
		var mal = await mediaService.GetIdsAsync(addressed.Keys, MediaProvider.Mal, cancellationToken);
		var mapped = await animeIds.FindManyAsync([.. addressed.Values], cancellationToken);
		return aniList.Keys.Concat(mal.Keys).Concat(addressed.Where(entry => mapped.ContainsKey(entry.Value)).Select(entry => entry.Key)).ToHashSet();
	}

	private async Task<IReadOnlyDictionary<MediaId, MediaDetails>> StoredAsync(
		WantlistOperationAction action,
		IReadOnlyDictionary<MediaId, IExternalMediaId> addressed,
		CancellationToken cancellationToken
	)
	{
		return action is WantlistOperationAction.Add
			? await mediaService.GetStoredDetailsAsync(addressed.Keys, cancellationToken)
			: new Dictionary<MediaId, MediaDetails>();
	}

	private async Task<Optional<ArrTagUser>> ClaimantAsync(ArrTagScheme scheme, CancellationToken cancellationToken)
	{
		var everyone = await users.GetAllAsync(cancellationToken);
		return everyone.OrderBy(user => user.JoinedAt).FirstOrNone().When(_ => scheme.PerUser).Map(Owner);
	}

	private sealed record Setup(
		ArrSinkConfig Config,
		Optional<ArrTagUser> Claimant,
		ArrTagUser Viewer,
		IReadOnlySet<MediaId> Anime,
		IReadOnlyDictionary<MediaId, MediaDetails> Stored
	)
	{
		public Known About(MediaId id) => new(Anime.Contains(id), Stored.GetValueOrNone(id));
	}

	private sealed record Known(bool Anime, Optional<MediaDetails> Details)
	{
		public Optional<string> Title => Details.Map(details => Optional.Of(details.Title));

		public bool MayBeAnimated
		{
			get
			{
				var genres = Details.Map(details => details.Genres).GetValueOr([]);
				return genres.Count == 0 || genres.Contains(Animation, StringComparer.OrdinalIgnoreCase);
			}
		}
	}

	private sealed record Held(ArrSession Session, IReadOnlyList<ArrTitle> Sole, IReadOnlyList<ArrTitle> Staying, ValueSet<int> Only);
}
