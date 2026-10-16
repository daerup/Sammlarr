using Domain.Identity;

namespace Contracts.Identity;

public sealed record InvitedAccount(UserId Id, string Username, string DisplayName);
