using Contracts.Identity;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using SkiaSharp;

namespace API.Pictures;

internal sealed class PictureGrids(HttpClient http, ILogger<PictureGrids> logger) : IPictureGrids
{
	private const int Side = 48;

	private const int MostPixels = 2048 * 2048;

	private static readonly SKImageInfo Grid = new(Side, Side, SKColorType.Rgba8888, SKAlphaType.Unpremul);

	private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

	public async Task<Optional<byte[]>> ReadAsync(string url, CancellationToken cancellationToken = default)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var picture) || (picture.Scheme != Uri.UriSchemeHttps && picture.Scheme != Uri.UriSchemeHttp))
		{
			return new None();
		}

		try
		{
			return await FetchAsync(picture, cancellationToken);
		}
		catch (Exception ex)
			when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException or RateLimiterRejectedException
				|| (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
			)
		{
			logger.LogInformation("The picture at {Url} could not be fetched: {Reason}", picture, ex.Message);
			return new None();
		}
	}

	private async Task<Optional<byte[]>> FetchAsync(Uri picture, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync(picture, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogInformation("The picture at {Url} answered {Status}", picture, (int)response.StatusCode);
			return new None();
		}

		return Decoded(await response.Content.ReadAsByteArrayAsync(cancellationToken));
	}

	private static Optional<byte[]> Decoded(byte[] bytes)
	{
		using var data = SKData.CreateCopy(bytes);
		using var codec = SKCodec.Create(data);
		if (codec is null || (long)codec.Info.Width * codec.Info.Height > MostPixels)
		{
			return new None();
		}

		using var bitmap = SKBitmap.Decode(codec);
		using var grid = bitmap?.Resize(Grid, Sampling);
		return Optional.Of(grid).Map(scaled => scaled.GetPixelSpan().ToArray());
	}
}
