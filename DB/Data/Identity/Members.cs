using Domain.Identity;

namespace DB.Data.Identity;

internal sealed record Member
{
	public required UserRow Row { get; init; }

	public required string Role { get; init; }
}

internal static class Members
{
	internal static IQueryable<Member> Of(SammlarrDbContext ctx)
	{
		return from user in ctx.Users
			join membership in ctx.UserRoles on user.Id equals membership.UserId
			join role in ctx.Roles on membership.RoleId equals role.Id
			select new Member { Row = user, Role = role.Name! };
	}

	internal static User ToUser(Member member) => ToUser(member.Row, member.Role);

	internal static User ToUser(UserRow row, string role)
	{
		return new User(UserId.From(row.Id), row.DisplayName, row.AvatarUrl, Enum.Parse<UserRole>(role), row.JoinedAt) { TagName = row.TagName };
	}
}
