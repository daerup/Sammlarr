using Contracts.Identity;

namespace UI.Features.Settings.Components.Users;

internal static class JoinedPeople
{
	extension(IEnumerable<Person> people)
	{
		internal IEnumerable<JoinedPerson> Joined => people.Select(JoinedOf).WhereSome();
	}

	private static Optional<JoinedPerson> JoinedOf(Person person)
	{
		return person switch
		{
			JoinedPerson joined => Some.Of(joined),
			InvitedPerson => new None(),
		};
	}
}
