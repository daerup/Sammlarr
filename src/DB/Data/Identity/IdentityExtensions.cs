using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Data.Identity;

internal static class IdentityExtensions
{
	internal static async Task<T> InTransactionAsync<T>(this IServiceScope scope, Func<UserManager<UserRow>, Task<T>> work, CancellationToken cancellationToken)
	{
		await using var transaction = await scope.ServiceProvider.GetRequiredService<SammlarrDbContext>().Database.BeginTransactionAsync(cancellationToken);
		var result = await work(scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>());
		await transaction.CommitAsync(cancellationToken);
		return result;
	}

	internal static async Task InTransactionAsync(this IServiceScope scope, Func<UserManager<UserRow>, Task> work, CancellationToken cancellationToken)
	{
		await using var transaction = await scope.ServiceProvider.GetRequiredService<SammlarrDbContext>().Database.BeginTransactionAsync(cancellationToken);
		await work(scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>());
		await transaction.CommitAsync(cancellationToken);
	}

	internal static async Task<UserRow> RequireAsync(this UserManager<UserRow> users, UserId id)
	{
		return await users.FindByIdAsync(id.Value.ToString()) ?? throw new InvalidOperationException($"There is no user {id.Value}.");
	}

	internal static void Ensure(this IdentityResult result)
	{
		if (!result.Succeeded)
		{
			throw new InvalidOperationException(Describe(result));
		}
	}

	internal static Result<T> ToResult<T>(this IdentityResult result, T value)
		where T : notnull
	{
		return result.Succeeded ? Success.Of(value) : Error.From(Describe(result));
	}

	private static string Describe(IdentityResult result) => string.Join(" ", result.Errors.Select(error => error.Description));
}
