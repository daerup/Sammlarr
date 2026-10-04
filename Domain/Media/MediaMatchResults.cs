namespace Domain.Media;

public static class MediaMatchResults
{
	extension(MediaMatchResult match)
	{
		public MediaDetails Details
		{
			get
			{
				return match switch
				{
					MatchedMedia matched => matched.Details,
					UnmatchedMedia unmatched => unmatched.Details,
				};
			}
		}

		public Optional<MediaId> MatchedId
		{
			get
			{
				return match switch
				{
					MatchedMedia matched => Some.Of(matched.Identity.Id),
					UnmatchedMedia => new None(),
				};
			}
		}

		public ValueSet<IExternalMediaId> Externals
		{
			get
			{
				return match switch
				{
					MatchedMedia matched => matched.Identity.Externals,
					UnmatchedMedia unmatched => unmatched.Externals,
				};
			}
		}
	}

	extension(IEnumerable<MediaMatchResult> results)
	{
		public IEnumerable<MatchedMedia> Matched()
		{
			foreach (var result in results)
			{
				if (result is MatchedMedia matched)
				{
					yield return matched;
				}
			}
		}
	}
}
