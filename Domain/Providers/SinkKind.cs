namespace Domain.Providers;

public enum SinkKind
{
	Plex,
	Arr,
}

public static class SinkKinds
{
	extension(SinkKind sink)
	{
		public Service Service
		{
			get
			{
				return sink switch
				{
					SinkKind.Plex => RemoteService.Plex,
					SinkKind.Arr => new Arr(),
				};
			}
		}
	}
}
