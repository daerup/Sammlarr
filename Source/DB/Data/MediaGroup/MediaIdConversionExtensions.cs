using System.Collections.ObjectModel;

namespace DB.Data.MediaGroup;

internal static class MediaIdConversionExtensions
{
	extension(IEnumerable<MediaIdentifier> source)
	{
		public Collection<MediaIdentifierGroupEntry> ToIdentifierEntries(MediaIdentifierGroupEntity groupEntity) =>
			[.. source.Select(identifier => new MediaIdentifierGroupEntry { Identifier = identifier, MediaIdGroupEntity = groupEntity })];
	}
}
