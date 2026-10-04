using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Domain.Wantlist.Arr;

public enum ArrUserTagSource
{
	[Description("Name")]
	Name,

	[Description("Id")]
	Id,
}

public enum ArrFeedTagSource
{
	[Description("List name")]
	ListName,

	[Description("Owner and list")]
	OwnerAndList,

	[Description("Feed id")]
	Id,
}

public sealed record ArrTagUser(Guid Id, string Name);

public sealed record ArrTagFeed(Guid Id, string Name, string Identifier);

public sealed record ArrTagScheme(string Prefix, bool PerUser, ArrUserTagSource UserSource, bool PerFeed, ArrFeedTagSource FeedSource, bool PlainPrefix)
{
	public static ArrTagScheme Default { get; } = new("sammlarr", true, ArrUserTagSource.Name, true, ArrFeedTagSource.ListName, false);

	public string Root => ArrTagSlug.Of(Prefix).GetValueOr("sammlarr");

	public static Optional<string> Slug(string text) => ArrTagSlug.Of(text);

	public string UserTag(ArrTagUser user) => $"{Root}-u-{ArrTagSlug.Of(UserPart(user)).GetValueOr(() => ArrTagSlug.Of(user.Id))}";

	public string KeptTag => $"{Root}-kept";

	public IReadOnlyList<string> UserTags(ArrTagUser user) =>
		[.. Enum.GetValues<ArrUserTagSource>().Select(source => (this with { UserSource = source }).UserTag(user)).Distinct()];

	public string FeedTag(ArrTagFeed feed)
	{
		var slug = ArrTagSlug.Of(FeedPart(feed)).GetValueOr(() => ArrTagSlug.Of(feed.Id));
		return IsReserved(slug) ? $"{Root}-f-{slug}" : $"{Root}-{slug}";
	}

	public IReadOnlyList<string> Labels(ArrTagUser user, Optional<ArrTagFeed> feed)
	{
		Optional<string>[] labels =
		[
			Optional.When(PlainPrefix || !PerUser, () => Root),
			Optional.When(PerUser, () => UserTag(user)),
			feed.When(_ => PerFeed).Map(FeedTag),
		];
		return [.. labels.WhereSome()];
	}

	public bool Owns(string label) => label == Root || label.StartsWith($"{Root}-", StringComparison.Ordinal);

	public bool IsUserTag(string label) => label.StartsWith($"{Root}-u-", StringComparison.Ordinal);

	private static bool IsReserved(string slug) => slug.StartsWith("u-", StringComparison.Ordinal) || slug == "kept";

	private string UserPart(ArrTagUser user)
	{
		return UserSource switch
		{
			ArrUserTagSource.Name => user.Name,
			ArrUserTagSource.Id => ArrTagSlug.Of(user.Id),
		};
	}

	private string FeedPart(ArrTagFeed feed)
	{
		return FeedSource switch
		{
			ArrFeedTagSource.ListName => feed.Name,
			ArrFeedTagSource.OwnerAndList => feed.Identifier,
			ArrFeedTagSource.Id => ArrTagSlug.Of(feed.Id),
		};
	}
}

internal static partial class ArrTagSlug
{
	public static Optional<string> Of(string text)
	{
		var kept = text.Normalize(NormalizationForm.FormKD).Where(Shown).Select(character => IsTagCharacter(character) ? char.ToLowerInvariant(character) : '-');
		var slug = Dashes().Replace(string.Concat(kept), "-").Trim('-');
		return Optional.When(slug.Length > 0, () => slug);
	}

	public static string Of(Guid id) => id.ToString("N");

	private static bool Shown(char character)
	{
		var category = CharUnicodeInfo.GetUnicodeCategory(character);
		return category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.Format);
	}

	private static bool IsTagCharacter(char character) => char.IsAsciiLetterOrDigit(character);

	[GeneratedRegex("-{2,}")]
	private static partial Regex Dashes();
}
