using Domain.Identity;

namespace Contracts.Identity;

public sealed record StoredColour(Optional<double> GoldenHue, Optional<StoredPicture> Picture)
{
	public static StoredColour Empty { get; } = new(new None(), new None());
}

public sealed record StoredPicture(string Url, Optional<PictureTone> Tone);

public sealed record PictureTone(double Hue, double Chroma);

public sealed record RosterColour(UserId Id, DateTimeOffset JoinedAt, StoredColour Colour);
