using System.Globalization;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Ratings;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Ratings;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class RatingsSectionTests : BunitContext
{
	private readonly Mock<IRatingService> _ratings = IRatingService.Mock();

	[Before(Test)]
	public void Setup()
	{
		_ratings.GetDefaultAsync(Any()).Returns(new RatingStrategy(AveragedRating.Everything));
		_ratings.GetAvailableAsync(Any()).Returns([RatingSource.Imdb]);
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(_ratings.Object);
		Services.AddSingleton(new RatingContext(_ratings.Object));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task ImdbFile_OnceLoaded_SaysInEnglishWhichDaysFileTheRatingsComeFrom()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");
		var loadedAt = new DateTimeOffset(2026, 10, 6, 4, 30, 0, TimeSpan.Zero);
		_ratings.GetImdbFileDateAsync(Any()).Returns(Some.Of(loadedAt));

		var cut = Render<RatingsSection>();

		cut.WaitForState(() => cut.FindAll(".imdb-file").Count == 1);
		await Assert.That(cut.Find(".imdb-file").TextContent).Contains(loadedAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture));
	}

	[Test]
	public async Task ImdbFile_BeforeTheFirstLoad_SaysItIsNotLoadedYet()
	{
		_ratings.GetImdbFileDateAsync(Any()).Returns(new None());

		var cut = Render<RatingsSection>();

		cut.WaitForState(() => cut.FindAll(".imdb-file").Count == 1);
		var line = cut.Find(".imdb-file").TextContent;
		await Assert.That(line).Contains("not been loaded yet");
		await Assert.That(line.Any(char.IsDigit)).IsFalse();
	}
}
