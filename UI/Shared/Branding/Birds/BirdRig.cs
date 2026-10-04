using System.Text.RegularExpressions;

namespace UI.Shared.Branding.Birds;

internal static class BirdRig
{
	internal const double EyeRadius = 5.57;

	internal const string Wing = "M0,-9 C18,-30 58,-46 104,-40 C86,-22 62,-4 42,7 C28,12 12,12 0,9 Z";

	internal const string WingEdge = "M5,9 C18,11 30,10 41,6";

	internal static readonly string[] WingLines = ["M56,-24 L90,-36", "M46,-12 L74,-22", "M36,-1 L58,-9"];

	internal static readonly BirdPoint Eye = new(167.46, 74.01);

	internal static readonly BirdPoint Shoulder = new(146, 108);

	internal static readonly BirdPoint Feet = new(176, 198.8);

	internal static readonly BirdPoint TailPivot = new(116.81, 157.31);

	internal static readonly BirdPoint BodyCentre = new(150, 128);

	private static readonly BirdPoint Neck = new(158, 96);

	private static readonly BirdPoint Hip = new(168, 166);

	private static readonly BirdPoint HeadCentre = new(171, 68);

	private static readonly BirdPoint NeckDirection = Direction(BodyCentre, HeadCentre);

	private static readonly string[] Parts = Split(MagpieGeometry.Body);

	internal static readonly string Crease = Parts[2];

	internal static readonly IReadOnlyList<BirdPoint> Outline = BirdPath.Sample(Parts[0], 900);

	internal static readonly IReadOnlyList<BirdPoint> TailSamples = BirdPath.Sample(MagpieGeometry.Tail, 120);

	internal static readonly IReadOnlyList<BirdPoint> WingSamples = BirdPath.Sample(Wing, 80);

	internal static double HeadWeight(BirdPoint point)
	{
		var t = Math.Max(0, Math.Min(1, ((point.X - Neck.X) * NeckDirection.X + (point.Y - Neck.Y) * NeckDirection.Y + 4) / 16));
		return t * t * (3 - 2 * t);
	}

	internal static BirdPoint Turn(BirdPoint point, BirdPoint centre, double degrees)
	{
		var radians = degrees * Math.PI / 180;
		var cos = Math.Cos(radians);
		var sin = Math.Sin(radians);
		return new BirdPoint(
			centre.X + (point.X - centre.X) * cos - (point.Y - centre.Y) * sin,
			centre.Y + (point.X - centre.X) * sin + (point.Y - centre.Y) * cos
		);
	}

	internal static BirdPoint Deform(BirdPoint point, BirdShape shape)
	{
		var weight = HeadWeight(point);
		var leg = point.Y > 166 && point.X > 150;
		var moved = point.X > 186 && point.Y < 84 ? Beak(point, shape) : point;
		moved = leg ? Legs(moved, shape.Legs) : moved;
		moved = shape.Round && !leg ? Rounder(moved, weight) : moved;
		return shape.Head != 0 ? Turned(moved, shape.Head, weight) : moved;
	}

	private static BirdPoint Beak(BirdPoint point, BirdShape shape)
	{
		var x = 189 + (point.X - 189) * shape.BeakLength;
		var y = 63 + (point.Y - 63) * shape.BeakLength;
		var hook = Math.Max(0, (x - 189) / 26);
		return new BirdPoint(x, shape.Hook ? y + hook * hook * 7 : y);
	}

	private static BirdPoint Legs(BirdPoint point, BirdLegs legs)
	{
		return legs switch
		{
			BirdLegs.Standing => point,
			BirdLegs.Fold => new BirdPoint(Hip.X + (point.X - Hip.X) * 0.6 - 6, Hip.Y + (point.Y - Hip.Y) * 0.05),
			BirdLegs.Tuck => new BirdPoint(point.X - (point.Y - Hip.Y) * 0.35, Hip.Y + (point.Y - Hip.Y) * 0.42),
			BirdLegs.Crouch => point with { Y = Hip.Y + (point.Y - Hip.Y) * 0.45 },
		};
	}

	private static BirdPoint Rounder(BirdPoint point, double weight)
	{
		var factor = 1 + 0.13 * (1 - weight);
		return new BirdPoint(BodyCentre.X + (point.X - BodyCentre.X) * factor, BodyCentre.Y + (point.Y - BodyCentre.Y) * factor);
	}

	private static BirdPoint Turned(BirdPoint point, int degrees, double weight)
	{
		var turned = Turn(point, Neck, degrees);
		return new BirdPoint(point.X + (turned.X - point.X) * weight, point.Y + (turned.Y - point.Y) * weight);
	}

	private static BirdPoint Direction(BirdPoint from, BirdPoint to)
	{
		var length = double.Hypot(to.X - from.X, to.Y - from.Y);
		return new BirdPoint((to.X - from.X) / length, (to.Y - from.Y) / length);
	}

	private static string[] Split(string body)
	{
		var parts = Regex.Split(body, "z(?=M)", RegexOptions.IgnoreCase);
		return [.. parts.Select((part, index) => index < parts.Length - 1 ? $"{part}z" : part)];
	}
}
