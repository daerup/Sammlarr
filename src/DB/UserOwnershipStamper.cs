using System.Diagnostics.CodeAnalysis;
using DB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DB;

internal sealed class UserOwnershipStamper
{
	[SuppressMessage("Performance", "CA1822", Justification = "The stamper is an injected service, so the DB context takes it as an instance.")]
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
