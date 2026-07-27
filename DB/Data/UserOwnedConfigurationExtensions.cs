using DB.Data.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DB.Data;

internal static class UserOwnedConfigurationExtensions
{
	public const string UserIdColumn = "UserId";

	public static EntityTypeBuilder<T> HasUserOwner<T>(this EntityTypeBuilder<T> builder)
		where T : class, IUserOwned
	{
		builder.Property<Guid>(UserIdColumn);
		builder.HasOne<UserRow>().WithMany().HasForeignKey(UserIdColumn).OnDelete(DeleteBehavior.Cascade);
		return builder;
	}
}
