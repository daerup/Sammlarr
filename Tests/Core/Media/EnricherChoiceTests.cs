using Core.Media;
using Domain.Media;
using Domain.Providers;

namespace Core.Tests.Media;

public class EnricherChoiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly ProviderLoad Idle = new(new None(), 0);

	[Test]
	public async Task Next_PicksTheEnricherThatFillsTheMostMissingFields() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline | MediaField.Summary,
						[Option(MediaProvider.Tvdb, MediaField.Tagline), Option(MediaProvider.Tmdb, MediaField.Tagline | MediaField.Summary)],
						Now
					)
					?.Provider
			)
			.IsEqualTo(MediaProvider.Tmdb);

	[Test]
	public async Task Next_ForEqualCoverage_PicksTheLessBusyEnricher() =>
		await Assert
			.That(
				EnricherChoice
					.Next(
						MediaField.Tagline,
						[
							Option(MediaProvider.Tmdb, MediaField.Tagline, Idle with { Busy = 0.8 }),
							Option(MediaProvider.Tvdb, MediaField.Tagline, Idle with { Busy = 0.1 }),
						],
						Now
					)
					?.Provider
			)
			.IsEqualTo(MediaProvider.Tvdb);

	[Test]
	public async Task Next_SkipsABlockedEnricherAndOneThatCannotFillAnything() =>
		await Assert
			.That(
				EnricherChoice.Next(
					MediaField.Tagline,
					[
						Option(MediaProvider.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
						Option(MediaProvider.Tvdb, MediaField.Poster),
					],
					Now
				)
			)
			.IsNull();

	[Test]
	public async Task Next_ForAnEnricherWhoseBlockHasPassed_PicksItAgain() =>
		await Assert
			.That(
				EnricherChoice
					.Next(MediaField.Tagline, [Option(MediaProvider.Tmdb, MediaField.Tagline, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(-1)) })], Now)
					?.Provider
			)
			.IsEqualTo(MediaProvider.Tmdb);

	[Test]
	public async Task BlockedFills_NamesTheFieldsOfTheBlockedEnrichersThatCouldHaveHelped() =>
		await Assert
			.That(
				EnricherChoice.BlockedFills(
					MediaField.Tagline,
					[
						Option(MediaProvider.Tmdb, MediaField.Tagline | MediaField.Summary, Idle with { BlockedUntil = Some.Of(Now.AddSeconds(30)) }),
						Option(MediaProvider.Tvdb, MediaField.Tagline),
					],
					Now
				)
			)
			.IsEqualTo(MediaField.Tagline | MediaField.Summary);

	private static EnricherOption Option(MediaProvider provider, MediaField fills, ProviderLoad? load = null) =>
		new(provider, TmdbId.From("1", MediaType.Movie), fills, load ?? Idle);
}
