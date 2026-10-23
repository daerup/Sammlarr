using Contracts.Identity;
using Contracts.Settings;
using Core.Caching;
using Core.Settings;
using Domain.Identity;
using Domain.Wantlist.Arr;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Settings;

public sealed class ArrSinkConfigServiceTests : IDisposable
{
	private static readonly ArrSinkConfig Connected = ArrSinkConfig.Empty with
	{
		Radarr = ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878"), ApiKey = Some.Of(new Secret("0123456789abcdef0123456789abcdef")) },
	};

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<IArrSinkConfigRepository> _repository = IArrSinkConfigRepository.Mock();
	private readonly Mock<IUserRepository> _users = IUserRepository.Mock();
	private readonly TestCurrentUser _user = new();

	private readonly ArrSinkConfigService Testee;

	public ArrSinkConfigServiceTests()
	{
		_repository.GetAsync(Any()).Returns(ArrSinkConfig.Empty);
		_users.GetAllAsync(Any()).Returns([Member("Guest")]);
		Testee = new ArrSinkConfigService(_repository.Object, new TypedMemoryCache<string, ArrSinkConfig>(_memory), _users.Object, _user);
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
	public async Task GetAsync_OnAOneUserInstall_KeepsUserTagsOff()
	{
		_repository.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Tags = ArrTagScheme.Default with { PerUser = false } });

		await Assert.That((await Testee.GetAsync()).Tags.PerUser).IsFalse();
	}

	[Test]
	public async Task GetAsync_OnceASecondUserSyncs_TurnsUserTagsBackOn()
	{
		_repository.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Tags = ArrTagScheme.Default with { PerUser = false } });
		_users.GetAllAsync(Any()).Returns([Member("Guest"), Member("Daerup")]);

		await Assert.That((await Testee.GetAsync()).Tags.PerUser).IsTrue();
	}

	[Test]
	public async Task StoreAsync_AsAMember_Refuses()
	{
		_user.Role = UserRole.Member;

		await Assert.That(async () => await Testee.StoreAsync(Connected)).Throws<UnauthorizedAccessException>();
		_repository.StoreAsync(Any<ArrSinkConfig>(), Any()).WasCalled(Times.Never);
	}

	private static User Member(string name) => new(UserId.New(), name, string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch);
}
