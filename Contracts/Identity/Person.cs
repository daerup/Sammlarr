using Domain.Identity;
using Domain.Providers;

namespace Contracts.Identity;

public sealed record PersonHoldings(int Feeds, int Wanted, int Blocklisted, IReadOnlyList<MediaProvider> Keys)
{
	public static PersonHoldings Empty { get; } = new(0, 0, 0, []);
}

public sealed record JoinedPerson(User User, PersonHoldings Holdings, Optional<DateTimeOffset> LockedUntil, double Hue);

public sealed record InvitedPerson(string Username, string DisplayName, DateTimeOffset ExpiresAt);

public union Person(JoinedPerson, InvitedPerson);