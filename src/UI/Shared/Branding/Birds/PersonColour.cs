using Contracts.Identity;

namespace UI.Shared.Branding.Birds;

public sealed record PersonColour(double Hue, Optional<double> Chroma)
{
	internal static PersonColour Golden(double hue) => new(hue, new None());

	internal static PersonColour Of(double golden, Optional<StoredPicture> picture)
	{
		return picture.Map(stored => stored.Tone).Match(tone => new PersonColour(tone.Hue, Some.Of(tone.Chroma)), () => Golden(golden));
	}
}
