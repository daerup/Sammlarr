namespace Contracts.Media;

public interface IMediaIdConverter
{
	async Task<TMediaId?> ConvertToOrDefaultAsync<TMediaId>(IMediaId mediaId)
		where TMediaId : struct, IMediaId
	{
		var converted = (await ConvertAsync(mediaId)).OfType<TMediaId>().ToList();
		return converted.Any() ? converted.Single() : null;
	}

	Task<List<IMediaId>> ConvertAsync(IMediaId mediaId);
}
