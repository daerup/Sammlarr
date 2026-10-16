using System.Security.Cryptography;
using System.Text;
using Contracts.Media;
using Domain.Media;

namespace Core.Tests.Media;

internal static class TestClaims
{
	internal static Mock<IIdentityClaims> Granting()
	{
		var claims = IIdentityClaims.Mock();
		claims.ClaimAsync(Any(), Any()).Returns((claim, _) => new Claimed(claim.Pinned.GetValueOr(() => RowOf(claim.Keys)), claim.Keys));
		return claims;
	}

	private static MediaId RowOf(ValueSet<IIdentityKey> keys)
	{
		var named = string.Join("|", keys.Select(key => $"{key}").Order(StringComparer.Ordinal));
		return MediaId.From(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(named))[..16]));
	}
}
