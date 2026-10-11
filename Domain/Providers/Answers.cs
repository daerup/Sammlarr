namespace Domain.Providers;

public static class Answers
{
	extension<T>(Answer<T> answer)
		where T : notnull
	{
		public bool IsDefinitive => answer is Found<T> or NoMatch;

		public Answer<TOut> Map<TOut>(Func<T, TOut> map)
			where TOut : notnull
		{
			return answer switch
			{
				Found<T> found => new Found<TOut>(map(found.Value)),
				NoMatch noMatch => noMatch,
				Unavailable unavailable => unavailable,
				Faulted faulted => faulted,
			};
		}

		public Optional<T> ToOptional()
		{
			return answer switch
			{
				Found<T> found => Some.Of(found.Value),
				NoMatch or Unavailable or Faulted => new None(),
			};
		}
	}
}
