using Contracts.Caching;
using Contracts.Identity;

namespace Core.Auth;

internal sealed class SecretCache<TProvider, TSecret>(ITypedMemoryCache<(Guid User, TProvider Provider), TSecret> cache, ICurrentUser currentUser)
	where TProvider : struct, Enum
	where TSecret : class
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

	public async ValueTask<Result<TSecret>> GetOrLoadAsync(TProvider provider, Func<Task<Result<TSecret>>> load) =>
		cache.TryGetValue(Key(provider), out var cached) && cached is not null ? Success.Of(cached) : await load().Apply(secret => Put(provider, secret));

	public void Put(TProvider provider, TSecret secret) => cache.Set(Key(provider), secret, Lifetime);

	public void Evict(TProvider provider) => cache.Remove(Key(provider));

	private (Guid User, TProvider Provider) Key(TProvider provider) => (currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty, provider);
}
