using Domain.Identity.Birds;

namespace UI.Shared.Branding.Birds;

internal enum BirdLegs
{
	Standing,
	Fold,
	Tuck,
	Crouch,
}

internal enum BirdWings
{
	Folded,
	Up,
	Down,
	Open,
	Half,
	Glide,
}

internal sealed record BirdShape(
	int Head,
	int Tilt,
	int Tail,
	double BeakLength,
	bool Hook,
	bool Round,
	BirdCrest Crest,
	bool FacesLeft,
	int Feathers,
	BirdLegs Legs,
	int Lift,
	BirdWings Wings,
	bool Shadow,
	bool Singing
)
{
	internal static BirdShape Of(BirdGenome genome)
	{
		var pose = PoseOf(genome.Pose);
		return new BirdShape(
			pose.Head + genome.Head,
			pose.Tilt + genome.Tilt,
			pose.Tail + genome.Tail,
			genome.BeakLength,
			genome.Hook,
			genome.Round || pose.Round,
			genome.Crest,
			genome.FacesLeft,
			genome.Feathers,
			pose.Legs,
			pose.Lift,
			pose.Wings,
			pose.Shadow,
			pose.Singing
		);
	}

	private static Pose PoseOf(BirdPose pose)
	{
		return pose switch
		{
			BirdPose.Perched => new Pose(),
			BirdPose.LookingUp => new Pose(Head: -26),
			BirdPose.Pecking => new Pose(Head: 78, Tilt: 26, Tail: -10),
			BirdPose.Crouching => new Pose(Head: 12, Tail: 8, Legs: BirdLegs.Crouch, Lift: 15, Round: true),
			BirdPose.Hopping => new Pose(Tilt: -10, Tail: -16, Legs: BirdLegs.Tuck, Lift: -26, Wings: BirdWings.Half, Shadow: true),
			BirdPose.Flying => new Pose(Tilt: -4, Tail: -12, Legs: BirdLegs.Fold, Lift: -8, Wings: BirdWings.Up),
			BirdPose.Downstroke => new Pose(Tilt: -4, Tail: -8, Legs: BirdLegs.Fold, Lift: -14, Wings: BirdWings.Down),
			BirdPose.Gliding => new Pose(Tilt: 2, Tail: -4, Legs: BirdLegs.Fold, Lift: -10, Wings: BirdWings.Glide),
			BirdPose.WingsOpen => new Pose(Head: -12, Wings: BirdWings.Open),
			BirdPose.Singing => new Pose(Head: -40, Singing: true),
		};
	}

	private sealed record Pose(
		int Head = 0,
		int Tilt = 0,
		int Tail = 0,
		BirdLegs Legs = BirdLegs.Standing,
		int Lift = 0,
		BirdWings Wings = BirdWings.Folded,
		bool Round = false,
		bool Shadow = false,
		bool Singing = false
	);
}
