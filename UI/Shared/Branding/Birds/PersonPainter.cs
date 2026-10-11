using Contracts.Identity;

namespace UI.Shared.Branding.Birds;

internal sealed class PersonPainter(IPictureGrids pictures, IUserManagementService users)
{
	internal async Task<bool> PaintAsync(IReadOnlyList<JoinedPerson> people, CancellationToken cancellationToken = default)
	{
		var golden = PersonColours.GoldenHues(people);
		var painted = await Task.WhenAll(people.Select(person => PaintAsync(person, golden[person.User.Id], cancellationToken)));
		return painted.Any(stored => stored);
	}

	private async Task<bool> PaintAsync(JoinedPerson person, double golden, CancellationToken cancellationToken)
	{
		var repainted = (await FetchAsync(person, cancellationToken)).Map(picture => new StoredColour(Some.Of(golden), Some.Of(picture)));
		var gilded = Optional.When(person.Colour.GoldenHue.IsNone(), () => person.Colour with { GoldenHue = Some.Of(golden) });
		return await repainted.Or(() => gilded).Match(colour => StoreAsync(person, colour, cancellationToken), () => false);
	}

	private async Task<bool> StoreAsync(JoinedPerson person, StoredColour colour, CancellationToken cancellationToken)
	{
		await users.StoreColourAsync(person.User.Id, colour, cancellationToken);
		return true;
	}

	private async Task<Optional<StoredPicture>> FetchAsync(JoinedPerson person, CancellationToken cancellationToken)
	{
		var url = person.User.AvatarUrl;
		if (url.Length == 0 || person.Colour.Picture.Match(stored => stored.Url == url, () => false))
		{
			return new None();
		}

		var grid = await pictures.ReadAsync(url, cancellationToken);
		return grid.Map(rgba => new StoredPicture(url, PictureColour.Of(rgba)));
	}
}
