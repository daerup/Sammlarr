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

internal sealed class ExternalIdsView(
	IKeyedServiceIndex<MediaProvider, IExternalPageLinker> linkers,
	MediaProviderBrandingService branding,
	ICurrentUser currentUser,
	IProviderConfigService providerConfigs,
	IArrSinkConfigService arrConfigs
)
{
	private static readonly ValueSet<MediaProvider> ShownWithRatingsOnly = [MediaProvider.RottenTomatoes];

	private Task<ArrSinkConfig>? _arr;

	public async Task<IReadOnlyList<ExternalIdCard>> CardsAsync(IReadOnlyList<IExternalMediaId> ids, Optional<string> title)
	{
		var arr = await ArrAsync();
		var arrCard = arr.Map(config => ArrSearchLinks.For(config, ids)).Map(ArrCard);
		return [.. IdCards(ids, title).Append(arrCard).WhereSome()];
	}

	private IEnumerable<Optional<ExternalIdCard>> IdCards(IReadOnlyList<IExternalMediaId> ids, Optional<string> title)
	{
		return ids.Where(id => !ShownWithRatingsOnly.Contains(id.Source)).Select(id => linkers[id.Source].PageOf([id], title).Map(page => IdCard(id, page)));
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
		var brand = branding.GetBrandingInfo(link.App);
		return new ExternalIdCard(brand, link.Term, link.Url, $"Find {link.Term} in {brand.DisplayName} (opens in new tab)");
	}
}
