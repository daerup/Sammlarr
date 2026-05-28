using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaQuerySession(SammlarrDbContext context) : IMediaQuerySession
{
	public SammlarrDbContext Context { get; } = context;

	public ValueTask DisposeAsync() => Context.DisposeAsync();
}

internal sealed class MediaQuerySessionFactory(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IMediaQuerySessionFactory
{
	public async ValueTask<IMediaQuerySession> CreateAsync(CancellationToken cancellationToken = default)
	{
		var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return new MediaQuerySession(context);
	}
}
