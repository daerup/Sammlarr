using Contracts.Identity;
using Domain.Identity;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding.Birds;

public class PersonPainterTests
{
	private const string Avatar = "https://plex.tv/users/a/avatar?c=2";

	private const int Side = 48;

	private readonly Mock<IPictureGrids> _pictures = IPictureGrids.Mock();

	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	private PersonPainter Testee => new(_pictures.Object, _users.Object);

	[Test]
	public async Task PaintAsync_ForSomeoneWithoutAGoldenHue_StoresTheNextOne()
	{
		var person = Person(string.Empty, StoredColour.Empty);

		var painted = await Testee.PaintAsync([person]);

		await Assert.That(painted).IsTrue();
		_users
			.StoreColourAsync(person.User.Id, Is<StoredColour>(colour => colour!.GoldenHue.GetValueOr(0) == 40 && colour.Picture.IsNone()), Any())
			.WasCalled(Times.Once);
		_pictures.ReadAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task PaintAsync_ForANewPicture_StoresItsToneWithTheAddressItCameFrom()
	{
		_pictures.ReadAsync(Avatar, Any()).Returns(Some.Of(Solid(0x1f, 0x8a, 0x8a)));
		var person = Person(Avatar, new StoredColour(Some.Of(40d), new None()));

		await Testee.PaintAsync([person]);

		_users
			.StoreColourAsync(
				person.User.Id,
				Is<StoredColour>(colour =>
					colour!.Picture.Map(picture => picture.Url).GetValueOr(string.Empty) == Avatar
					&& colour.Picture.Map(picture => picture.Tone).Map(tone => Math.Round(tone.Hue, MidpointRounding.ToEven)).GetValueOr(0) == 195
				),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task PaintAsync_ForAGreyPicture_RemembersItWithoutATone()
	{
		_pictures.ReadAsync(Avatar, Any()).Returns(Some.Of(Solid(128, 128, 128)));
		var person = Person(Avatar, new StoredColour(Some.Of(40d), new None()));

		await Testee.PaintAsync([person]);

		_users
			.StoreColourAsync(
				person.User.Id,
				Is<StoredColour>(colour =>
					colour!.Picture.Map(picture => picture.Url).GetValueOr(string.Empty) == Avatar && colour.Picture.Map(picture => picture.Tone).IsNone()
				),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task PaintAsync_ForAPictureAlreadyPaintedFromTheSameAddress_AsksNothingAndStoresNothing()
	{
		var person = Person(Avatar, new StoredColour(Some.Of(40d), Some.Of(new StoredPicture(Avatar, new None()))));

		var painted = await Testee.PaintAsync([person]);

		await Assert.That(painted).IsFalse();
		_pictures.ReadAsync(Any(), Any()).WasNeverCalled();
		_users.StoreColourAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task PaintAsync_WhenThePictureCannotBeRead_KeepsWhatIsStoredToTryAgainNextTime()
	{
		_pictures.ReadAsync(Avatar, Any()).Returns(new None());
		var person = Person(
			Avatar,
			new StoredColour(Some.Of(40d), Some.Of(new StoredPicture("https://plex.tv/users/a/avatar?c=1", Some.Of(new PictureTone(195, 0.09)))))
		);

		var painted = await Testee.PaintAsync([person]);

		await Assert.That(painted).IsFalse();
		_users.StoreColourAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task PaintAsync_ForSomeoneWithoutAGoldenHueWhosePictureCannotBeRead_StoresTheHueAndKeepsTheStoredPicture()
	{
		_pictures.ReadAsync(Avatar, Any()).Returns(new None());
		var stored = new StoredPicture("https://plex.tv/users/a/avatar?c=1", Some.Of(new PictureTone(195, 0.09)));
		var person = Person(Avatar, new StoredColour(new None(), Some.Of(stored)));

		await Testee.PaintAsync([person]);

		_users
			.StoreColourAsync(
				person.User.Id,
				Is<StoredColour>(colour => colour!.GoldenHue.GetValueOr(0) == 40 && colour.Picture.Map(picture => picture.Url).GetValueOr(string.Empty) == stored.Url),
				Any()
			)
			.WasCalled(Times.Once);
	}

	private static JoinedPerson Person(string avatar, StoredColour colour)
	{
		var user = new User(UserId.New(), "someone", avatar, UserRole.Member, DateTimeOffset.UnixEpoch);
		return new JoinedPerson(user, PersonHoldings.Empty, new None(), colour);
	}

	private static byte[] Solid(byte red, byte green, byte blue)
	{
		return [.. Enumerable.Range(0, Side * Side).SelectMany(_ => new[] { red, green, blue, (byte)255 })];
	}
}
