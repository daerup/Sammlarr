using Contracts.Identity;
using Domain.Identity;

namespace Integration.Tests.Auth;

internal sealed record IssuedLinkParts(UserId User, Secret Token)
{
	internal static IssuedLinkParts Of(Result<IssuedLink> issued)
	{
		var parts = issued.GetOrThrow(error => new InvalidOperationException(error.Reason)).Path.Reveal().Split('/');
		return new IssuedLinkParts(UserId.From(Guid.Parse(parts[2])), new Secret(parts[3]));
	}
}
