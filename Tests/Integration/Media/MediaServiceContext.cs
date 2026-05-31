using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;

namespace Integration.Tests.Media;

public sealed class MediaServiceContext
{
	public IMediaDetail? Detail { get; set; }
	public Identification? Identification { get; set; }
	public IReadOnlyList<Identification?>? Identifications { get; set; }
}
