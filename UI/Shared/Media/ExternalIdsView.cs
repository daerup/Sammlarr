using Contracts.DI;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using UI.Shared.Services.Branding;
using UI.Shared.Services.Links;

namespace UI.Shared.Media;

internal sealed record ExternalIdCard(BrandingInfo Brand, string Subtext, string Page, string Label);

internal sealed record ExternalIdLink(BrandingInfo Brand, IExternalMediaId Id, Optional<string> Page)
{
	public string Label => $"View {Id.Value} on {Brand.DisplayName} (opens in new tab)";
}

internal sealed class ExternalIdsView(
	IKeyedServiceIndex<MediaProvider, IExternalPageLinker> linkers,
	MediaProviderBrandingService branding,
	ICurrentUser currentUser,
	IProviderConfigService providerConfigs,
	IArrSinkConfigService arrConfigs
)
{
	private static readonly ValueSet<MediaProvider> ShownWithRatingsOnly =
	[
		MediaProvider.RottenTomatoes,
		MediaProvider.Metacritic,
		MediaProvider.Letterboxd,
		MediaProvider.Trakt,
	];

	private Task<ArrSinkConfig>? _arr;

	public IReadOnlyList<ExternalIdCard> IdCards(IReadOnlyList<IExternalMediaId> ids, Optional<string> title)
	{
		return
		[
			.. ids.Where(id => !ShownWithRatingsOnly.Contains(id.Source))
				.Select(id => linkers[id.Source].PageOf([id], title).Map(page => IdCard(id, page)))
				.WhereSome(),
		];
	}

	public IReadOnlyList<ExternalIdLink> IdLinks(IEnumerable<IExternalMediaId> ids, Optional<string> title)
	{
		return [.. ids.OrderBy(id => id.Source).Select(id => new ExternalIdLink(branding.GetBrandingInfo(id.Source), id, linkers[id.Source].PageOf([id], title)))];
	}

	public async Task<IReadOnlyList<ExternalIdCard>> WithArrCardAsync(IReadOnlyList<ExternalIdCard> idCards, IReadOnlyList<IExternalMediaId> ids)
	{
		var arrCard = await ArrAsync().Map(config => ArrSearchLinks.For(config, ids)).Map(ArrCard);
		return arrCard.Match(card => [.. idCards, card], () => idCards);
	}

	private async Task<Optional<ArrSinkConfig>> ArrAsync()
	{
		if (currentUser.Role != UserRole.Admin || (await providerConfigs.GetAsync()).Sink != SinkKind.Arr)
		{
			return new None();
		}

		if (_arr is { IsFaulted: true })
		{
			_arr = null;
		}

		return Some.Of(await (_arr ??= arrConfigs.GetAsync()));
	}

	private ExternalIdCard IdCard(IExternalMediaId id, string page)
	{
		var brand = branding.GetBrandingInfo(id.Source);
		return new ExternalIdCard(brand, id.Value, page, $"View {id.Value} on {brand.DisplayName} (opens in new tab)");
	}

	private ExternalIdCard ArrCard(ArrSearchLink link)
	{
		var brand = branding.GetBrandingInfo(link.App.Remote);
		return new ExternalIdCard(brand, link.Term, link.Url, $"Find {link.Term} in {brand.DisplayName} (opens in new tab)");
	}
}
