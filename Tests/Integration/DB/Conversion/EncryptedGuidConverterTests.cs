using DB.Conversion.Encryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.DB.Conversion;

public class EncryptedGuidConverterTests
{
	private static IDataProtectionService CreateDataProtectionService()
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
