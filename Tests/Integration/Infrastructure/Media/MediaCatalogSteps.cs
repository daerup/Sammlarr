using System;
using Contracts.Media;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class MediaCatalogSteps(MediaCatalogContext catalog)
{
	[Given("the following media:")]
	[Given("additional media:")]
	public void GivenMedia(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
		{
			var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
			var ids = row.Ids ?? throw new InvalidOperationException($"Media '{row.Media}' must declare an 'ids' column.");
			catalog.Register(row.Media!, type, ids);
		}
	}
}
