using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using API.Arr.Types;
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
	ArrClients clients,
	IArrSinkConfigService configs,
	IUserRepository users,
	IMediaService mediaService,
	ICurrentUser currentUser,
	ILogger<ArrSink> logger
) : IMediaSink
{
	public SinkKind Kind => SinkKind.Arr;

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
		var viewer = await ViewerAsync(cancellationToken);
		var claims = (await ClaimantAsync(config.Tags, cancellationToken)).Match(first => first.Id == viewer.Id, () => false);
		List<FoundMedia> items = [];
		foreach (var app in Enum.GetValues<ArrApp>())
		{
			items.AddRange(await MineAsync(clients.For(app), config, viewer, claims, cancellationToken));
		}

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
		var setup = new Setup(config, await ClaimantAsync(config.Tags, cancellationToken), await ViewerAsync(cancellationToken));
		var sinkIds = await mediaService.GetSinkIdsAsync([.. pushes.Select(push => push.MediaId)], cancellationToken);
		var sessions = new Dictionary<ArrApp, Result<ArrSession>>();
		foreach (var push in pushes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var outcome = await sinkIds
				.GetValueOrNone(push.MediaId)
				.Match(id => PushAsync(action, push, id, setup, sessions, cancellationToken), () => Task.FromResult(SinkSyncOutcome.Unaddressable));
			yield return new SinkSyncAck(push.MediaId, outcome);
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
		return await held.Match(apps => ClearAllAsync(apps, cancellationToken), error => Task.FromResult<Result<int>>(error));
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
		foreach (var app in Enum.GetValues<ArrApp>())
		{
			var session = await ArrSession.OpenAsync(clients.For(app), ArrSession.Connect(app, config.Of(app)), cancellationToken);
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
		var sole = titles.Where(title => title.Tags.Any(only.Contains) && !session.IsKept(title, scheme) && !session.WantedByOthers(title, scheme, only)).ToList();
		return new Held(session, sole, [.. titles.Except(sole)], only);
	}

	private static async Task<Result<int>> ClearAllAsync(IReadOnlyList<Held> apps, CancellationToken cancellationToken)
	{
		Result<int> deleted = Success.Of(0);
		foreach (var app in apps)
		{
			deleted = await deleted.Match(count => ClearAsync(app, count, cancellationToken), error => Task.FromResult<Result<int>>(error));
		}

		return deleted;
	}

	private static async Task<Result<int>> ClearAsync(Held app, int before, CancellationToken cancellationToken)
	{
		Result<int> deleted = app.Sole.Count == 0 ? Success.Of(0) : await app.Session.DeleteAsync(app.Sole, cancellationToken);
		return await deleted.Match(gone => UntagAsync(app, before + gone, cancellationToken), error => Task.FromResult<Result<int>>(AlreadyDeleted(error, before)));
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

	private async Task<IReadOnlyList<FoundMedia>> MineAsync(
		ArrClient client,
		ArrSinkConfig config,
		ArrTagUser viewer,
		bool claims,
		CancellationToken cancellationToken
	)
	{
		var opened = await ArrSession.OpenAsync(client, ArrSession.Connect(client.App, config.Of(client.App)), cancellationToken);
		var session = opened.Match(open => open, error => throw new InvalidOperationException(error.Reason));
		var mine = session.Mine(config.Tags, viewer);
		return
		[
			.. session
				.Titles.Where(title => title.Tags.Any(mine.Contains) || (claims && session.IsUnclaimed(title, config.Tags)))
				.Select(title => ArrFound.Of(client.App, title)),
		];
	}

	private async Task<SinkSyncOutcome> PushAsync(
		WantlistOperationAction action,
		SinkPush push,
		IExternalMediaId id,
		Setup setup,
		Dictionary<ArrApp, Result<ArrSession>> sessions,
		CancellationToken cancellationToken
	)
	{
		var app = id.Type is MediaType.Show ? ArrApp.Sonarr : ArrApp.Radarr;
		if (!sessions.TryGetValue(app, out var session))
		{
			session = await ArrSession.OpenAsync(clients.For(app), ArrSession.Connect(app, setup.Config.Of(app)), cancellationToken);
			sessions[app] = session;
		}

		var pushed = await session.Match(open => PushToAsync(open, action, push, id, setup, cancellationToken), Failed);
		return pushed
			.ApplyError(error => logger.LogWarning("Pushing {Action} for {ExternalId} to {App} failed: {Reason}", action, id, app.Name, error.Reason))
			.GetValueOr(SinkSyncOutcome.Failed);
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
		var feed = push.AddedBy.Map(added => new ArrTagFeed(added.Id.Value, added.Name, added.Identifier));
		var labels = setup.Config.Tags.Labels(setup.Viewer, feed);
		return await session
			.Find(id)
			.Match(
				existing => TagAsync(session, existing, Labeling(session, existing, setup, labels), cancellationToken),
				() => CreateAsync(session, id, setup.Config.Of(session.App), labels, cancellationToken)
			);
	}

	private static IReadOnlyList<string> Labeling(ArrSession session, ArrTitle title, Setup setup, IReadOnlyList<string> labels)
	{
		var scheme = setup.Config.Tags;
		if (session.Owned(title, scheme).Count == 0)
		{
			return [.. labels, scheme.KeptTag];
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
		return await tags.Match(ids => RetaggedAsync(session, title, ids, cancellationToken), Failed);
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
		CancellationToken cancellationToken
	)
	{
		var tags = await session.EnsureTagsAsync(labels, cancellationToken);
		return await tags.Match(
			ids =>
				settings
					.QualityProfileId.Map(profile => settings.RootFolderPath.Map(folder => new ArrAddition(profile, folder, ids)))
					.Match(
						addition => LookUpAndAddAsync(session, id, settings, addition, cancellationToken),
						() => Failed(Error.From($"{session.App.Name} has no quality profile or root folder chosen yet."))
					),
			Failed
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

	private Task<Result<SinkSyncOutcome>> RemoveAsync(ArrSession session, IExternalMediaId id, Setup setup, CancellationToken cancellationToken)
	{
		return session.Find(id).Match(title => UntagAsync(session, title, setup, cancellationToken), () => Done(SinkSyncOutcome.Applied));
	}

	private async Task<Result<SinkSyncOutcome>> UntagAsync(ArrSession session, ArrTitle title, Setup setup, CancellationToken cancellationToken)
	{
		var scheme = setup.Config.Tags;
		var mine = session.Mine(scheme, setup.Viewer);
		var wantedByOthers = scheme.PerUser && session.WantedByOthers(title, scheme, mine);
		var removed = await (
			(wantedByOthers, session.IsKept(title, scheme)) switch
			{
				(true, _) => session.RetagAsync(title, [.. title.Tags.Where(mine.Contains)], ArrTagChange.Remove, cancellationToken),
				(false, true) => session.RetagAsync(title, session.Owned(title, scheme), ArrTagChange.Remove, cancellationToken),
				(false, false) => session.DeleteAsync(title, cancellationToken),
			}
		);
		return removed.Map(_ => SinkSyncOutcome.Applied);
	}

	private async Task<Optional<ArrTagUser>> ClaimantAsync(ArrTagScheme scheme, CancellationToken cancellationToken)
	{
		var everyone = await users.GetAllAsync(cancellationToken);
		return everyone.OrderBy(user => user.JoinedAt).FirstOrNone(_ => true).When(_ => scheme.PerUser).Map(Owner);
	}

	private static Task<Result<SinkSyncOutcome>> Done(SinkSyncOutcome outcome) => Task.FromResult<Result<SinkSyncOutcome>>(Success.Of(outcome));

	private static Task<Result<SinkSyncOutcome>> Failed(Error error) => Task.FromResult<Result<SinkSyncOutcome>>(error);

	private sealed record Setup(ArrSinkConfig Config, Optional<ArrTagUser> Claimant, ArrTagUser Viewer);

	private sealed record Held(ArrSession Session, IReadOnlyList<ArrTitle> Sole, IReadOnlyList<ArrTitle> Staying, ValueSet<int> Only);
}
