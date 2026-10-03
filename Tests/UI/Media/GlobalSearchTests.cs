using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class GlobalSearchTests : BunitContext
{
	private const string Movie = "Inception";
	private const string Person = "Christopher Nolan";

	private static readonly MediaMatchResult Matched = new MatchedMedia(
		new MediaIdentity(MediaId.From(Guid.NewGuid()), [PlexId.From("1", MediaType.Movie)]),
		new MovieDetails { Title = Movie }
	);

	private static readonly MediaMatchResult Unmatched = new UnmatchedMedia([PlexId.From("123", MediaType.Unknown)], new UnknownDetails { Title = Person });

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(IMediaService.Mock().Object);

		var providerConfig = IProviderConfigService.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(providerConfig.Object);

		var wantlist = IWantlistService.Mock();
		wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		Services.AddSingleton(wantlist.Object);

		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(blocklist.Object);

		var search = IMediaSearchService.Mock();
		search.SearchAsync(Any(), Any(), Any()).Returns([Matched, Unmatched]);
		Services.AddSingleton(search.Object);

		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Search_WithAMatchedAndAnUnmatchedResult_RendersBothButTogglesOnlyTheMatched()
	{
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Person });

		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		var rows = cut.FindAll(".search-result-row");
		await Assert.That(rows).Count().IsEqualTo(2);
		await Assert.That(rows.Single(row => row.TextContent.Contains(Movie)).QuerySelectorAll(".search-result-toggle-btn")).IsNotEmpty();
		await Assert.That(rows.Single(row => row.TextContent.Contains(Person)).QuerySelectorAll(".search-result-toggle-btn")).IsEmpty();
	}
}
