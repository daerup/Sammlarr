using System.ComponentModel;
using Domain.Media;
using UI.Features.Settings.Components.Providers;

namespace UI.Features.Settings.Components.Ratings;

internal enum RatingCombination
{
	[Description("Average")]
	Average,

	[Description("First available")]
	FirstAvailable,
}

internal sealed record PipelineTile(RatingSource Source, bool On, Optional<int> Rank, double Y);

internal sealed record PipelineTrace(RatingSource Source, IReadOnlyList<MapPoint> Points)
{
	internal string Path => TracePath.Draw(Points);

	internal double Pin => Points[^1].Y;
}

internal sealed record RatingPipelineModel(IReadOnlyList<PipelineTile> Tiles, IReadOnlyList<PipelineTrace> Traces, bool Dashed, double OutputY, double Height)
{
	private const double TileWidth = 280;
	internal const double TileHeight = 44;
	internal const double OutputX = 560;
	private const double OutputWidth = 230;
	internal const double OutputHeight = 60;
	internal const double Width = OutputX + OutputWidth;
	private const double RowPitch = 52;
	private const double TurnX = 400;
	private const double TurnSlope = 0.35;
	private const double PinPitch = 8;

	internal static RatingPipelineModel For(RatingStrategy strategy, ValueSet<RatingSource> available)
	{
		var (rows, counted, dashed) = strategy switch
		{
			AveragedRating averaged => Averaged(averaged, available),
			OrderedRating ordered => Ordered(ordered, available),
		};
		var placed = rows.Select((row, index) => row with { Y = index * RowPitch + TileHeight / 2 }).ToList();
		var middle = Middle(placed.Where(tile => counted.Contains(tile.Source)).ToList(), placed);
		var shift = Math.Max(0, OutputHeight / 2 - middle);
		var tiles = placed.Select(tile => tile with { Y = tile.Y + shift }).ToList();
		var feeding = tiles.Where(tile => counted.Contains(tile.Source)).ToList();
		var traces = feeding.Select((tile, index) => Trace(tile, middle + shift + (index - (feeding.Count - 1) / 2.0) * PinPitch)).ToList();
		var output = middle + shift - OutputHeight / 2;
		var height = Math.Max(tiles.Select(tile => tile.Y + TileHeight / 2).DefaultIfEmpty(0).Max(), output + OutputHeight);
		return new RatingPipelineModel(tiles, traces, dashed, output, height);
	}

	private static double Middle(IReadOnlyList<PipelineTile> feeding, IReadOnlyList<PipelineTile> all)
	{
		if (feeding.Count > 0)
		{
			return feeding.Average(tile => tile.Y);
		}

		return all.Count > 0 ? all.Average(tile => tile.Y) : OutputHeight / 2;
	}

	private static PipelineTrace Trace(PipelineTile tile, double pin)
	{
		var turn = TurnX + Math.Abs(tile.Y - pin) * TurnSlope;
		return new PipelineTrace(tile.Source, [new(TileWidth, tile.Y), new(turn, tile.Y), new(turn, pin), new(OutputX, pin)]);
	}

	private static (List<PipelineTile> Rows, ValueSet<RatingSource> Counted, bool Dashed) Averaged(AveragedRating averaged, ValueSet<RatingSource> available)
	{
		var counted = averaged.Counted(available);
		var rows = Enum.GetValues<RatingSource>()
			.Where(available.Contains)
			.OrderBy(source => !counted.Contains(source))
			.Select(source => new PipelineTile(source, averaged.SwitchedOn.Contains(source), new None(), 0))
			.ToList();
		return (rows, counted, averaged.FallsBack(available));
	}

	private static (List<PipelineTile> Rows, ValueSet<RatingSource> Counted, bool Dashed) Ordered(OrderedRating ordered, ValueSet<RatingSource> available)
	{
		var rows = ordered.Order.Where(available.Contains).Select((source, index) => new PipelineTile(source, true, Some.Of(index + 1), 0)).ToList();
		return (rows, available, false);
	}
}
