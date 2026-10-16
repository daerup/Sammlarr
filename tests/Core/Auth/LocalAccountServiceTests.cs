using Contracts.Identity;
using Core.Auth;
using Domain.Identity;
using Microsoft.Extensions.Logging;
using TestInfrastructure;

namespace Core.Tests.Auth;

public class LocalAccountServiceTests
{
	private static readonly User Member = new(UserId.New(), "Mira", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch);

	private readonly Mock<ILocalAccountRepository> _accounts = ILocalAccountRepository.Mock();
	private readonly Mock<ILogger<LocalAccountService>> _logger = ILogger<LocalAccountService>.Mock();

	private LocalAccountService Testee
	{
		get
		{
			var currentUser = ICurrentUser.Mock();
			currentUser.Id.Returns(Member.Id);
			return new LocalAccountService(_accounts.Object, currentUser.Object, ISessionInvalidator.Mock().Object, _logger.Object);
		}
	}

	[Test]
	[Arguments("The current password is not right.")]
	[Arguments(Lockout.Message)]
	public async Task ChangePasswordAsync_WhenTheChangeIsRefused_WarnsWithTheReason(string reason)
	{
		_accounts.GetAsync(Any(), Any()).Returns(Some.Of(new LocalAccount(Member, "mira", new None())));
		_accounts.ChangePasswordAsync(Any(), Any(), Any(), Any()).Returns(Error.From(reason));

		var changed = await Testee.ChangePasswordAsync("wrong", "a new password");

		await Assert.That(changed).IsError();
		await Assert.That(Warnings()).IsEquivalentTo([$"Password change refused for user {Member.Id}: {reason}"]);
	}

	private IEnumerable<string> Warnings()
	{
		return Mock.Invocations(_logger).Where(call => call.Arguments[0] is LogLevel.Warning).Select(call => $"{call.Arguments[2]}");
	}
}
