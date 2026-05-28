using Contracts.Media;
using Contracts.Media.Search;

namespace Integration.Tests.Infrastructure;

public sealed class MediaDetailContext
{
	public List<IMediaDetail>? Items { get; set; }
	public IReadOnlySet<MediaId>? Ids { get; set; }
}
