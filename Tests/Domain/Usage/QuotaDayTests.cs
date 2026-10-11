using CsCheck;
using Domain.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Usage;

public class QuotaDayTests
{
	private static readonly DateTimeOffset Reset = new(2026, 10, 11, 2, 0, 0, TimeSpan.Zero);

	private static readonly DateTimeOffset Now = Reset.AddHours(9).AddMinutes(40);

	private static readonly long[] SpentPerHour = [2, 419, 79, 1, 0, 4, 17, 65, 104, 118];

	private static readonly Gen<(long[] Spent, long Remaining)> Days = Gen.Select(Gen.Long[0, 200].Array[10], Gen.Long[0, 1000]);

	[Test]
	public async Task Of_ForADayWithNothingSpentOutside_StartsAtTheAllowanceAndEndsAtWhatIsLeft()
	{
		var day = QuotaDay.Of(Spent(SpentPerHour), Allowance(191), Now);

		await Assert
			.That(day.Points.Select(point => point.Left))
			.IsEquivalentTo([1000L, 998, 579, 500, 499, 499, 495, 478, 413, 309, 191], CollectionOrdering.Matching);
		await Assert.That(day.Points[0].At).IsEqualTo(Reset);
		await Assert.That(day.Points[^1].At).IsEqualTo(Now);
		var reserveAt = await Assert.That(day.ReserveAt).IsSome();
		await Assert.That(reserveAt).IsBetween(Reset.AddHours(10).AddMinutes(58), Reset.AddHours(10).AddMinutes(59));
	}

	[Test]
	public async Task Of_ForADayWithRequestsSpentOutside_StartsWhereSammlarrsOwnCallsBegan()
	{
		var day = QuotaDay.Of(Spent(SpentPerHour), Allowance(71) with { Outside = Some.Of(120L) }, Now);

		await Assert
			.That(day.Points.Select(point => point.Left))
			.IsEquivalentTo([880L, 878, 459, 380, 379, 379, 375, 358, 293, 189, 71], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Of_OnceTheReserveWasReached_WaitsFromThatMinuteToTheReset()
	{
		var reached = Reset.AddHours(8).AddMinutes(30);

		var day = QuotaDay.Of(Spent(SpentPerHour), Allowance(40) with { ReserveReachedAt = Some.Of(reached) }, Now);

		await Assert.That(day.Waits).IsEquivalentTo([new QuotaWait(reached, Reset.AddDays(1))]);
		await Assert.That(day.ReserveAt).IsNone();
	}

	[Test]
	public async Task Of_BelowTheReserveWithoutTheMinuteItWasReached_WaitsFromTheFirstHourBelowIt()
	{
		var day = QuotaDay.Of(Spent(SpentPerHour), Allowance(40), Now);

		await Assert.That(day.Waits).IsEquivalentTo([new QuotaWait(Now, Reset.AddDays(1))]);
	}

	[Test]
	public async Task Of_WithTheDayBefore_DrawsItsHoursUpToTheResetAndItsWait()
	{
		var reached = Reset.AddHours(-4).AddMinutes(-56);
		IReadOnlyList<(DateTimeOffset Hour, long Spent)> yesterday = [.. Enumerable.Range(1, 14).Select(hours => (Reset.AddHours(-hours), 25L))];
		var allowance = Allowance(191) with { DayBefore = Some.Of(new DayEnd(30, Reset, Some.Of(reached))) };

		var day = QuotaDay.Of([.. yesterday, .. Spent(SpentPerHour)], allowance, Now);

		await Assert.That(day.Points[0]).IsEqualTo(new QuotaPoint(Reset.AddHours(-14), 380));
		await Assert.That(day.Points[14]).IsEqualTo(new QuotaPoint(Reset, 30));
		await Assert.That(day.Points[15]).IsEqualTo(new QuotaPoint(Reset, 1000));
		await Assert.That(day.Waits).IsEquivalentTo([new QuotaWait(reached, Reset)]);
	}

	[Test]
	public async Task Of_WithADayBeforeThatEndedBelowTheReserveWithoutTheMinute_WaitsFromItsFirstHourBelow()
	{
		IReadOnlyList<(DateTimeOffset Hour, long Spent)> yesterday = [.. Enumerable.Range(1, 14).Select(hours => (Reset.AddHours(-hours), 5L))];
		var allowance = Allowance(191) with { DayBefore = Some.Of(new DayEnd(30, Reset, new None())) };

		var day = QuotaDay.Of([.. yesterday, .. Spent(SpentPerHour)], allowance, Now);

		await Assert.That(day.Waits).IsEquivalentTo([new QuotaWait(Reset.AddHours(-4), Reset)]);
	}

	[Test]
	public async Task Of_WithAnEndedDayOlderThanTheLastReset_StartsAtTheReset()
	{
		var allowance = Allowance(191) with { DayBefore = Some.Of(new DayEnd(30, Reset.AddDays(-1), new None())) };

		var day = QuotaDay.Of(Spent(SpentPerHour), allowance, Now);

		await Assert.That(day.Points[0]).IsEqualTo(new QuotaPoint(Reset, 1000));
	}

	[Test]
	public async Task Of_WhenThePaceLastsUntilTheReset_SaysNoTimeForTheReserve()
	{
		var day = QuotaDay.Of(Spent([2, 0, 0, 0, 0, 0, 0, 1, 1, 1]), Allowance(995), Now);

		await Assert.That(day.ReserveAt).IsNone();
		await Assert.That(day.Waits).IsEmpty();
	}

	[Test]
	public void Of_ForAnyDay_EndsAtWhatIsLeft()
	{
		Days.Sample((spent, remaining) => Points(spent, remaining)[^1].Left == remaining);
	}

	[Test]
	public void Of_ForAnyDay_NeverRisesBetweenResets()
	{
		Days.Sample(NeverRises);
	}

	[Test]
	public void Of_ForAnyDay_NeverClimbsAboveTheAllowance()
	{
		Days.Sample((spent, remaining) => Points(spent, remaining).All(point => point.Left <= 1000));
	}

	private static bool NeverRises(long[] spent, long remaining)
	{
		var points = Points(spent, remaining);
		return points.Zip(points.Skip(1)).All(pair => pair.First.Left >= pair.Second.Left);
	}

	private static IReadOnlyList<QuotaPoint> Points(long[] spent, long remaining) => QuotaDay.Of(Spent(spent), Allowance(remaining), Now).Points;

	private static IReadOnlyList<(DateTimeOffset Hour, long Spent)> Spent(long[] perHour)
	{
		return [.. perHour.Select((spent, hour) => (Reset.AddHours(hour), spent))];
	}

	private static QuotaAllowance Allowance(long remaining)
	{
		return new QuotaAllowance(remaining, Reset.AddDays(1), Some.Of(1000L), new None(), new None(), 50, 0.5);
	}
}
