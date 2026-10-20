using UI.Shared.Common;

namespace UI.Tests.Common;

public class UrlGuidTests
{
	[Test]
	public async Task Encode_ProducesUrlSafe22CharString()
	{
		var id = Guid.NewGuid();

		var result = UrlGuid.Encode(id);

		await Assert.That(result.Length).IsEqualTo(22);
		await Assert.That(result).DoesNotContain("+");
		await Assert.That(result).DoesNotContain("/");
		await Assert.That(result).DoesNotContain("=");
	}

	[Test]
	public async Task Decode_RoundTrips_Encode()
	{
		var id = Guid.NewGuid();

		var result = UrlGuid.Decode(UrlGuid.Encode(id));

		await Assert.That(result).IsEqualTo(id);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("tooshort")]
	[Arguments("waytoolongtobeavalidshortid123456")]
	[Arguments("!!!!!!!!!!!!!!!!!!!!!!")]
	[Arguments("1MOyofblkHirze8SNFZ4+A")]
	public async Task Decode_WhenNotAnEncodedGuid_Throws(string? input)
	{
		await Assert.That(() => UrlGuid.Decode(input)).ThrowsException();
	}

	[Test]
	public async Task Encode_ForAKnownGuid_GivesTheEncodingSharedLinksAlreadyCarry()
	{
		var id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

		await Assert.That(UrlGuid.Encode(id)).IsEqualTo("1MOyofblkHirze8SNFZ4kA");
	}

	[Test]
	public async Task Decode_TheEncodingSharedLinksAlreadyCarry_GivesTheKnownGuid()
	{
		await Assert.That(UrlGuid.Decode("1MOyofblkHirze8SNFZ4kA")).IsEqualTo(Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"));
	}
}
