using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Core.Processing;

internal static class ShuffleRank
{
	internal static ulong Of(Guid seed, Guid key)
	{
		Span<byte> input = stackalloc byte[32];
		seed.TryWriteBytes(input[..16]);
		key.TryWriteBytes(input[16..]);
		Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
		SHA256.HashData(input, hash);
		return BinaryPrimitives.ReadUInt64BigEndian(hash);
	}
}
