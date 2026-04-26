using Contracts.Media;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Data.MediaGroup;

internal class MediaIdentifierConverter()
	: ValueConverter<MediaIdentifier, string>(identifier => $"{identifier.Source}:{identifier.MediaType}:{identifier.ExternalId}", value => Parse(value))
{
	private static MediaIdentifier Parse(string value)
	{
		var firstSeparator = value.IndexOf(':');
		var secondSeparator = value.IndexOf(':', firstSeparator + 1);
		if (firstSeparator < 0 || secondSeparator < 0 || secondSeparator == value.Length - 1)
		{
			throw new InvalidOperationException("Invalid media identifier value.");
		}

		var sourceText = value[..firstSeparator];
		var mediaTypeText = value[(firstSeparator + 1)..secondSeparator];
		var externalId = value[(secondSeparator + 1)..];

		if (!Enum.TryParse(sourceText, out MediaProvider source))
		{
			throw new InvalidOperationException($"Unsupported media provider '{sourceText}'.");
		}

		if (!Enum.TryParse(mediaTypeText, out MediaType mediaType))
		{
			throw new InvalidOperationException($"Unsupported media type '{mediaTypeText}'.");
		}

		return new MediaIdentifier(source, externalId, mediaType);
	}
}
