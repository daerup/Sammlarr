namespace Shared.Tests.Unit;

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
	public async Task Decode_ReturnsNull_WhenNull()
	{
		var result = UrlGuid.Decode(null);

		await Assert.That(result).IsNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("tooshort")]
	[Arguments("waytoolongtobeavalidshortid123456")]
	public async Task Decode_ReturnsNull_WhenWrongLength(string input)
	{
		var result = UrlGuid.Decode(input);

		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task Decode_ReturnsNull_WhenInvalidBase64()
	{
		var result = UrlGuid.Decode("!!!!!!!!!!!!!!!!!!!!!!"); // 22 chars but invalid

		await Assert.That(result).IsNull();
	}

	[Test]
	public async Task Encode_IsStable_ForSameGuid()
	{
		var id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

		var first = UrlGuid.Encode(id);
		var second = UrlGuid.Encode(id);

		await Assert.That(first).IsEqualTo(second);
	}
}
