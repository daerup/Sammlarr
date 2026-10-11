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

internal sealed record PipelineTile(RatingSource Source, bool On, Optional<int> Rank, double Y, bool Supplied);

internal sealed record PipelineTrace(RatingSource Source, IReadOnlyList<MapPoint> Points)
{
	internal string Path => TracePath.Draw(Points);

	internal double Pin => Points[^1].Y;
}

internal sealed record RatingPipelineModel(IReadOnlyList<PipelineTile> Tiles, IReadOnlyList<PipelineTrace> Traces, bool Dashed, double OutputY, double Height)
{
	internal const double TileWidth = 280;
	internal const double TileHeight = 44;
	internal const double OutputX = 560;
	private const double OutputWidth = 230;
	internal const double OutputHeight = 60;
	internal const double Width = OutputX + OutputWidth;
	private const double RowPitch = 52;
	private const double TurnX = 400;
	private const double TurnSlope = 0.35;
	internal const double PinInset = 8;
	private const double MaxPinPitch = 8;

	internal static RatingPipelineModel For(RatingStrategy strategy, OrderedRating order, ValueSet<RatingSource> available)
	{
		var (rows, counted, dashed) = strategy switch
		{
			AveragedRating averaged => Averaged(averaged, order, available),
			OrderedRating ordered => Ordered(ordered, available),
		};
		var tiles = rows.Select((row, index) => row with { Y = index * RowPitch + TileHeight / 2 }).ToList();
		var middle = tiles.Average(tile => tile.Y);
		var feeding = tiles.Where(tile => counted.Contains(tile.Source)).ToList();
		var pitch = PinPitch(feeding.Count);
		var traces = feeding.Select((tile, index) => Trace(tile, middle + (index - (feeding.Count - 1) / 2.0) * pitch)).ToList();
		return new RatingPipelineModel(tiles, traces, dashed, middle - OutputHeight / 2, tiles[^1].Y + TileHeight / 2);
	}

	private static double PinPitch(int count) => Math.Min(MaxPinPitch, (OutputHeight - 2 * PinInset) / (count - 1));

	private static PipelineTrace Trace(PipelineTile tile, double pin)
	{
		var turn = TurnX + Math.Abs(tile.Y - pin) * TurnSlope;
		return new PipelineTrace(tile.Source, [new(TileWidth, tile.Y), new(turn, tile.Y), new(turn, pin), new(OutputX, pin)]);
	}

	private static (List<PipelineTile> Rows, ValueSet<RatingSource> Counted, bool Dashed) Averaged(
		AveragedRating averaged,
		OrderedRating order,
		ValueSet<RatingSource> available
	)
	{
		var counted = averaged.Counted(available);
		var rows = order.Order.Select(source => new PipelineTile(source, averaged.SwitchedOn.Contains(source), new None(), 0, available.Contains(source))).ToList();
		return (rows, counted, averaged.FallsBack(available));
	}

	private static (List<PipelineTile> Rows, ValueSet<RatingSource> Counted, bool Dashed) Ordered(OrderedRating ordered, ValueSet<RatingSource> available)
	{
		var rows = ordered.Order.Select((source, index) => new PipelineTile(source, true, Some.Of(index + 1), 0, available.Contains(source))).ToList();
		return (rows, available, false);
	}
}
