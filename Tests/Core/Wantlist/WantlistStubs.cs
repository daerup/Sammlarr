using Contracts;
using Contracts.Announcing;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Core.Announcing;
using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

internal static class WantlistStubs
{
	public static readonly SinkContents NothingOnTheSink = new([], []);

	public static Mock<IMediaSink> EmptySink()
	{
		var sink = IMediaSink.Mock();
		sink.ReadAsync(Any()).Returns(NothingOnTheSink);
		return sink;
	}

	public static WantlistSyncPlanner Planner(Mock<IMediaSink> sink, Mock<IWantlistOutbox> outbox) =>
		new(Active(sink), EmptyRepository().Object, outbox.Object, NullLogger<WantlistSyncPlanner>.Instance);

	public static Mock<IWantlistRepository> EmptyRepository()
	{
		var repository = IWantlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WantlistItemProvenance>(), Any()).Returns([]);
		repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).Returns([]);
		return repository;
	}

	public static Mock<IWantlistOutbox> EmptyOutbox()
	{
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([]);
		return outbox;
	}

	public static Mock<IWantlistOutbox> OutboxPending(IReadOnlyList<MediaId> mediaIds)
	{
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([.. mediaIds.Select(id => new WantlistOutboxEntry(id, WantlistOperationAction.Add, WantlistOutboxState.Pending, 0))]);
		return outbox;
	}

	public static WantlistSyncEngine Engine(
		Mock<IMediaSink> sink,
		Mock<IWantlistOutbox>? outbox = null,
		IReadOnlyDictionary<MediaId, IExternalMediaId>? externals = null,
		Mock<IEventPublisher>? events = null,
		WantlistSyncGate? gate = null,
		ICurrentUser? user = null,
		IMessagePublisher? publisher = null
	)
	{
		outbox ??= EmptyOutbox();
		var owner = user ?? new TestCurrentUser();
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
		var media = IMediaRepository.Mock();
		media.GetExternalsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any()).Returns(externals ?? new Dictionary<MediaId, IExternalMediaId>());

		return new WantlistSyncEngine(
			gate ?? new WantlistSyncGate(),
			Planner(sink, outbox),
			Active(sink),
			EmptyRepository().Object,
			outbox.Object,
			media.Object,
			new Announcer((events ?? IEventPublisher.Mock()).Object, publisher ?? new RecordingPublisher()),
			owner,
			clock.Object,
			NullLogger<WantlistSyncEngine>.Instance
		);
	}

	private static ActiveSink Active(Mock<IMediaSink> sink) =>
		new(ProviderConfigStubs.PlexWantlist().Object, new FakeKeyedServiceIndex<SinkKind, IMediaSink>().Register(SinkKind.Plex, sink.Object));
}
