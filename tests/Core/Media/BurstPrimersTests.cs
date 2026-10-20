using Contracts.Media;
using Contracts.Settings;
using Core.DI;
using Core.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shared.Options;

namespace Core.Tests.Media;

public class BurstPrimersTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
	private static readonly IReadOnlyList<IExternalMediaId> FightClub = [ImdbId.From("tt0137523", MediaType.Movie)];
	private static readonly IReadOnlySet<RemoteService> Mdblist = new HashSet<RemoteService> { RemoteService.Mdblist };
	private static readonly FieldNeeds ShownRatings = new(MediaFields.None, MediaFields.AllRatings);
	private static readonly FieldNeeds RequiredRatings = new(MediaFields.AllRatings, MediaFields.None);
	private static readonly IReadOnlySet<RemoteService> NoIdServices = new HashSet<RemoteService>();

	[Test]
	public async Task PrimeAsync_ForATitleTheServiceAnsweredLately_PrimesNothing()
	{
		var (testee, primer) = Testee(RemoteLoad.Idle);
		var answered = new CachedDetails(
			new MovieDetails { Title = "Fight Club" },
			Now,
			Now,
			[new ProviderAnswer(RemoteService.Mdblist, MediaFields.AllRatings, Now)]
		);

		await testee.PrimeAsync([new PrimedTitle(FightClub, Some.Of(answered))], Run(Mdblist, ShownRatings), NoIdServices, CancellationToken.None);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task PrimeAsync_WhenTheServiceIsNotConnected_PrimesNothing()
	{
		var (testee, primer) = Testee(RemoteLoad.Idle);

		await testee.PrimeAsync([new PrimedTitle(FightClub, new None())], Run(new HashSet<RemoteService>(), ShownRatings), NoIdServices, CancellationToken.None);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task PrimeAsync_ForAGridThatNeedsNoField_PrimesNothingForAStoredTitle()
	{
		var (testee, primer) = Testee(RemoteLoad.Idle);
		var stored = new CachedDetails(new MovieDetails { Title = "Fight Club" }, Now, Now, []);

		await testee.PrimeAsync([new PrimedTitle(FightClub, Some.Of(stored))], Run(Mdblist, FieldNeeds.None), NoIdServices, CancellationToken.None);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	[Arguments(false, 0)]
	[Arguments(true, 1)]
	public async Task PrimeAsync_WhileTheServiceQueuesPastShownWait_PrimesOnlyForFieldsTheLoadRequires(bool required, int primes)
	{
		var (testee, primer) = Testee(RemoteLoad.Idle with { EstimatedWait = TimeSpan.FromSeconds(6) });

		await testee.PrimeAsync(
			[new PrimedTitle(FightClub, new None())],
			Run(Mdblist, required ? RequiredRatings : ShownRatings),
			NoIdServices,
			CancellationToken.None
		);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Exactly(primes));
	}

	[Test]
	public async Task PrimeAsync_ForMixedTitles_PrimesOnlyThoseThatNeedTheService()
	{
		var (testee, primer) = Testee(RemoteLoad.Idle);
		IReadOnlyList<IExternalMediaId> answeredLately = [ImdbId.From("tt0133093", MediaType.Movie)];
		var answered = new CachedDetails(
			new MovieDetails { Title = "The Matrix" },
			Now,
			Now,
			[new ProviderAnswer(RemoteService.Mdblist, MediaFields.AllRatings, Now)]
		);

		await testee.PrimeAsync(
			[new PrimedTitle(FightClub, new None()), new PrimedTitle(answeredLately, Some.Of(answered))],
			Run(Mdblist, ShownRatings),
			NoIdServices,
			CancellationToken.None
		);

		primer
			.PrimeAsync(Is<IReadOnlyCollection<IReadOnlyList<IExternalMediaId>>>(titles => titles!.Single().SequenceEqual(FightClub)), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task PrimeAsync_ForAStaleTitleWhoseAnswerHasRun_PrimesItEvenForAFieldItHolds()
	{
		var (testee, primer) = Testee(RemoteLoad.Idle, MediaField.Title);
		var stale = new CachedDetails(
			new MovieDetails { Title = "Fight Club" },
			Now.AddDays(-20),
			Now.AddDays(-20),
			[new ProviderAnswer(RemoteService.Mdblist, MediaFields.None, Now.AddDays(-20))]
		);

		await testee.PrimeAsync([new PrimedTitle(FightClub, Some.Of(stale))], Run(Mdblist, FieldNeeds.None), NoIdServices, CancellationToken.None);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PrimeAsync_WhenTheServiceIsBlockedPastItsEnrichWait_PrimesNothingEvenForRequiredFields()
	{
		var (testee, primer) = Testee(RemoteLoad.Idle with { BlockedUntil = Some.Of(Now.AddSeconds(10)) });

		await testee.PrimeAsync([new PrimedTitle(FightClub, new None())], Run(Mdblist, RequiredRatings), NoIdServices, CancellationToken.None);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task PrimeAsync_ForAConnectedIdService_PrimesEveryTitleWithoutStoredDetails()
	{
		var anime = IBurstPrimer.Mock();
		anime.Remote.Returns(RemoteService.AnimeLists);
		var (testee, _) = Testee(RemoteLoad.Idle, MediaFields.AllRatings, anime.Object);
		var answered = new CachedDetails(new MovieDetails { Title = "Fight Club" }, Now, Now, []);

		await testee.PrimeAsync(
			[new PrimedTitle(FightClub, new None()), new PrimedTitle([ImdbId.From("tt0133093", MediaType.Movie)], Some.Of(answered))],
			Run(Mdblist, FieldNeeds.None),
			new HashSet<RemoteService> { RemoteService.AnimeLists },
			CancellationToken.None
		);

		anime
			.PrimeAsync(Is<IReadOnlyCollection<IReadOnlyList<IExternalMediaId>>>(titles => titles!.Single().SequenceEqual(FightClub)), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task PrimeAsync_ForAnIdServiceThatIsNotConnected_PrimesNothing()
	{
		var anime = IBurstPrimer.Mock();
		anime.Remote.Returns(RemoteService.AnimeLists);
		var (testee, _) = Testee(RemoteLoad.Idle, MediaFields.AllRatings, anime.Object);

		await testee.PrimeAsync([new PrimedTitle(FightClub, new None())], Run(Mdblist, FieldNeeds.None), NoIdServices, CancellationToken.None);

		anime.PrimeAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task PrimeAsync_WhenOnePrimerFails_StillPrimesTheOthers()
	{
		var anime = IBurstPrimer.Mock();
		anime.Remote.Returns(RemoteService.AnimeLists);
		anime.PrimeAsync(Any(), Any()).Returns(() => Task.FromException(new InvalidOperationException("the map is locked")));
		var (testee, primer) = Testee(RemoteLoad.Idle, MediaFields.AllRatings, anime.Object);

		await testee.PrimeAsync(
			[new PrimedTitle(FightClub, new None())],
			Run(Mdblist, ShownRatings),
			new HashSet<RemoteService> { RemoteService.AnimeLists },
			CancellationToken.None
		);

		primer.PrimeAsync(Any(), Any()).WasCalled(Times.Once);
	}

	private static DetailsRun Run(IReadOnlySet<RemoteService> connected, FieldNeeds needs)
	{
		return new DetailsRun(connected, needs, new LibraryWriter(IMediaRepository.Mock().Object, NullLogger<LibraryWriter>.Instance));
	}

	private static (BurstPrimers Testee, Mock<IBurstPrimer> Primer) Testee(RemoteLoad load) => Testee(load, MediaFields.AllRatings);

	private static (BurstPrimers Testee, Mock<IBurstPrimer> Primer) Testee(RemoteLoad load, MediaFields fills, params IBurstPrimer[] others)
	{
		var enricher = IDetailsEnricher.Mock();
		enricher.Fills.Returns(fills);
		enricher.Accepts.Returns([MediaProvider.Imdb]);
		var clock = new FakeTimeProvider(Now);
		var capacity = TestCapacity.With(_ => load);
		var details = new DetailsFetcher(
			new KeyedServiceSet<RemoteService, IDetailsEnricher>([new(RemoteService.Mdblist, enricher.Object)]),
			TestClaims.Granting().Object,
			IProviderConfigService.Mock().Object,
			capacity,
			TestReadiness.Everything(),
			[TestArtwork.ResizesAnything()],
			clock,
			new FeedLoadOptions(),
			new HousekeepingOptions(),
			NullLogger<DetailsFetcher>.Instance
		);
		var primer = IBurstPrimer.Mock();
		primer.Remote.Returns(RemoteService.Mdblist);
		return (new BurstPrimers([.. others, primer.Object], details, capacity, clock, new FeedLoadOptions()), primer);
	}
}
