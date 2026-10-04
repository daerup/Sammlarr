using Contracts.Identity;
using Domain.Identity;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding.Birds;

public class PersonColoursTests
{
	private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

	[Test]
	public async Task GoldenHues_ForPeopleWithoutOne_StepTheGoldenAngleFromTheLatestOneInJoinOrder()
	{
		var first = Person(Start, Some.Of(315d));
		var second = Person(Start.AddDays(2), new None());
		var third = Person(Start.AddDays(3), new None());

		var hues = PersonColours.GoldenHues([third, first, second]);

		await Assert.That(new[] { hues[first.User.Id], hues[second.User.Id], hues[third.User.Id] }).IsEquivalentTo([315, 92.5, 230], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GoldenHues_ForPeopleWhoJoinedInTheSameInstant_StepInTheOrderOfTheirIds()
	{
		var first = Person(Start, Some.Of(315d));
		var later = Person(Start.AddDays(1), new None(), Guid.Parse("00000000-0000-0000-0000-000000000002"));
		var earlier = Person(Start.AddDays(1), new None(), Guid.Parse("00000000-0000-0000-0000-000000000001"));

		var hues = PersonColours.GoldenHues([later, first, earlier]);

		await Assert.That(new[] { hues[earlier.User.Id], hues[later.User.Id] }).IsEquivalentTo([92.5, 230], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GoldenHues_ForTheFirstPerson_IsTheLogosGold()
	{
		var first = Person(Start, new None());

		await Assert.That(PersonColours.GoldenHues([first])[first.User.Id]).IsEqualTo(40);
	}

	[Test]
	public async Task Of_ForAPictureWithATone_TakesTheTone()
	{
		var person = Person(Start, Some.Of(40d), Some.Of(new StoredPicture("https://plex.tv/users/a/avatar", Some.Of(new PictureTone(195, 0.09)))));

		var colour = PersonColours.Of([person])[person.User.Id];

		await Assert.That(colour.Hue).IsEqualTo(195);
		await Assert.That(colour.Chroma).IsSome().And.IsEqualTo(0.09);
	}

	[Test]
	public async Task Of_ForAGreyPicture_KeepsTheGoldenHueInTheAppsTone()
	{
		var person = Person(Start, Some.Of(177.5), Some.Of(new StoredPicture("https://plex.tv/users/a/avatar", new None())));

		var colour = PersonColours.Of([person])[person.User.Id];

		await Assert.That(colour.Hue).IsEqualTo(177.5);
		await Assert.That(colour.Chroma).IsNone();
	}

	private static JoinedPerson Person(DateTimeOffset joinedAt, Optional<double> golden) => Person(joinedAt, golden, new None());

	private static JoinedPerson Person(DateTimeOffset joinedAt, Optional<double> golden, Guid id) => Person(joinedAt, golden, new None(), id);

	private static JoinedPerson Person(DateTimeOffset joinedAt, Optional<double> golden, Optional<StoredPicture> picture) =>
		Person(joinedAt, golden, picture, Guid.NewGuid());

	private static JoinedPerson Person(DateTimeOffset joinedAt, Optional<double> golden, Optional<StoredPicture> picture, Guid id)
	{
		var user = new User(UserId.From(id), "someone", string.Empty, UserRole.Member, joinedAt);
		return new JoinedPerson(user, PersonHoldings.Empty, new None(), new StoredColour(golden, picture));
	}
}
