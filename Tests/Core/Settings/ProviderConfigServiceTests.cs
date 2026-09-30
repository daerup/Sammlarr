using Contracts.Settings;
using Core.Caching;
using Core.Settings;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Settings;

public sealed class ProviderConfigServiceTests : IDisposable
{
	private static readonly ProviderConfig Stored = Config(RegistrationMode.Open);

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<IProviderConfigRepository> _repository = IProviderConfigRepository.Mock();
	private readonly TestCurrentUser _user = new();

	private readonly ProviderConfigService Testee;

	public ProviderConfigServiceTests()
	{
		_repository.GetAsync(Any()).Returns(Stored);
		Testee = new ProviderConfigService(_repository.Object, new TypedMemoryCache<string, ProviderConfig>(_memory), _user);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetAsync_WhenReadTwice_LoadsTheStoredConfigOnce()
	{
		await Testee.GetAsync();

		await Assert.That(await Testee.GetAsync()).IsEqualTo(Stored);
		_repository.GetAsync(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_AsAnAdmin_StoresItAndServesItWithoutReadingItBack()
	{
		var closed = Config(RegistrationMode.Closed);

		await Testee.StoreAsync(closed);

		await Assert.That(await Testee.GetAsync()).IsEqualTo(closed);
		_repository.StoreAsync(closed, Any()).WasCalled(Times.Once);
		_repository.GetAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task StoreAsync_AsAMember_RefusesAndLeavesTheStoredConfigAlone()
	{
		_user.Role = UserRole.Member;

		await Assert.That(async () => await Testee.StoreAsync(Config(RegistrationMode.Closed))).Throws<UnauthorizedAccessException>();
		_repository.StoreAsync(Any<ProviderConfig>(), Any()).WasCalled(Times.Never);
	}

	private static ProviderConfig Config(RegistrationMode registration) =>
		new()
		{
			Sink = SinkKind.Plex,
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Plex,
			RatingProvider = RatingProvider.Tmdb,
			AuthProvider = AuthProvider.Plex,
			RegistrationMode = registration,
		};
}
