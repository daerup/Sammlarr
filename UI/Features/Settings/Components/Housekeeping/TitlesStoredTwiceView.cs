using System.Globalization;
using Contracts.Media;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using UI.Shared.Media;
using UI.Shared.Services.Branding;

namespace UI.Features.Settings.Components.Housekeeping;

internal static class TitlesStoredTwiceView
{
	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	public static IReadOnlyList<StoredTwice> ToDecide(IEnumerable<StoredTwice> pairs)
	{
		return [.. pairs.Where(pair => pair.Note.State is Undecided).OrderByDescending(pair => pair.Note.Attempts > 0).ThenBy(pair => pair.Note.SeenAt)];
	}

	public static IReadOnlyList<StoredTwice> KeptApart(IEnumerable<StoredTwice> pairs)
	{
		return [.. pairs.Where(pair => pair.Note.State is Distinct).OrderBy(pair => pair.Note.SeenAt)];
	}

	public static Optional<MediaDetails> Details(StoredTitle title) => title.Media.Cached.Map(cached => cached.Details);

	public static string Name(StoredTitle title) => DetailsTitle(title).GetValueOr(() => FirstId(title));

	public static Optional<int> Year(StoredTitle title) => Details(title).Map(details => Optional.Of(details.ReleaseDate)).Map(date => date.Year);

	public static bool YearsDiffer(StoredTwice pair) => new[] { Year(pair.Held), Year(pair.Other) }.WhereSome().Distinct().Count() > 1;

	public static IReadOnlySet<MediaProvider> Clashing(StoredTwice pair)
	{
		return pair.Held.Media.Identity.Keys.Select(key => key.Source).Intersect(pair.Other.Media.Identity.Keys.Select(key => key.Source)).ToHashSet();
	}

	public static string Plain(StoredTwice pair)
	{
		var (held, other) = (Name(pair.Held), Name(pair.Other));
		var years = new[] { Year(pair.Held), Year(pair.Other) }.WhereSome().Distinct().ToList();
		var shown = Name(Shown(pair));
		var together = years.Count == 0 ? shown : $"{shown} ({string.Join(" and ", years)})";
		return Details(pair.Held)
			.Map(_ => Details(pair.Other).Map(_ => held == other ? together : $"{held} and {other}").GetValueOr(together))
			.GetValueOr(together);
	}

	public static string Claim(IdentityConflict note, MediaProviderBrandingService branding)
	{
		var names = note.Sources.Order().Select(source => branding.Name(source)).ToList();
		var verb = names.Count == 1 ? "says" : "say";
		return $"{string.Join(" and ", names)} {verb} they are one {Kind(note)}";
	}

	private static string Asking(IdentityConflict note, MediaProviderBrandingService branding)
	{
		return note.LastAsk.Match(
			ask => $"{branding.Name(ask.Service)} {Outcome(ask.Outcome)} {Day(ask.At)} (asked {Times(note.Attempts)}) · next ask {Day(note.AskAfter)}",
			() => "Not asked yet · the nightly pass asks a second source"
		);
	}

	public static string Reason(StoredTwice pair, UserId me, MediaProviderBrandingService branding)
	{
		return pair.Note.State switch
		{
			Undecided => Asking(pair.Note, branding),
			Distinct distinct => Reason(pair, distinct, me, branding),
		};
	}

	public static string Short(IdentityConflict note, UserId me, MediaProviderBrandingService branding)
	{
		return note.State switch
		{
			Undecided when note.Attempts == 0 => "new, asking tonight",
			Undecided => $"asked {Times(note.Attempts)}, next {Day(note.AskAfter)}",
			Distinct distinct => KeptShort(distinct.Reason, me, branding),
		};
	}

	public static bool Contradicts(StoredTwice pair)
	{
		return IdentityConflict.Contradicts(pair.Held.Media.Identity.Keys, pair.Other.Media.Identity.Keys).Match(_ => true, () => false);
	}

	public static StoredTitle Shown(StoredTwice pair) => Details(pair.Held).Map(_ => pair.Held).GetValueOr(pair.Other);

	public static Optional<string> Poster(StoredTwice pair)
	{
		return Details(pair.Held).Map(details => Optional.Of(details.PosterUrl)).Or(() => Details(pair.Other).Map(details => Optional.Of(details.PosterUrl)));
	}

	public static Optional<string> DetailsTitle(StoredTitle title) => Details(title).Map(details => Optional.Of(details.Title)).When(name => name.Length > 0);

	public static string ApplyTitle(int changes) =>
		changes == 0 ? "Nothing to apply" : FormattableString.CurrentCulture($"Apply {changes} {Plural(changes, "change")}?");

	public static (string Label, string Icon) Unmarked(IdentityConflict note)
	{
		return note.State switch
		{
			Undecided => ("Left to the nightly pass", "moon"),
			Distinct => ("Stays kept apart", "split"),
		};
	}

	public static bool Tried(IdentityConflict note) => note.State is Undecided && note.Attempts > 0;

	public static string Uses(TitleUses uses)
	{
		List<string> parts = [];
		if (uses.Wantlists > 0)
		{
			parts.Add(FormattableString.CurrentCulture($"on {uses.Wantlists} {Plural(uses.Wantlists, "wantlist")}"));
		}

		if (uses.Feeds > 0)
		{
			parts.Add(FormattableString.CurrentCulture($"in {uses.Feeds} {Plural(uses.Feeds, "feed")}"));
		}

		return parts.Count == 0 ? "on no wantlist and in no feed" : string.Join(" · ", parts);
	}

	public static string Staged(ConflictChoice choice)
	{
		return choice switch
		{
			ConflictChoice.Merge => "Will merge",
			ConflictChoice.KeepApart => "Will keep apart",
			ConflictChoice.Leave => "Left to the nightly pass",
		};
	}

	public static string StagedIcon(ConflictChoice choice)
	{
		return choice switch
		{
			ConflictChoice.Merge => "merge",
			ConflictChoice.KeepApart => "split",
			ConflictChoice.Leave => "moon",
		};
	}

	public static string Waiting(int undecided, int keptApart)
	{
		var open =
			undecided > 0
				? FormattableString.CurrentCulture(
					$"{undecided} {Plural(undecided, "pair")} that may be one title. The nightly pass checks them with a second source; settle one now if you know."
				)
				: "Nothing to decide: every pair found so far is settled.";
		var kept = keptApart > 0 ? FormattableString.CurrentCulture($" {keptApart} {Plural(keptApart, "pair")} {Are(keptApart)} kept apart.") : string.Empty;
		return open + kept;
	}

	private static string Reason(StoredTwice pair, Distinct distinct, UserId me, MediaProviderBrandingService branding)
	{
		var note = pair.Note;
		return distinct.Reason switch
		{
			Contradiction contradiction =>
				$"Kept apart: both have a {contradiction.Source.Label} id ({Value(pair.Held, contradiction.Source)} and {Value(pair.Other, contradiction.Source)}), so they are two {Kind(note)}s",
			TypesDiffer => "Kept apart: one is a film and the other a show",
			AnsweredElsewhere answered =>
				$"Kept apart {Day(distinct.At)}: {branding.Name(answered.By)} says it is {answered.Answer.Source.Label} {answered.Answer.Value}, not {note.Claimed.Value}",
			KeptApartByHand kept => $"Kept apart by {(kept.By == me ? "you" : "hand")}, {Day(distinct.At)}",
		};
	}

	private static string KeptShort(DistinctReason reason, UserId me, MediaProviderBrandingService branding)
	{
		return reason switch
		{
			Contradiction contradiction => $"two {contradiction.Source.Label} ids",
			TypesDiffer => "a film and a show",
			AnsweredElsewhere answered => $"{branding.Name(answered.By)} maps it elsewhere",
			KeptApartByHand kept => kept.By == me ? "kept apart by you" : "kept apart by hand",
		};
	}

	private static string Value(StoredTitle title, MediaProvider source)
	{
		return title.Media.Identity.Keys.FirstOrNone(key => key.Source == source).Map(key => key.Value).GetValueOr(string.Empty);
	}

	private static string FirstId(StoredTitle title)
	{
		return title.Media.Identity.Keys.OrderBy(key => key.Source).FirstOrNone().Map(key => $"{key.Source.Label} {key.Value}").GetValueOr(string.Empty);
	}

	private static string Kind(IdentityConflict note)
	{
		return note.Claimed.Type switch
		{
			MediaType.Movie => "film",
			MediaType.Show => "show",
			MediaType.Unknown => "title",
		};
	}

	private static string Outcome(AskOutcome outcome)
	{
		return outcome switch
		{
			AskOutcome.NoMatch => "didn't know it",
			AskOutcome.Unavailable => "gave no answer",
			AskOutcome.Faulted => "failed to answer",
		};
	}

	private static string Times(int attempts)
	{
		return attempts switch
		{
			1 => "once",
			2 => "twice",
			_ => FormattableString.CurrentCulture($"{attempts} times"),
		};
	}

	private static string Day(DateTimeOffset at) => at.ToLocalTime().ToString("MMM d", Invariant);

	private static string Plural(int count, string noun) => count == 1 ? noun : $"{noun}s";

	private static string Are(int count) => count == 1 ? "is" : "are";
}
