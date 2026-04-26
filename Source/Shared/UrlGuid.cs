namespace Shared;

public static class UrlGuid
{
	public static string Encode(Guid id)
	{
		Span<byte> bytes = stackalloc byte[16];
		Span<char> chars = stackalloc char[24];
		id.TryWriteBytes(bytes);
		Convert.TryToBase64Chars(bytes, chars, out _);
		for (var i = 0; i < 22; i++)
		{
			if (chars[i] == '+') chars[i] = '-';
			else if (chars[i] == '/') chars[i] = '_';
		}
		return new string(chars[..22]);
	}

	public static Guid? Decode(string? s)
	{
		if (s is not { Length: 22 }) return null;
		Span<char> chars = stackalloc char[24];
		s.AsSpan().CopyTo(chars);
		chars[22] = '=';
		chars[23] = '=';
		for (var i = 0; i < 22; i++)
		{
			if (chars[i] == '-') chars[i] = '+';
			else if (chars[i] == '_') chars[i] = '/';
		}
		Span<byte> bytes = stackalloc byte[16];
		if (!Convert.TryFromBase64Chars(chars, bytes, out var written) || written != 16)
			return null;
		return new Guid(bytes);
	}
}
