using Contracts;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Wantlist;
using Core.Identity;
using Core.Wantlist.Sync;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Domain.Usage;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Identity;

public class UserManagementServiceTests
{
	private static readonly User Mira = new(UserId.New(), "mira", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch);

	private readonly Mock<IUserRepository> _users = IUserRepository.Mock();

	private readonly Mock<IMediaSink> _sink = IMediaSink.Mock();

	private UserManagementService Testee
	{
		get
		{
			var config = IProviderConfigService.Mock();
			config
				.GetAsync(Any())
				.Returns(
					new ProviderConfig
					{
						Sink = SinkKind.Arr,
						TrendingProvider = TrendingProvider.Tmdb,
						SearchProvider = SearchProvider.Plex,
						AuthProvider = AuthProvider.Local,
						RegistrationMode = RegistrationMode.Closed,
					}
				);
			var validator = ISessionValidator.Mock();
			validator.IsCurrentAsync(Any(), Any(), Any()).Returns(true);
			return new UserManagementService(
				_users.Object,
				ILocalAccountRepository.Mock().Object,
				new TestCurrentUser(),
				config.Object,
				validator.Object,
				ISessionInvalidator.Mock().Object,
				new ActiveSink(config.Object, new FakeKeyedServiceIndex<SinkKind, IMediaSink>().Register(SinkKind.Arr, _sink.Object)),
				IDateTimeProvider.Mock().Object
			);
		}
	}

	[Before(Test)]
	public void People() => Holding(Mira);

	[Test]
	public async Task DeleteWithSinkTitlesAsync_WhenTheSinkClearsTheirTitles_RemovesThemAndSaysHowMany()
	{
		_sink.DeleteTitlesOfAsync(Any(), Any()).Returns(Success.Of(3));

		var removed = await Testee.DeleteWithSinkTitlesAsync(Mira.Id);

		await Assert.That(removed).IsSuccess().And.IsEqualTo(3);
		_users.DeleteAsync(Mira.Id, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task DeleteWithSinkTitlesAsync_ClearsTheSinkUnderTheWantlistSyncCause()
	{
		var cause = UsageCause.Other;
		_sink.DeleteTitlesOfAsync(Any(), Any()).Returns(Success.Of(3)).Callback(() => cause = UsageCauses.Current.Cause);

		await Testee.DeleteWithSinkTitlesAsync(Mira.Id);

		await Assert.That(cause).IsEqualTo(UsageCause.WantlistSync);
	}

	[Test]
	public async Task SinkHoldingsAsync_ReadsTheSinkUnderTheWantlistSyncCause()
	{
		var cause = UsageCause.Other;
		_sink.HoldingsOfAsync(Any(), Any()).Returns(Error.From("Radarr answered 500.")).Callback(() => cause = UsageCauses.Current.Cause);

		await Testee.SinkHoldingsAsync(Mira.Id);

		await Assert.That(cause).IsEqualTo(UsageCause.WantlistSync);
	}

	[Test]
	public async Task DeleteWithSinkTitlesAsync_WhenTheSinkFails_KeepsThePerson()
	{
		_sink.DeleteTitlesOfAsync(Any(), Any()).Returns(Error.From("Sonarr answered 500."));

		var removed = await Testee.DeleteWithSinkTitlesAsync(Mira.Id);

		await Assert.That(removed).IsError();
		_users.DeleteAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task DeleteWithSinkTitlesAsync_ForTheLastAdmin_TouchesNothing()
	{
		Holding(Mira with { Role = UserRole.Admin });

		var removed = await Testee.DeleteWithSinkTitlesAsync(Mira.Id);

		await Assert.That(removed).IsError();
		_sink.DeleteTitlesOfAsync(Any(), Any()).WasCalled(Times.Never);
		_users.DeleteAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task GetColourRosterAsync_ForAMember_PairsEveryoneWithTheirStoredColourOrNone()
	{
		var ada = new User(UserId.New(), "ada", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch.AddDays(1));
		var painted = new StoredColour(Some.Of(92.5), new None());
		_users.GetAllAsync(Any()).Returns([Mira, ada]);
		_users.GetColoursAsync(Any()).Returns(new Dictionary<UserId, StoredColour> { [ada.Id] = painted });

		var roster = await Testee.GetColourRosterAsync();

		await Assert.That(roster).IsEquivalentTo([new RosterColour(Mira.Id, Mira.JoinedAt, StoredColour.Empty), new RosterColour(ada.Id, ada.JoinedAt, painted)]);
	}

	private void Holding(User person)
	{
		_users.GetAsync(Mira.Id, Any()).Returns(person);
		_users.GetAllAsync(Any()).Returns([person]);
	}
}
