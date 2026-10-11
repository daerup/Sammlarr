using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal static class MergeLog
{
	extension(ILogger logger)
	{
		public void Merged(Merged merged, IdentityConflict note, string settledBy)
		{
			logger.LogInformation(
				"Merged {Removed} into {Survivor}, stored twice through {Claimed} claimed by {Sources}, settled by {SettledBy}",
				merged.Removed,
				merged.Survivor,
				$"{note.Claimed}",
				string.Join(", ", note.Sources.Order()),
				settledBy
			);
		}
	}
}
