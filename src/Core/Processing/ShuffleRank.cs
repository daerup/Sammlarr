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

	internal static Guid Reseed(Guid seed, DateTimeOffset after)
	{
		Span<byte> input = stackalloc byte[24];
		seed.TryWriteBytes(input[..16]);
		BinaryPrimitives.WriteInt64BigEndian(input[16..], after.UtcTicks);
		Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
		SHA256.HashData(input, hash);
		return new Guid(hash[..16]);
	}
}
