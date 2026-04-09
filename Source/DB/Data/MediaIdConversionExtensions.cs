using System.Collections.ObjectModel;
using DB.Data.Entities;

namespace DB.Data;

internal static class MediaIdConversionExtensions
{
	extension(IEnumerable<MediaIdentifier> source)
	{
		public Collection<MediaIdentifierEntry> ToIdentifierEntries(MediaIdentifierGroup group)
		{
			return [.. source.Select(identifier => new MediaIdentifierEntry { Identifier = identifier, MediaIdGroup = group })];
		}
	}
}
