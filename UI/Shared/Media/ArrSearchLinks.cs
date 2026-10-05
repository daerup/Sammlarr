using Domain.Media;
using Domain.Wantlist.Arr;

namespace UI.Shared.Media;

internal sealed record ArrSearchLink(ArrApp App, string Term, string Url);

internal static class ArrSearchLinks
{
	public static Optional<ArrSearchLink> For(ArrSinkConfig config, IReadOnlyList<IExternalMediaId> ids)
	{
		return Enum.GetValues<ArrApp>()
			.Select(app => SearchId(app, ids).Map(id => (App: app, Id: id)))
			.WhereSome()
			.FirstOrNone()
			.Map(found => config.Of(found.App).Url.Map(Address).Map(address => Link(found.App, found.Id, address)));
	}

	private static Optional<IExternalMediaId> SearchId(ArrApp app, IReadOnlyList<IExternalMediaId> ids)
	{
		return app.SearchableBy.SelectMany(provider => ids.Where(id => id.Source == provider && id.Type == app.Type)).FirstOrNone();
	}

	private static Optional<Uri> Address(string url)
	{
		return Optional.When(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", () => uri!);
	}

	private static ArrSearchLink Link(ArrApp app, IExternalMediaId id, Uri address)
	{
		var term = ArrSearchTerm.Of(id);
		return new ArrSearchLink(app, term, $"{address.GetLeftPart(UriPartial.Path).TrimEnd('/')}/add/new?term={Uri.EscapeDataString(term)}");
	}
}
