using Contracts.Media;
using Domain.Media;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class MediaCatalogSteps(MediaCatalogContext catalog)
{
	[Given("media:")]
	public void GivenMedia(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaDeclarationRow>())
		{
			var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
			var ids = row.Ids ?? throw new InvalidOperationException("A 'media:' row must declare an 'ids' column.");
			catalog.Register(ids, type, row.As, row.Year);
		}
	}
}
