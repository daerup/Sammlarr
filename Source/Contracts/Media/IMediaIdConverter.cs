namespace Contracts.Media;

public interface IMediaIdConverter
{
	async Task<TMediaId> ConvertToAsync<TMediaId>(IMediaId mediaId, MediaType mediaType)
		where TMediaId : IMediaId
	{
		return (await ConvertAsync(mediaId, mediaType)).OfType<TMediaId>().Single();
	}
	async Task<TMediaId?> ConvertToOrDefaultAsync<TMediaId>(IMediaId mediaId, MediaType mediaType)
		where TMediaId : struct, IMediaId
	{
		var converted = (await ConvertAsync(mediaId, mediaType)).OfType<TMediaId>().ToList();
		return converted.Any() ? converted.Single() : null;
	}

	Task<List<IMediaId>> ConvertAsync(IMediaId mediaId, MediaType mediaType);
}
