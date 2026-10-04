using Domain.Wantlist.Arr;

namespace UI.Features.Settings.Components.Providers;

internal sealed record ArrTagPreviewRow(string Title, string Detail, IReadOnlyList<string> Labels);

internal static class ArrTagPreview
{
	internal static IReadOnlyList<ArrTagPreviewRow> Rows(ArrTagScheme scheme, IReadOnlyList<ArrTagUser> users, Optional<ArrTagFeed> feed)
	{
		var byHand = users.Take(2).Select(user => new ArrTagPreviewRow(user.Name, "Added by hand", scheme.Labels(user, new None()))).ToList();
		var fromFeed = users
			.Take(1)
			.Select(user => feed.Map(added => new ArrTagPreviewRow(added.Name, $"From {user.Name}'s feed", scheme.Labels(user, Some.Of(added)))))
			.WhereSome();
		return [.. byHand.Take(1), .. fromFeed, .. byHand.Skip(1)];
	}
}
