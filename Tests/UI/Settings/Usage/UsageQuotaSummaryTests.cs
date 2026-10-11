using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Usage;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public partial class UsageQuotaSummaryTests : BunitContext
{
	private static readonly string Resets = UsageSamples.Local(UsageSamples.Reset);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ANormalMorning_SaysWhatIsLeftAndWhenTitleLookupsWait()
	{
		var cut = RenderSummary(UsageSamples.Quota(UsageSamples.Allowance(191)));

		await Assert
			.That(Text(cut))
			.IsEqualTo(
				$"191 of 1,000 left today · resets at {Resets} · at the pace of the last 3 hours, title lookups wait from about {UsageSamples.Local(UsageSamples.Reset.AddHours(11))}"
			);
	}

	[Test]
	public async Task Render_AQuietMorning_SaysThePaceLastsUntilTheReset()
	{
		var cut = RenderSummary(UsageSamples.Quota(UsageSamples.Allowance(995), [2, 0, 0, 0, 0, 0, 0, 1, 1, 1]));

		await Assert.That(Text(cut)).IsEqualTo($"995 of 1,000 left today · resets at {Resets} · at this pace it lasts until the reset");
	}

	[Test]
	public async Task Render_OnceTheReserveIsReached_SaysSinceWhenTitleLookupsWait()
	{
		var reached = UsageSamples.Morning.AddMinutes(-9);
		var allowance = UsageSamples.Allowance(50) with { ReserveReachedAt = Some.Of(reached) };

		var cut = RenderSummary(UsageSamples.Quota(allowance, [2, 419, 79, 1, 0, 4, 17, 65, 104, 259]));

		await Assert
			.That(Text(cut))
			.IsEqualTo(
				$"50 of 1,000 left · title lookups wait since {UsageSamples.Local(reached)}, until the reset at {Resets} · feed list pages may use the last 50"
			);
	}

	[Test]
	public async Task Render_WithRequestsUsedOutsideSammlarr_CountsThem()
	{
		var cut = RenderSummary(UsageSamples.Quota(UsageSamples.Allowance(71) with { Outside = Some.Of(120L) }));

		await Assert.That(Text(cut)).StartsWith($"71 of 1,000 left today · resets at {Resets} · 120 used outside Sammlarr today · ");
	}

	[Test]
	public async Task Render_WithoutTheAllowance_SaysItShowsAfterTheNextReset()
	{
		var allowance = UsageSamples.Allowance(191) with { Allowance = new None(), DayBefore = new None() };

		var cut = RenderSummary(UsageSamples.Quota(allowance));

		await Assert.That(Text(cut)).StartsWith($"191 left today · resets at {Resets} · the day's allowance shows after the next reset · ");
	}

	[Test]
	public async Task Render_WithoutAnAnswerSinceTheReset_SaysWhatIsLeftShowsAfterTheNextCall()
	{
		var cut = RenderSummary(UsageSamples.Unanswered());

		await Assert.That(Text(cut)).IsEqualTo($"MDBList resets at {Resets} · no answer since, so what is left shows after its next call");
	}

	[Test]
	public async Task Render_ForSevenDays_SaysTheDailyAllowanceAndWhenItResets()
	{
		var cut = RenderSummary(UsageSamples.Week(UsageSamples.Quota(UsageSamples.Allowance(191))), UsageRange.Week);

		await Assert.That(Text(cut)).IsEqualTo($"1,000 a day, resets at {Resets}");
	}

	[Test]
	public async Task Render_ForSevenDaysAnsweredWithoutTheAllowance_SaysItShowsAfterTheNextReset()
	{
		var cut = RenderSummary(UsageSamples.Quota(UsageSamples.Allowance(191) with { Allowance = new None() }), UsageRange.Week);

		await Assert.That(Text(cut)).IsEqualTo($"MDBList resets at {Resets} · the day's allowance shows after the next reset");
	}

	[Test]
	public async Task Render_ForThirtyDaysWithoutAnAnswer_SaysWhenTheAllowanceShows()
	{
		var cut = RenderSummary(UsageSamples.Unanswered(), UsageRange.Month);

		await Assert.That(Text(cut)).IsEqualTo($"MDBList resets at {Resets} · its daily allowance shows after its next answer");
	}

	private static string Text(IRenderedComponent<UsageQuotaSummary> cut)
	{
		var parts = cut.Find("[data-testid=usage-quota-summary]").Children.Select(part => Spaces().Replace(part.TextContent, " ").Trim());
		return string.Join(" ", parts.Where(part => part.Length > 0));
	}

	private IRenderedComponent<UsageQuotaSummary> RenderSummary(UsageQuota quota, UsageRange range = UsageRange.Day)
	{
		return Render<UsageQuotaSummary>(parameters => parameters.Add(summary => summary.Quota, quota).Add(summary => summary.Range, range));
	}

	[GeneratedRegex(@"\s+")]
	private static partial Regex Spaces();
}
