using System.Threading.Channels;
using Contracts.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class LibraryWriter : IAsyncDisposable
{
	private const int MostPerCommit = 256;

	private readonly Channel<DetailsChange> _queue = Channel.CreateUnbounded<DetailsChange>(new UnboundedChannelOptions { SingleReader = true });
	private readonly IMediaRepository _repository;
	private readonly ILogger<LibraryWriter> _logger;
	private readonly Task _committing;

	public LibraryWriter(IMediaRepository repository, ILogger<LibraryWriter> logger)
	{
		_repository = repository;
		_logger = logger;
		_committing = Task.Run(CommitAllAsync);
	}

	public void Save(DetailsChange change)
	{
		if (change.Changed)
		{
			_queue.Writer.TryWrite(change);
		}
	}

	public async ValueTask DisposeAsync()
	{
		_queue.Writer.TryComplete();
		await _committing;
	}

	private async Task CommitAllAsync()
	{
		while (await _queue.Reader.WaitToReadAsync())
		{
			List<DetailsChange> group = [];
			while (group.Count < MostPerCommit && _queue.Reader.TryRead(out var change))
			{
				group.Add(change);
			}

			await CommitAsync(group);
		}
	}

	private async Task CommitAsync(IReadOnlyList<DetailsChange> group)
	{
		try
		{
			await _repository.SaveDetailsAsync(group, CancellationToken.None);
		}
		catch (Exception failure) when (group.Count > 1)
		{
			_logger.LogWarning(failure, "A group commit of {Count} details failed, so each is written on its own", group.Count);
			foreach (var change in group)
			{
				await CommitAsync([change]);
			}
		}
		catch (Exception failure)
		{
			_logger.LogWarning(failure, "The details of {MediaId} could not be written and will be fetched again", group[0].Id);
		}
	}
}
