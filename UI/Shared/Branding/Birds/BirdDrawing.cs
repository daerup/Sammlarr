using System.Globalization;
using System.Text;
using Domain.Identity.Birds;

namespace UI.Shared.Branding.Birds;

internal sealed record BirdArt(string Markup, string ViewBox);

internal static class BirdDrawing
{
	private static readonly BirdPoint Origin = new(0, 0);

	private static readonly string Rounder = Invariant(
		$"translate({BirdRig.BodyCentre.X} {BirdRig.BodyCentre.Y}) scale(1.1) translate({-BirdRig.BodyCentre.X} {-BirdRig.BodyCentre.Y})"
	);

	internal static BirdArt Draw(BirdGenome genome, string body, string cut) => Draw(BirdShape.Of(genome), body, cut);

	private static BirdArt Draw(BirdShape shape, string body, string cut)
	{
		var points = BirdRig.Outline.Select(point => BirdRig.Deform(point, shape)).ToList();
		var headOnly = shape with { BeakLength = 1, Hook = false, Round = false, Legs = BirdLegs.Standing };
		var eye = BirdRig.Deform(BirdRig.Eye, headOnly);
		var crown = Crown(points);
		var shoulder = BirdRig.Deform(BirdRig.Shoulder, shape with { Head = 0 });
		var wings = WingsOf(shape.Wings);

		var art = new StringBuilder();
		foreach (var wing in wings.Where(wing => !wing.Near))
		{
			art.Append(Blade(wing, shoulder, body));
		}

		art.Append(
			Invariant($"<g transform=\"rotate({shape.Tail} {BirdRig.TailPivot.X} {BirdRig.TailPivot.Y})\"><path d=\"{MagpieGeometry.Tail}\" fill=\"{body}\"/></g>")
		);
		art.Append(Crest(shape, crown, body));
		art.Append(Invariant($"<path d=\"M{string.Join("L", points.Select(point => $"{Fixed(point.X)},{Fixed(point.Y)}"))}Z\" fill=\"{body}\"/>"));
		if (wings.Count == 0)
		{
			art.Append(Invariant($"<path d=\"{BirdRig.Crease}\" fill=\"{cut}\" transform=\"{(shape.Round ? Rounder : string.Empty)}\"/>"));
		}

		foreach (var wing in wings.Where(wing => wing.Near))
		{
			art.Append(Blade(wing, shoulder, body));
			art.Append(
				Invariant(
					$"<path d=\"{BirdRig.WingEdge} {string.Join(" ", BirdRig.WingLines.Take(shape.Feathers))}\" fill=\"none\" stroke=\"{cut}\" stroke-width=\"3.2\" stroke-linecap=\"round\"{Placed(wing, shoulder)}/>"
				)
			);
		}

		art.Append(Invariant($"<circle cx=\"{Fixed(eye.X)}\" cy=\"{Fixed(eye.Y)}\" r=\"{BirdRig.EyeRadius}\" fill=\"{cut}\"/>"));
		if (shape.Singing)
		{
			var from = BirdRig.Deform(new BirdPoint(191, 63), headOnly);
			var to = BirdRig.Deform(new BirdPoint(209, 55), headOnly);
			art.Append(
				Invariant($"<path d=\"M{Fixed(from.X)},{Fixed(from.Y)} L{Fixed(to.X)},{Fixed(to.Y)}\" stroke=\"{cut}\" stroke-width=\"3.4\" stroke-linecap=\"round\"/>")
			);
			art.Append(
				Invariant(
					$"<g fill=\"{body}\" opacity=\".7\"><path d=\"M204,44 v-22 l14,-5 v22\" stroke=\"{body}\" stroke-width=\"4\" fill=\"none\" stroke-linecap=\"round\"/><circle cx=\"200\" cy=\"44\" r=\"5\"/><circle cx=\"214\" cy=\"39\" r=\"5\"/></g>"
				)
			);
		}

		var shadow = shape.Shadow ? Invariant($"<ellipse cx=\"176\" cy=\"204\" rx=\"40\" ry=\"5\" fill=\"{body}\" opacity=\".25\"/>") : string.Empty;
		var posed = Invariant($"{shadow}<g transform=\"translate(0 {shape.Lift}) rotate({shape.Tilt} {BirdRig.Feet.X} {BirdRig.Feet.Y})\">{art}</g>");
		var markup = shape.FacesLeft ? $"<g transform=\"translate(256 0) scale(-1 1)\">{posed}</g>" : posed;
		return new BirdArt(markup, Frame(shape, points, crown, shoulder, wings));
	}

	internal static string Fixed(double value)
	{
		var quarters = value * 4;
		var tie = quarters == Math.Floor(quarters) && Math.Abs(quarters % 2) == 1;
		var rounded = tie ? Math.Round(value, 1, MidpointRounding.AwayFromZero) : value;
		return value == 0 ? "0.0" : rounded.ToString("F1", CultureInfo.InvariantCulture);
	}

	private static string Frame(BirdShape shape, IReadOnlyList<BirdPoint> points, BirdPoint crown, BirdPoint shoulder, IReadOnlyList<WingPlace> wings)
	{
		var cloud = new List<BirdPoint>(points);
		cloud.AddRange(BirdRig.TailSamples.Select(point => BirdRig.Turn(point, BirdRig.TailPivot, shape.Tail)));
		cloud.AddRange(wings.SelectMany(wing => BirdRig.WingSamples.Select(point => Spread(point, wing, shoulder))));
		if (shape.Crest != BirdCrest.None)
		{
			cloud.AddRange([new BirdPoint(crown.X - 40, crown.Y - 24), new BirdPoint(crown.X + 6, crown.Y - 18)]);
		}

		if (shape.Singing)
		{
			cloud.AddRange([new BirdPoint(196, 14), new BirdPoint(220, 50)]);
		}

		var placed = cloud.Select(point => Lifted(BirdRig.Turn(point, BirdRig.Feet, shape.Tilt), shape.Lift)).ToList();
		if (shape.Shadow)
		{
			placed.AddRange([new BirdPoint(136, 204), new BirdPoint(216, 204)]);
		}

		var xs = placed.Select(point => shape.FacesLeft ? 256 - point.X : point.X).ToList();
		var ys = placed.Select(point => point.Y).ToList();
		var centreX = (xs.Min() + xs.Max()) / 2;
		var centreY = (ys.Min() + ys.Max()) / 2;
		var radius = Math.Max(112, xs.Select((x, index) => double.Hypot(x - centreX, ys[index] - centreY)).Max()) * 1.07;
		return $"{Fixed(centreX - radius)} {Fixed(centreY - radius)} {Fixed(2 * radius)} {Fixed(2 * radius)}";
	}

	private static BirdPoint Crown(IReadOnlyList<BirdPoint> points)
	{
		var crown = new BirdPoint(0, 999);
		for (var index = 0; index < points.Count; index++)
		{
			var rest = BirdRig.Outline[index];
			if (BirdRig.HeadWeight(rest) > 0.98 && rest.X < 186 && points[index].Y < crown.Y)
			{
				crown = points[index];
			}
		}

		return crown;
	}

	private static string Crest(BirdShape shape, BirdPoint crown, string body)
	{
		var placed = Invariant($"transform=\"translate({Fixed(crown.X)} {Fixed(crown.Y)}) rotate({shape.Head})\"");
		return shape.Crest switch
		{
			BirdCrest.None => string.Empty,
			BirdCrest.Tuft => Invariant(
				$"<path d=\"M0,2 l-4,-15 M-6,4 l-12,-10 M5,1 l0,-15\" {placed} stroke=\"{body}\" stroke-width=\"5\" stroke-linecap=\"round\" fill=\"none\"/>"
			),
			BirdCrest.Plume => Invariant($"<path d=\"M2,4 C-6,-16 -24,-22 -40,-16 C-26,-12 -14,-4 -8,8 Z\" {placed} fill=\"{body}\"/>"),
		};
	}

	private static string Blade(WingPlace wing, BirdPoint shoulder, string body)
	{
		return Invariant($"<path d=\"{BirdRig.Wing}\" fill=\"{body}\"{Placed(wing, shoulder)}/>");
	}

	private static string Placed(WingPlace wing, BirdPoint shoulder)
	{
		return Invariant($" transform=\"translate({Fixed(shoulder.X + wing.OffsetX)} {Fixed(shoulder.Y + wing.OffsetY)}) rotate({wing.Angle}) scale(0.88)\"");
	}

	private static BirdPoint Spread(BirdPoint point, WingPlace wing, BirdPoint shoulder)
	{
		var turned = BirdRig.Turn(new BirdPoint(point.X * 0.88, point.Y * 0.88), Origin, wing.Angle);
		return new BirdPoint(turned.X + shoulder.X + wing.OffsetX, turned.Y + shoulder.Y + wing.OffsetY);
	}

	private static BirdPoint Lifted(BirdPoint point, int lift) => point with { Y = point.Y + lift };

	private static IReadOnlyList<WingPlace> WingsOf(BirdWings wings)
	{
		return wings switch
		{
			BirdWings.Folded => [],
			BirdWings.Up => [new WingPlace(-112, false, 4, -6), new WingPlace(-128, true, 0, 0)],
			BirdWings.Down => [new WingPlace(76, false, 10, 2), new WingPlace(100, true, 0, 4)],
			BirdWings.Open => [new WingPlace(-58, false, 10, -4), new WingPlace(-140, true, 0, 0)],
			BirdWings.Half => [new WingPlace(-160, true, 0, 0)],
			BirdWings.Glide => [new WingPlace(-148, false, 10, -6), new WingPlace(-168, true, 0, -4)],
		};
	}

	private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

	private readonly record struct WingPlace(int Angle, bool Near, int OffsetX, int OffsetY);
}
