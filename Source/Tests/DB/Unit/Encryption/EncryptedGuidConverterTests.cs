using DB.Encryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Tests.Unit.Encryption;

public class EncryptedGuidConverterTests
{
	private IDataProtectionService CreateDataProtectionService()
	{
		var services = new ServiceCollection();
		services.AddDataProtection();
		var provider = services.BuildServiceProvider();
		return new DataProtectionService(provider.GetRequiredService<IDataProtectionProvider>());
	}

	[Test]
	public async Task Convert_WithValidGuid_ReturnsEncryptedString()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedGuidConverter(service);
		var guidValue = Guid.NewGuid();

		var encrypted = testee.ConvertToProvider(guidValue);

		await Assert.That(encrypted).IsNotNull();
		await Assert.That(encrypted).IsNotEqualTo(guidValue.ToString());
	}

	[Test]
	public async Task ConvertBack_WithEncryptedString_ReturnsOriginalGuid()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedGuidConverter(service);
		var originalGuid = Guid.NewGuid();
		var encrypted = testee.ConvertToProvider(originalGuid);

		var decrypted = testee.ConvertFromProvider(encrypted);

		await Assert.That(decrypted).IsEqualTo(originalGuid);
	}

	[Test]
	public async Task Encrypt_ThenDecrypt_WithVariousGuids_RoundTripSucceeds()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedGuidConverter(service);
		var testGuids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid() };

		using var _ = Assert.Multiple();
		foreach (var guid in testGuids)
		{
			var encrypted = testee.ConvertToProvider(guid);
			var decrypted = testee.ConvertFromProvider(encrypted);
			await Assert.That(decrypted).IsEqualTo(guid);
		}
	}

	[Test]
	public async Task EncryptedValue_IsBase64Encoded()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedGuidConverter(service);
		var guidValue = Guid.NewGuid();

		var encrypted = testee.ConvertToProvider(guidValue);
		var encryptedString = (encrypted as string) ?? string.Empty;

		var isBase64 = IsBase64String(encryptedString);
		await Assert.That(isBase64).IsTrue();
	}

	private static bool IsBase64String(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return true;
		}

		Span<byte> buffer = new Span<byte>(new byte[value.Length]);
		return Convert.TryFromBase64String(value, buffer, out _);
	}

	[Test]
	public async Task Encrypt_WithSameGuid_ProducesDifferentCiphertexts()
	{
		var service1 = CreateDataProtectionService();
		var service2 = CreateDataProtectionService();
		var testee1 = new EncryptedGuidConverter(service1);
		var testee2 = new EncryptedGuidConverter(service2);
		var guidValue = Guid.NewGuid();

		var encrypted1 = testee1.ConvertToProvider(guidValue);
		var encrypted2 = testee2.ConvertToProvider(guidValue);

		await Assert.That(encrypted1).IsNotEqualTo(encrypted2);
	}

	[Test]
	public async Task ConvertBack_WithInvalidBase64_ThrowsInvalidOperationException()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedGuidConverter(service);

		await Assert.That(() => testee.ConvertFromProvider("not-valid-base64!!!")).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ConvertBack_WithInvalidGuidString_ThrowsFormatException()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedGuidConverter(service);
		var invalidGuidString = "not-a-valid-guid";
		var encrypted = service.Encrypt(invalidGuidString);

		await Assert.That(() => testee.ConvertFromProvider(encrypted)).Throws<FormatException>();
	}
}
