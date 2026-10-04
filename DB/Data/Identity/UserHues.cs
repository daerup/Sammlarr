using Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Identity;

internal static class UserHues
{
	private static readonly SemaphoreSlim Gate = new(1, 1);

	internal static async Task AddAsync(SammlarrDbContext ctx, UserRow row, CancellationToken cancellationToken)
	{
		await Gate.WaitAsync(cancellationToken);
		try
		{
			var last = await ctx
				.Users.AsNoTracking()
				.OrderByDescending(user => user.JoinedAt)
				.ThenByDescending(user => user.Id)
				.Select(user => (double?)user.Hue)
				.FirstOrDefaultAsync(cancellationToken);
			row.Hue = PersonHues.After(Optional.Of(last));
			ctx.Users.Add(row);
			await ctx.SaveChangesAsync(cancellationToken);
		}
		finally
		{
			Gate.Release();
		}
	}
}
