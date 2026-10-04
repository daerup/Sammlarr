using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Text;
using API.Http;
using API.Pictures;
using Contracts.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.DI;
using Shared.Options;
using SkiaSharp;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Pictures;

public class PictureGridsTests
{
	private const string Avatar = "https://plex.tv/users/guest/avatar";

	private const int Side = 48;

	private static readonly SKColor Teal = new(0x1f, 0x8a, 0x8a);

	private static readonly SKColor Orange = new(0xe8, 0x89, 0x2e);

	private readonly MockHttpHandler _plex = Mock.HttpHandler();

	[Test]
	[Arguments(SKEncodedImageFormat.Png)]
	[Arguments(SKEncodedImageFormat.Jpeg)]
	public async Task ReadAsync_ForAPngOrAJpeg_GivesItsPixelsOnTheGrid(SKEncodedImageFormat format)
	{
		_plex.OnGet("/users/guest/avatar").RespondWith(Picture(Drawn(format)));

		await Assert.That((await Testee.ReadAsync(Avatar)).Map(Corners)).IsSome().And.IsEqualTo("teal orange");
	}

	[Test]
	public async Task ReadAsync_ForAPalettePng_GivesItsPixelsOnTheGrid()
	{
		_plex.OnGet("/users/guest/avatar").RespondWith(Picture(PalettePng()));

		await Assert.That((await Testee.ReadAsync(Avatar)).Map(Corners)).IsSome().And.IsEqualTo("teal orange");
	}

	[Test]
	public async Task ReadAsync_WhenThePictureIsMissing_GivesNothing()
	{
		_plex.OnGet("/users/guest/avatar").Respond(HttpStatusCode.NotFound);

		await Assert.That(await Testee.ReadAsync(Avatar)).IsNone();
	}

	[Test]
	public async Task ReadAsync_ForBytesThatAreNoPicture_GivesNothing()
	{
		_plex.OnGet("/users/guest/avatar").RespondWith(Picture(Encoding.UTF8.GetBytes("<html><body>Not a picture</body></html>")));

		await Assert.That(await Testee.ReadAsync(Avatar)).IsNone();
	}

	[Test]
	public async Task ReadAsync_ForAPictureBiggerThanAnyAvatar_GivesNothing()
	{
		_plex.OnGet("/users/guest/avatar").RespondWith(Picture(PalettePng(4096)));

		await Assert.That(await Testee.ReadAsync(Avatar)).IsNone();
	}

	[Test]
	public async Task ReadAsync_WhenTheHostCannotBeReached_GivesNothing()
	{
		_plex.OnAnyRequest().Throws(new HttpRequestException("No such host is known."));

		await Assert.That(await Testee.ReadAsync(Avatar)).IsNone();
	}

	[Test]
	public async Task ReadAsync_WhenThePictureTakesLongerThanTheClientWaits_GivesNothing()
	{
		var testee = new PictureGrids(new HttpClient(new Stalled()) { Timeout = TimeSpan.FromMilliseconds(100) }, NullLogger<PictureGrids>.Instance);

		await Assert.That(await testee.ReadAsync(Avatar)).IsNone();
	}

	[Test]
	[Arguments("")]
	[Arguments("e2e-redacted")]
	[Arguments("ftp://plex.tv/users/guest/avatar")]
	public async Task ReadAsync_ForSomethingThatIsNoWebAddress_GivesNothingWithoutAsking(string url)
	{
		await Assert.That(await Testee.ReadAsync(url)).IsNone();
		_plex.Verify(request => request.Method(HttpMethod.Get), Times.Never);
	}

	[Test]
	public async Task ReadAsync_WhenTheCallerCancels_StopsWithTheCaller()
	{
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();
		var testee = new PictureGrids(new HttpClient(new Stalled()), NullLogger<PictureGrids>.Instance);

		await Assert.That(async () => await testee.ReadAsync(Avatar, cancelled.Token)).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task ReadAsync_ThroughItsRegisteredClient_FollowsTheRedirectsToThePicture()
	{
		var grid = await ReadThroughItsRegisteredClientAsync("/users/guest/avatar");

		await Assert.That(grid.Map(Corners)).IsSome().And.IsEqualTo("teal orange");
	}

	[Test]
	public async Task ReadAsync_ThroughItsRegisteredClient_RefusesAPictureOfMoreThanFourMegabytes()
	{
		await Assert.That(await ReadThroughItsRegisteredClientAsync("/noise.png")).IsNone();
	}

	private static async Task<Optional<byte[]>> ReadThroughItsRegisteredClientAsync(string path)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		await using var plex = builder.Build();
		plex.MapGet("/users/guest/avatar", () => Results.Redirect("/avatar/guest"));
		plex.MapGet("/avatar/guest", () => Results.Redirect("/guest.png"));
		plex.MapGet("/guest.png", () => Results.Bytes(Drawn(SKEncodedImageFormat.Png), "image/png"));
		plex.MapGet("/noise.png", () => Results.Bytes(Noise(), "image/png"));
		await plex.StartAsync();
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(new PictureOptions());
				services.AddSingleton<ProviderConcurrencyGates>();
				services.AddModule<PicturesModule>();
			})
			.Build();

		return await host.Services.GetRequiredService<IPictureGrids>().ReadAsync($"{plex.Urls.Single()}{path}");
	}

	private static byte[] Noise()
	{
		var info = new SKImageInfo(1100, 1100, SKColorType.Rgba8888, SKAlphaType.Unpremul);
		var pixels = new byte[info.BytesSize];
		new Random(7).NextBytes(pixels);
		using var image = SKImage.FromPixelCopy(info, pixels);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private PictureGrids Testee => new(new HttpClient(_plex), NullLogger<PictureGrids>.Instance);

	private static string Corners(byte[] rgba) => $"{Name(rgba, 0)} {Name(rgba, Side - 1)}";

	private static string Name(byte[] rgba, int x)
	{
		var colour = new SKColor(rgba[x * 4], rgba[x * 4 + 1], rgba[x * 4 + 2]);
		return colour switch
		{
			_ when Near(colour, Teal) => "teal",
			_ when Near(colour, Orange) => "orange",
			_ => $"{colour}",
		};
	}

	private static bool Near(SKColor colour, SKColor target)
	{
		return Math.Abs(colour.Red - target.Red) + Math.Abs(colour.Green - target.Green) + Math.Abs(colour.Blue - target.Blue) <= 12;
	}

	private static HttpResponseMessage Picture(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

	private static byte[] Drawn(SKEncodedImageFormat format)
	{
		using var surface = SKSurface.Create(new SKImageInfo(80, 80));
		surface.Canvas.Clear(Teal);
		using var paint = new SKPaint { Color = Orange };
		surface.Canvas.DrawRect(56, 0, 24, 80, paint);
		using var image = surface.Snapshot();
		using var data = image.Encode(format, 90);
		return data.ToArray();
	}

	private static byte[] PalettePng(int side = Side)
	{
		using var png = new MemoryStream();
		png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
		var header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, side);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), side);
		(header[8], header[9]) = (8, 3);
		Chunk(png, "IHDR", header);
		Chunk(png, "PLTE", [Teal.Red, Teal.Green, Teal.Blue, Orange.Red, Orange.Green, Orange.Blue]);
		using var rows = new MemoryStream();
		for (var y = 0; y < side; y++)
		{
			rows.Write([0, .. Enumerable.Range(0, side).Select(x => (byte)(x < side * 3 / 4 ? 0 : 1))]);
		}

		using var packed = new MemoryStream();
		using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, true))
		{
			zlib.Write(rows.ToArray());
		}

		Chunk(png, "IDAT", packed.ToArray());
		Chunk(png, "IEND", []);
		return png.ToArray();
	}

	private static void Chunk(Stream png, string type, byte[] data)
	{
		var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
		var length = new byte[4];
		BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
		var crc = new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
		png.Write(length);
		png.Write(body);
		png.Write(crc);
	}

	private static uint Crc32(byte[] bytes)
	{
		var crc = 0xFFFFFFFFu;
		foreach (var value in bytes)
		{
			crc ^= value;
			for (var bit = 0; bit < 8; bit++)
			{
				crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
			}
		}

		return crc ^ 0xFFFFFFFFu;
	}

	private sealed class Stalled : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
			return Picture(PalettePng());
		}
	}
}
