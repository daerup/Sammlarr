using Contracts.Identity;
using Domain.Identity;

namespace UI.Shared.Branding.Birds;

internal static class PersonColours
{
	internal static IReadOnlyDictionary<UserId, double> GoldenHues(IEnumerable<JoinedPerson> people) => GoldenHues(people.Select(Roster));

	internal static IReadOnlyDictionary<UserId, double> GoldenHues(IEnumerable<RosterColour> roster)
	{
		Dictionary<UserId, double> hues = [];
		Optional<double> last = new None();
		foreach (var person in InJoinOrder(roster))
		{
			var hue = person.Colour.GoldenHue.GetValueOr(() => PersonHues.After(last));
			hues[person.Id] = hue;
			last = Some.Of(hue);
		}

		return hues;
	}

	internal static IReadOnlyDictionary<UserId, PersonColour> Of(IEnumerable<JoinedPerson> people) => Of(people.Select(Roster));

	internal static IReadOnlyDictionary<UserId, PersonColour> Of(IEnumerable<RosterColour> roster)
	{
		var joined = roster.ToList();
		var golden = GoldenHues(joined);
		return joined.ToDictionary(person => person.Id, person => PersonColour.Of(golden[person.Id], person.Colour.Picture));
	}

	private static RosterColour Roster(JoinedPerson person) => new(person.User.Id, person.User.JoinedAt, person.Colour);

	private static IEnumerable<RosterColour> InJoinOrder(IEnumerable<RosterColour> roster)
	{
		return roster.OrderBy(person => person.JoinedAt).ThenBy(person => person.Id.Value);
	}
}
