using System.Buffers.Text;

namespace UI.Shared.Common;

internal static class UrlGuid
{
	internal static string Encode(Guid guid) => Base64Url.EncodeToString(guid.ToByteArray());

	internal static Guid Decode(ReadOnlySpan<char> encoded) => new(Base64Url.DecodeFromChars(encoded));
}
