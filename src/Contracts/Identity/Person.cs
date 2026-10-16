using Domain.Identity;
using Domain.Providers;

namespace Contracts.Identity;

public sealed record PersonHoldings(int Feeds, int Wanted, int Blocklisted, IReadOnlyList<RemoteService> Keys)
{
	public static PersonHoldings Empty { get; } = new(0, 0, 0, []);
}

public sealed record JoinedPerson(User User, PersonHoldings Holdings, Optional<DateTimeOffset> LockedUntil, StoredColour Colour);

public sealed record InvitedPerson(string Username, string DisplayName);

public readonly union Person(JoinedPerson, InvitedPerson);