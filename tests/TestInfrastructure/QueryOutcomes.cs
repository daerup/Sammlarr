using Domain.Processing;

namespace TestInfrastructure;

public enum OutcomeCase
{
	Qualified,
	Culled,
	Truncated,
}

public static class QueryOutcomes
{
	extension(QueryOutcome outcome)
	{
		public OutcomeCase Case
		{
			get
			{
				return outcome switch
				{
					Qualified => OutcomeCase.Qualified,
					Culled => OutcomeCase.Culled,
					Truncated => OutcomeCase.Truncated,
				};
			}
		}

		public bool ConsumesSlot => outcome is Qualified { ConsumesSlot: true };
	}

	extension(OutcomeCase @case)
	{
		public QueryOutcome Outcome
		{
			get
			{
				return @case switch
				{
					OutcomeCase.Qualified => new Qualified(ConsumesSlot: true, Rank: new None()),
					OutcomeCase.Culled => new Culled(new FilterChecks([], [])),
					OutcomeCase.Truncated => new Truncated(new SlotRank(1, 1)),
				};
			}
		}
	}
}
