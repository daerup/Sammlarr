using Contracts.Settings;
using Core.Caching;
using Core.Settings;
using Domain.Identity;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Settings;

public sealed class ArrSinkConfigServiceTests : IDisposable
{
	private static readonly ArrSinkConfig Connected = ArrSinkConfig.Empty with
	{
		Radarr = ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878"), ApiKey = Some.Of("0123456789abcdef0123456789abcdef") },
	};

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<IArrSinkConfigRepository> _repository = IArrSinkConfigRepository.Mock();
	private readonly TestCurrentUser _user = new();

	private readonly ArrSinkConfigService Testee;

	public ArrSinkConfigServiceTests()
	{
		_repository.GetAsync(Any()).Returns(ArrSinkConfig.Empty);
		Testee = new ArrSinkConfigService(_repository.Object, new TypedMemoryCache<string, ArrSinkConfig>(_memory), _user);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetAsync_WhenReadTwice_LoadsTheStoredConfigOnce()
	{
		await Testee.GetAsync();

		await Assert.That(await Testee.GetAsync()).IsEqualTo(ArrSinkConfig.Empty);
		_repository.GetAsync(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_AsAnAdmin_StoresItAndServesIt()
	{
		await Testee.StoreAsync(Connected);

		await Assert.That(await Testee.GetAsync()).IsEqualTo(Connected);
		_repository.StoreAsync(Connected, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_AsAMember_Refuses()
	{
		_user.Role = UserRole.Member;

		await Assert.That(async () => await Testee.StoreAsync(Connected)).Throws<UnauthorizedAccessException>();
		_repository.StoreAsync(Any<ArrSinkConfig>(), Any()).WasCalled(Times.Never);
	}
}
