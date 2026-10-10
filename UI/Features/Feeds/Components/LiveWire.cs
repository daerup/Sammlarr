using Domain.Feed.Trace;
using Domain.Providers;
using UI.Shared.Services.Branding;

namespace UI.Features.Feeds.Components;

internal enum WireKind : byte
{
	Started,
	SourceEnded,
	Listed,
	Known,
	Earlier,
	Path,
	Asked,
	Answered,
	GaveUp,
	Settled,
	Page,
}

public readonly record struct WireStep(long Seq, int AtMs, int Title, byte Kind)
{
	public byte Service { get; init; }

	public byte Stage { get; init; }

	public byte Outcome { get; init; }

	public byte Fate { get; init; }

	public int Ms { get; init; }

	public int Year { get; init; }

	public string? Name { get; init; }
}

public sealed record WireBatch(IReadOnlyList<WireStep> Steps, IReadOnlyList<WireCapacity> Capacity, bool Ended)
{
	public int? Reading { get; init; }
}

public readonly record struct WireCapacity(byte Service, int BlockedUntilMs, double Busy);

internal sealed record WireSnapshot(long CursorSeq, int ElapsedMs, bool Ended, FateTally Fates, IReadOnlyList<WireNode> Nodes, IReadOnlyList<WireTitle> Titles)
{
	public int? Reading { get; init; }
}

internal readonly record struct WireNode(byte Service, byte Stage, int Answered, int AfterRateLimit, int GaveUp, int Failed, int Waiting, int MeanMs);

internal readonly record struct WirePlace(byte Service, byte Stage);

internal readonly record struct WireTitle(int Title, string Name, int? Year, WirePlace? At, byte? LastIds, byte? LastDetails);

internal sealed record WireLayout(IReadOnlyList<WireSpot> Nodes, IReadOnlyList<WireSpot> Details, IReadOnlyList<WireEdge> Edges, WireBrand Sammlarr);

internal sealed record WireSpot(string Key, byte Service, int Column, bool InMemory, WireBrand Brand);

internal readonly record struct WireEdge(string? From, string To);

internal sealed record WireBrand(string Name, string Color, string Background, string Light, string Dark, double Pct);

internal static class LiveWire
{
	internal static WireStep Step(TraceStep step)
	{
		var at = new WireStep(step.Seq, step.AtMs, step.Title.GetValueOr(-1), 0);
		return step.Event switch
		{
			TitleStarted started => at with { Kind = (byte)WireKind.Started, Name = started.Name, Year = started.Year.GetValueOr(0) },
			SourceEnded => at with { Kind = (byte)WireKind.SourceEnded },
			ListedWithoutIds => at with { Kind = (byte)WireKind.Listed },
			TitleKnown => at with { Kind = (byte)WireKind.Known },
			TitleNotFoundEarlier => at with { Kind = (byte)WireKind.Earlier },
			PathStarted => at with { Kind = (byte)WireKind.Path },
			HopAsked asked => at with { Kind = (byte)WireKind.Asked, Service = (byte)asked.Service, Stage = (byte)asked.Stage },
			HopAnswered answered => at with
			{
				Kind = (byte)WireKind.Answered,
				Service = (byte)answered.Service,
				Stage = (byte)answered.Stage,
				Outcome = (byte)answered.Outcome,
				Ms = answered.Ms,
			},
			GaveUp gaveUp => at with { Kind = (byte)WireKind.GaveUp, Service = (byte)gaveUp.Service, Stage = (byte)gaveUp.Stage },
			TitleSettled settled => at with { Kind = (byte)WireKind.Settled, Fate = (byte)settled.Fate },
			SourcePage => at with { Kind = (byte)WireKind.Page },
		};
	}

	internal static WireCapacity Capacity(RemoteService service, RemoteLoad load, DateTimeOffset now, int elapsedMs)
	{
		var blockedUntil = load.BlockedUntil.When(until => until > now).Map(until => elapsedMs + (int)(until - now).TotalMilliseconds);
		return new WireCapacity((byte)service, blockedUntil.GetValueOr(0), load.Busy);
	}

	internal static WireSnapshot Snapshot(TraceSnapshot snapshot)
	{
		return new WireSnapshot(
			snapshot.Cursor.Seq,
			snapshot.ElapsedMs,
			snapshot.Ended.IsSome(),
			snapshot.Fates,
			[.. snapshot.Nodes.Select(Node)],
			[.. snapshot.Titles.Select(Title)]
		)
		{
			Reading = snapshot.Progress.Reading.ToNullable(),
		};
	}

	internal static WireLayout Layout(RunLayout layout, MediaProviderBrandingService branding, Func<string, string> asset)
	{
		WireBrand Brand(BrandingInfo brand, string name)
		{
			var art = brand.Tile.Art;
			return new WireBrand(name, brand.BrandColor, brand.Tile.Background, asset(art.Light), asset(art.Dark), brand.Tile.ArtPercent / 100d);
		}

		WireSpot Spot(RemoteService service, int column, bool inMemory)
		{
			return new WireSpot(Key(service), (byte)service, column, inMemory, Brand(branding.GetBrandingInfo(service), branding.Name(service)));
		}

		return new WireLayout(
			[.. layout.Ids.Select(node => Spot(node.Service, node.Column, node.InMemory))],
			[.. layout.Details.Select(service => Spot(service, 0, false))],
			[.. layout.Edges.Select(edge => new WireEdge(edge.From.Map(Key).ToNullable(), Key(edge.To)))],
			Brand(branding.Brands(new SammlarrAccounts())[0], "Sammlarr")
		);
	}

	private static string Key(RemoteService service) => service.ToString().ToLowerInvariant();

	private static WireNode Node(NodeTally node)
	{
		return new WireNode(
			(byte)node.Node.Service,
			(byte)node.Node.Stage,
			node.Answered,
			node.AfterRateLimit,
			node.GaveUp,
			node.Failed,
			node.Waiting,
			node.MeanMs
		);
	}

	private static WireTitle Title(TitleInFlight title)
	{
		return new WireTitle(
			title.Title,
			title.Name,
			title.Year.ToNullable(),
			title.At.Map(place => new WirePlace((byte)place.Service, (byte)place.Stage)).ToNullable(),
			title.LastIds.Map(service => (byte)service).ToNullable(),
			title.LastDetails.Map(service => (byte)service).ToNullable()
		);
	}
}
