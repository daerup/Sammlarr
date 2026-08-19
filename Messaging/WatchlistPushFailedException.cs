using Domain.Media;
using Domain.Watchlist.Outbox;

namespace Messaging;

internal sealed class WatchlistPushFailedException(MediaId mediaId, WatchlistOperationAction action)
	: Exception($"Failed to push {action} of media {mediaId} to the remote watchlist.");
