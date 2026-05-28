namespace Shared;

public static class UrlGuid
{
	public static string Encode(Guid guid)
	{
		Span<byte> guidBytes = stackalloc byte[16];
		Span<char> base64Chars = stackalloc char[24];
		guid.TryWriteBytes(guidBytes);
		Convert.TryToBase64Chars(guidBytes, base64Chars, out _);
		for (var i = 0; i < 22; i++)
		{
			base64Chars[i] = base64Chars[i] switch
			{
				'+' => '-',
				'/' => '_',
				_ => base64Chars[i],
			};
		}
		return new string(base64Chars[..22]);
	}

	public static Guid Decode(ReadOnlySpan<char> encoded)
	{
		if (encoded is not { Length: 22 })
		{
			throw new ArgumentException("provided value has invalid length", nameof(encoded));
		}

		Span<char> base64Chars = stackalloc char[24];
		encoded.CopyTo(base64Chars);
		for (var i = 0; i < 22; i++)
		{
			base64Chars[i] = base64Chars[i] switch
			{
				'-' => '+',
				'_' => '/',
				_ => base64Chars[i],
			};
		}
		base64Chars[22] = '=';
		base64Chars[23] = '=';
		Span<byte> guidBytes = stackalloc byte[16];
		return Convert.TryFromBase64Chars(base64Chars, guidBytes, out _)
			? new Guid(guidBytes)
			: throw new ArgumentException("provided value could not be decoded", nameof(encoded));
	}
}
