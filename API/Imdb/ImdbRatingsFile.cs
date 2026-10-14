using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using API.Http;
using API.Http.Lanes;
using Contracts.Datasets;
using Domain.Providers;
using Polly.Timeout;

namespace API.Imdb;

internal sealed class ImdbRatingsFile(HttpClient http) : IDataset<ImdbRatingLine>
{
	private const string Header = "tconst\taverageRating\tnumVotes";

	public string Name => DatasetNames.ImdbRatings;

	public async Task<Result<Optional<DatasetFile<ImdbRatingLine>>>> DownloadAsync(Optional<string> etag, CancellationToken cancellationToken = default)
	{
		try
		{
			return await FetchAsync(etag, cancellationToken);
		}
		catch (Exception ex)
			when (ex is HttpRequestException or LaneRefusedException or TimeoutRejectedException or OperationCanceledException { InnerException: TimeoutException })
		{
			return Error.From($"IMDb's datasets could not be reached: {ex.Message}");
		}
	}

	private async Task<Result<Optional<DatasetFile<ImdbRatingLine>>>> FetchAsync(Optional<string> etag, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "title.ratings.tsv.gz");
		etag.Apply(tag => request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(tag)));
		using var response = await http.SendAsync(request, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotModified)
		{
			return Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(new None());
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.ImdbDatasets, $"IMDb's datasets answered {(int)response.StatusCode} for the ratings file.");
		}

		var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
		var file = new DatasetFile<ImdbRatingLine>(Optional.Of(response.Headers.ETag?.Tag), Parse(body, cancellationToken));
		return Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(Some.Of(file));
	}

	private static async IAsyncEnumerable<ImdbRatingLine> Parse(byte[] body, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		using var plain = new MemoryStream();
		await using (var gzip = new GZipStream(new MemoryStream(body), CompressionMode.Decompress))
		{
			await gzip.CopyToAsync(plain, cancellationToken);
		}

		if (body.Length < 4 || (uint)plain.Length != BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(body.Length - 4)))
		{
			throw new InvalidDataException("IMDb's ratings file is cut short.");
		}

		plain.Position = 0;
		using var reader = new StreamReader(plain);
		if (await reader.ReadLineAsync(cancellationToken) != Header)
		{
			throw new InvalidDataException("IMDb's ratings file does not start with its header.");
		}

		while (await reader.ReadLineAsync(cancellationToken) is { } line)
		{
			yield return Line(line);
		}
	}

	private static ImdbRatingLine Line(string line)
	{
		var fields = line.Split('\t');
		if (
			fields.Length != 3
			|| !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var average)
			|| !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var votes)
		)
		{
			throw new InvalidDataException($"IMDb's ratings file holds a line it cannot read: '{line}'.");
		}

		return new ImdbRatingLine(fields[0], average, votes);
	}
}
