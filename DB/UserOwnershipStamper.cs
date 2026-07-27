using DB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB;

internal sealed class UserOwnershipStamper
{
	public void Stamp(EntityEntry<IUserOwned> entry, Guid userId)
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
