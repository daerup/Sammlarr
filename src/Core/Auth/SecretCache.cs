using Contracts.Caching;
using Contracts.Identity;
using Domain.Auth;

namespace Core.Auth;

internal sealed class SecretCache<TProvider, TSecret>(
	ITypedMemoryCache<(Guid User, TProvider Provider), TSecret> cache,
	ITypedMemoryCache<(Guid User, TProvider Provider), AccessCheck> rejections,
	ICurrentUser currentUser
)
	where TProvider : struct, Enum
	where TSecret : class
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

	public async ValueTask<Result<TSecret>> GetOrLoadAsync(TProvider provider, Func<Task<Result<TSecret>>> load, Func<TSecret, Task<AccessCheck>> check)
	{
		if (IsRejected(provider))
		{
			return Refused($"The stored {provider} secret was rejected.");
		}

		return await cache.GetValueOrNone(Key(provider)).Match(cached => Success.Of(cached), () => load().Map(secret => CheckedAsync(provider, secret, check)));
	}

	public bool IsRejected(TProvider provider) => rejections.GetValueOrNone(Key(provider)).IsSome();

	public void Put(TProvider provider, TSecret secret)
	{
		rejections.Remove(Key(provider));
		cache.Set(Key(provider), secret, Lifetime);
	}

	public void Reject(TProvider provider)
	{
		cache.Remove(Key(provider));
		rejections.Set(Key(provider), AccessCheck.Rejected);
	}

	public void Evict(TProvider provider)
	{
		cache.Remove(Key(provider));
		rejections.Remove(Key(provider));
	}

	private async Task<Result<TSecret>> CheckedAsync(TProvider provider, TSecret secret, Func<TSecret, Task<AccessCheck>> check)
	{
		var outcome = await check(secret);
		return outcome switch
		{
			AccessCheck.Valid => Kept(provider, secret),
			AccessCheck.Rejected => Rejected(provider),
			AccessCheck.Unreachable or AccessCheck.Spent => Success.Of(secret),
		};
	}

	private Result<TSecret> Kept(TProvider provider, TSecret secret)
	{
		Put(provider, secret);
		return Success.Of(secret);
	}

	private Result<TSecret> Rejected(TProvider provider)
	{
		Reject(provider);
		return Refused($"{provider} rejected the stored secret.");
	}

	private static Error Refused(string reason) => Error.From(reason, new SecretRejectedException(reason));

	private (Guid User, TProvider Provider) Key(TProvider provider)
	{
		return (currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty, provider);
	}
}
