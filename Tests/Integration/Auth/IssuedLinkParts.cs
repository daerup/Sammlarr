using Contracts.Identity;
using Domain.Identity;

namespace Integration.Tests.Auth;

internal sealed record IssuedLinkParts(UserId User, string Token)
{
	internal static IssuedLinkParts Of(Result<IssuedLink> issued)
	{
		var parts = issued.GetOrThrow(error => new InvalidOperationException(error.Reason)).Path.Split('/');
		return new IssuedLinkParts(UserId.From(Guid.Parse(parts[2])), parts[3]);
	}
}
