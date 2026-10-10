namespace Domain.Providers;

public static class Answers
{
	extension<T>(Answer<T> answer)
		where T : notnull
	{
		public bool IsDefinitive => answer is Found<T> or NoMatch;

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
