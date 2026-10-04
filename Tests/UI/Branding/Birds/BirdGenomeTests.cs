using CsCheck;
using Domain.Identity;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding.Birds;

public class BirdGenomeTests
{
	[Test]
	[Arguments("guest", 2083391600u, BirdPose.Perched, 8, 0, 0, 1.0, true, false, BirdCrest.Plume, true, 1)]
	[Arguments("daerup", 2563934149u, BirdPose.Singing, -8, 5, 8, 1.0, true, false, BirdCrest.Plume, false, 1)]
	[Arguments("mira", 3697562271u, BirdPose.LookingUp, 0, 0, -8, 0.78, false, true, BirdCrest.Plume, true, 2)]
	[Arguments("anna", 92819592u, BirdPose.Pecking, -8, 0, 8, 0.78, false, false, BirdCrest.None, false, 2)]
	[Arguments("eli", 61223484u, BirdPose.Hopping, 8, -5, -8, 0.78, false, false, BirdCrest.Tuft, true, 2)]
	[Arguments("kai", 3972112337u, BirdPose.Gliding, 0, 5, -8, 1.3, false, false, BirdCrest.None, false, 3)]
	[Arguments("nora", 1041166216u, BirdPose.Downstroke, -8, 0, -8, 1.3, false, false, BirdCrest.Tuft, true, 3)]
	[Arguments("uwe", 1033819993u, BirdPose.Crouching, 0, 0, 8, 1.0, false, true, BirdCrest.None, true, 2)]
	public async Task Of_ForTheMockupsSeeds_PicksTheMockupsBird(
		string seed,
		uint genes,
		BirdPose pose,
		int head,
		int tilt,
		int tail,
		double beak,
		bool hook,
		bool round,
		BirdCrest crest,
		bool facesLeft,
		int feathers
	)
	{
		await Assert.That(BirdGenome.Fnv1a($"{seed}#")).IsEqualTo(genes);
		await Assert.That(BirdGenome.Of(seed)).IsEqualTo(new BirdGenome(pose, head, tilt, tail, beak, hook, round, crest, facesLeft, feathers));
	}

	[Test]
	[Arguments("Guest\u200c#", 1216409642u)]
	[Arguments("00000000-0000-0000-0000-000000000001#", 346236071u)]
	[Arguments("ü#", 622554952u)]
	[Arguments("😀#", 754402164u)]
	public async Task Fnv1a_HashesCodePointsNotBytes(string text, uint expected)
	{
		await Assert.That(BirdGenome.Fnv1a(text)).IsEqualTo(expected);
	}

	[Test]
	public async Task Of_ForTheSameUser_AlwaysPicksTheSameBird()
	{
		var id = UserId.New();

		await Assert.That(BirdGenome.Of(id)).IsEqualTo(BirdGenome.Of(UserId.From(id.Value)));
	}

	[Test]
	public void From_ForAnyGenes_PicksOnlyValuesTheDrawingKnows()
	{
		Gen.UInt.Sample(genes =>
		{
			var bird = BirdGenome.From(genes);
			return Enum.IsDefined(bird.Pose)
				&& Enum.IsDefined(bird.Crest)
				&& bird.Head is -8 or 0 or 8
				&& bird.Tilt is -5 or 0 or 5
				&& bird.Tail is -8 or 0 or 8
				&& bird.BeakLength is 0.78 or 1 or 1.3
				&& bird.Feathers is >= 1 and <= 3;
		});
	}

	[Test]
	public async Task Of_AcrossUsers_ReachesEveryPoseAndEveryPart()
	{
		var birds = Enumerable.Range(0, 4000).Select(_ => BirdGenome.Of(UserId.New())).ToList();

		await Assert.That(birds.Select(bird => bird.Pose).Distinct().Count()).IsEqualTo(Enum.GetValues<BirdPose>().Length);
		await Assert.That(birds.Select(bird => bird.Crest).Distinct().Count()).IsEqualTo(Enum.GetValues<BirdCrest>().Length);
		await Assert.That(birds.Select(bird => (bird.Head, bird.Tilt, bird.Tail)).Distinct().Count()).IsEqualTo(27);
		await Assert.That(birds.Select(bird => (bird.BeakLength, bird.Hook, bird.Round, bird.FacesLeft)).Distinct().Count()).IsEqualTo(24);
		await Assert.That(birds.Select(bird => bird.Feathers).Distinct().Count()).IsEqualTo(3);
	}
}
