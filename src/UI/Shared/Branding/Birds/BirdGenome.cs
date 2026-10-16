using Domain.Identity;

namespace UI.Shared.Branding.Birds;

public enum BirdPose
{
	Perched,
	LookingUp,
	Pecking,
	Crouching,
	Hopping,
	Flying,
	Downstroke,
	Gliding,
	WingsOpen,
	Singing,
}

public enum BirdCrest
{
	None,
	Tuft,
	Plume,
}

internal readonly record struct BirdGenome(
	BirdPose Pose,
	int Head,
	int Tilt,
	int Tail,
	double BeakLength,
	bool Hook,
	bool Round,
	BirdCrest Crest,
	bool FacesLeft,
	int Feathers
)
{
	private static readonly int[] Turns = [-8, 0, 0, 8];

	private static readonly int[] Tilts = [-5, 0, 0, 5];

	private static readonly int[] Flicks = [-8, 0, 8];

	private static readonly double[] Beaks = [0.78, 1, 1, 1.3];

	internal static BirdGenome Of(UserId id) => Of(id.Value.ToString());

	internal static BirdGenome Of(string seed) => From(Fnv1a($"{seed}#"));

	internal static BirdGenome From(uint genes)
	{
		return new BirdGenome(
			(BirdPose)(genes % 10),
			Turns[(genes >> 4) % 4],
			Tilts[(genes >> 6) % 4],
			Flicks[(genes >> 8) % 3],
			Beaks[(genes >> 10) % 4],
			(genes >> 12) % 4 == 0,
			(genes >> 14) % 3 == 0,
			(BirdCrest)((genes >> 16) % 3),
			(genes >> 18) % 2 == 1,
			1 + (int)((genes >> 20) % 3)
		);
	}

	internal static uint Fnv1a(string text)
	{
		var hash = 2166136261;
		foreach (var rune in text.EnumerateRunes())
		{
			hash ^= (uint)rune.Value;
			hash = unchecked(hash * 16777619);
		}

		return hash;
	}
}
