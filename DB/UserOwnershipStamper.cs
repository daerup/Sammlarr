using DB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB;

internal static class UserOwnershipStamper
{
	public static void Stamp(EntityEntry<IUserOwned> entry, Guid userId)
	{
		var userIdProperty = entry.Property(UserOwnedConfigurationExtensions.UserIdColumn);
		if (entry.State is EntityState.Added)
		{
			userIdProperty.CurrentValue = userId;
		}
		else
		{
			userIdProperty.IsModified = false;
		}
	}
}
