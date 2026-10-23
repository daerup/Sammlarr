using System.Web;
using Contracts.Identity;
using Domain.Identity;

namespace Integration.Tests.Auth;

internal sealed record IssuedLinkParts(UserId User, Secret Token)
{
	internal static IssuedLinkParts Of(Result<IssuedLink> issued)
	{
		var link = new Uri(new Uri("http://sammlarr.test"), issued.GetOrThrow(error => new InvalidOperationException(error.Reason)).Path.Reveal());
		return new IssuedLinkParts(UserId.From(Guid.Parse(link.Segments[2])), new Secret(HttpUtility.ParseQueryString(link.Query)["token"]!));
	}
}
