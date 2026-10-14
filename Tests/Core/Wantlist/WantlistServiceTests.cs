using Contracts.Announcing;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Core.Announcing;
using Core.Tests.Settings;
using Core.Wantlist;
using Domain.Feed;
using Domain.Media;
using Domain.Usage;
using Domain.Wantlist;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

public class WantlistServiceTests
{
	private static readonly MediaId Title = MediaId.From(new Guid(31, 0, 0, new byte[8]));

	private readonly Mock<IMediaService> _media = IMediaService.Mock();

	private UsageCause _cause = UsageCause.Other;

	[Test]
	public async Task AddAsync_FromAPage_LooksUpTheSinkIdsUnderTheWantlistSyncCause()
	{
		await Testee().AddAsync([Title], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		await Assert.That(_cause).IsEqualTo(UsageCause.WantlistSync);
	}

	[Test]
	public async Task AddAsync_InsideAFeedRun_LooksUpTheSinkIdsUnderTheRunsCause()
	{
		using var run = UsageCauses.Mark(FeedId.From(Guid.NewGuid()));

		await Testee().AddAsync([Title], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		await Assert.That(_cause).IsEqualTo(UsageCause.FeedRun);
	}

	private WantlistService Testee()
	{
		_media.EnsureSinkIdsAsync(Any(), Any(), Any()).Returns(Array.Empty<MediaId>()).Callback(() => _cause = UsageCauses.Current.Cause);
		var repository = IWantlistRepository.Mock();
		repository.GetAllAsync(Any()).Returns([]);
		repository.AddAsync(Any(), Any(), Any()).Returns((ids, _, _) => [.. ids]);
		var outbox = IWantlistOutbox.Mock();
		outbox.GetAllAsync(Any(), Any()).Returns([]);
		return new WantlistService(
			repository.Object,
			outbox.Object,
			_media.Object,
			ProviderConfigStubs.PlexWantlist().Object,
			new Announcer(IEventPublisher.Mock().Object, new RecordingPublisher()),
			new TestCurrentUser()
		);
	}
}
