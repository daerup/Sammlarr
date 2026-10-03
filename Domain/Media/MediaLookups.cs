namespace Domain.Media;

public static class MediaLookups
{
	extension(MediaLookup lookup)
	{
		public MediaDetails Details
		{
			get
			{
				return lookup switch
				{
					IdentifiedMedia identified => identified.Details,
					UnmatchedMedia unmatched => unmatched.Details,
				};
			}
		}

		public ValueSet<IExternalMediaId> Externals
		{
			get
			{
				return lookup switch
				{
					IdentifiedMedia identified => identified.Identity.Externals,
					UnmatchedMedia unmatched => unmatched.Externals,
				};
			}
		}
	}

	extension(IEnumerable<MediaLookup> lookups)
	{
		public IEnumerable<IdentifiedMedia> Identified()
		{
			foreach (var lookup in lookups)
			{
				if (lookup is IdentifiedMedia identified)
				{
					yield return identified;
				}
			}
		}
	}
}
