namespace Contracts.Media;

public interface IMediaQuerySession : IAsyncDisposable;

public interface IMediaQuerySessionFactory
{
	ValueTask<IMediaQuerySession> CreateAsync(CancellationToken cancellationToken = default);
}
