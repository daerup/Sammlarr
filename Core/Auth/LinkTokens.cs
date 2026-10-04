using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Core.Auth;

internal static class LinkTokens
{
	internal static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

	internal static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

	internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
