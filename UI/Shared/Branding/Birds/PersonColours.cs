using Contracts.Identity;
using Domain.Identity;

namespace UI.Shared.Branding.Birds;

internal static class PersonColours
{
	internal static IReadOnlyDictionary<UserId, double> GoldenHues(IEnumerable<JoinedPerson> people)
	{
		Dictionary<UserId, double> hues = [];
		Optional<double> last = new None();
		foreach (var person in InJoinOrder(people))
		{
			var hue = person.Colour.GoldenHue.GetValueOr(() => PersonHues.After(last));
			hues[person.User.Id] = hue;
			last = Some.Of(hue);
		}

		return hues;
	}

	internal static IReadOnlyDictionary<UserId, PersonColour> Of(IEnumerable<JoinedPerson> people)
	{
		var joined = people.ToList();
		var golden = GoldenHues(joined);
		return joined.ToDictionary(person => person.User.Id, person => PersonColour.Of(golden[person.User.Id], person.Colour.Picture));
	}

	private static IEnumerable<JoinedPerson> InJoinOrder(IEnumerable<JoinedPerson> people)
	{
		return people.OrderBy(person => person.User.JoinedAt).ThenBy(person => person.User.Id.Value);
	}
}
