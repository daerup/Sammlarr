using Contracts.Announcing;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Core.Announcing;
using Core.Tests.Settings;
using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

internal static class WantlistStubs
{
	public static readonly SinkSnapshot NothingOnTheSink = new([], []);

	public static Mock<IMediaSink> EmptySink()
	{
		var sink = IMediaSink.Mock();
		sink.GetAsync(Any()).Returns(NothingOnTheSink);
		return sink;
	}

	public static Mock<IMediaSink> SinkAcking(params SinkSyncAck[] acks)
	{
		var sink = EmptySink();
		sink.ApplyAsync(Any(), Any(), Any()).Returns(acks.ToAsyncEnumerable());
		return sink;
	}

	public static WantlistSyncPlanner Planner(
		Mock<IMediaSink> sink,
		Mock<IWantlistOutbox> outbox,
		ILogger<WantlistSyncPlanner>? logger = null,
		Mock<IWantlistRepository>? repository = null,
		Mock<IMediaService>? media = null
	)
	{
		return new WantlistSyncPlanner(
			Active(sink),
			(repository ?? EmptyRepository()).Object,
			outbox.Object,
			(media ?? IMediaService.Mock()).Object,
			logger ?? NullLogger<WantlistSyncPlanner>.Instance
		);
	}

	public static Mock<IMediaService> Typed(params (MediaId Id, MediaType Type)[] titles)
	{
		var media = IMediaService.Mock();
		media.GetTypesAsync(Any(), Any()).Returns(titles.ToDictionary(title => title.Id, title => title.Type));
		return media;
	}

	public static WantlistSnapshot Wanted(IEnumerable<MediaId> ids) =>
		new([.. ids.Select(id => new WantlistItem(id, DateTimeOffset.UnixEpoch, new ManualOrigin(ManualSource.Unknown)))]);

	public static Mock<IWantlistRepository> EmptyRepository()
	{
		var repository = IWantlistRepository.Mock();
		repository.GetAllAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WantlistItemProvenance>(), Any()).Returns([]);
		repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).Returns([]);
		return repository;
	}

	public static Mock<IWantlistOutbox> EmptyOutbox()
	{
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAllAsync(Any(), Any()).Returns([]);
		return outbox;
	}

	public static Mock<IWantlistOutbox> OutboxPending(IReadOnlyList<MediaId> mediaIds)
	{
		var outbox = IWantlistOutbox.Mock();
		outbox
			.GetAllAsync(Any(), Any())
			.Returns([.. mediaIds.Select(id => new WantlistOutboxEntry(id, WantlistOperationAction.Add, WantlistOutboxState.Pending, 0))]);
		return outbox;
	}

	public static WantlistSyncEngine Engine(
		Mock<IMediaSink> sink,
		Mock<IWantlistOutbox>? outbox = null,
		Mock<IEventPublisher>? events = null,
		WantlistSyncGate? gate = null,
		ICurrentUser? user = null,
		IMessagePublisher? publisher = null,
		Mock<IWantlistRepository>? repository = null,
		Mock<IMediaService>? media = null
	)
	{
		outbox ??= EmptyOutbox();
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

		return new WantlistSyncEngine(
			gate ?? new WantlistSyncGate(),
			Planner(sink, outbox, media: media),
			Active(sink),
			(repository ?? EmptyRepository()).Object,
			outbox.Object,
			new Announcer((events ?? IEventPublisher.Mock()).Object, publisher ?? new RecordingPublisher()),
			user ?? new TestCurrentUser(),
			clock,
			NullLogger<WantlistSyncEngine>.Instance
		);
	}

	private static ActiveSink Active(Mock<IMediaSink> sink) =>
		new(ProviderConfigStubs.PlexWantlist().Object, new FakeKeyedServiceIndex<SinkKind, IMediaSink>().Register(SinkKind.Plex, sink.Object));
}
