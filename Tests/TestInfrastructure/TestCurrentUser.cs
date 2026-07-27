using Contracts.Identity;
using Domain.Identity;

namespace TestInfrastructure;

public sealed class TestCurrentUser : ICurrentUser
{
	public static readonly UserId DefaultId = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

	public bool IsAuthenticated { get; set; } = true;
	public UserId Id { get; set; } = DefaultId;
	public string DisplayName { get; set; } = "Test User";
	public string AvatarUrl { get; set; } = string.Empty;
	public UserRole Role { get; set; } = UserRole.Owner;
}
