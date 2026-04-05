using DB.Encryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Tests.Unit.Encryption;

public class EncryptedStringConverterTests
{
	private IDataProtectionService CreateDataProtectionService()
	{
		var services = new ServiceCollection();
		services.AddDataProtection();
		var provider = services.BuildServiceProvider();
		return new DataProtectionService(provider.GetRequiredService<IDataProtectionProvider>());
	}

	[Test]
	public async Task ConvertToProvider_WithValidString_ReturnsBase64EncodedCiphertext()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedStringConverter(service);
		var convertToProvider = testee.ConvertToProviderExpression.Compile();
		var plaintext = "super-secret-token";

		var encrypted = convertToProvider(plaintext);

		await Assert.That(encrypted).IsNotEmpty();
		await Assert.That(encrypted).IsNotEqualTo(plaintext);
	}

	[Test]
	public async Task ConvertToProvider_WithEmptyString_ReturnsEmptyString()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedStringConverter(service);
		var convertToProvider = testee.ConvertToProviderExpression.Compile();

		var encrypted = convertToProvider("");

		await Assert.That(encrypted).IsEmpty();
	}

	[Test]
	public async Task ConvertFromProvider_WithValidCiphertext_ReturnsOriginalPlaintext()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedStringConverter(service);
		var convertToProvider = testee.ConvertToProviderExpression.Compile();
		var convertFromProvider = testee.ConvertFromProviderExpression.Compile();
		var plaintext = "api-key-12345";
		var encrypted = convertToProvider(plaintext);

		var decrypted = convertFromProvider(encrypted);

		await Assert.That(decrypted).IsEqualTo(plaintext);
	}

	[Test]
	public async Task ConvertToProvider_ThenConvertFromProvider_WithVariousValues_RoundTripSucceeds()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedStringConverter(service);
		var convertToProvider = testee.ConvertToProviderExpression.Compile();
		var convertFromProvider = testee.ConvertFromProviderExpression.Compile();
		string[] testValues = ["simple-text", "text-with-spaces ", "special!@#$%^&*()", "unicode-café-naïve", "very-long-" + new string('x', 1000)];

		using var _ = Assert.Multiple();
		foreach (var value in testValues)
		{
			var encrypted = convertToProvider(value);
			var decrypted = convertFromProvider(encrypted);
			await Assert.That(decrypted).IsEqualTo(value);
		}
	}

	[Test]
	public async Task ConvertFromProvider_WithInvalidBase64_ThrowsInvalidOperationException()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedStringConverter(service);
		var convertFromProvider = testee.ConvertFromProviderExpression.Compile();

		await Assert.That(() => convertFromProvider("not-valid-base64!!!")).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ConvertToProvider_WithSamePlaintextOnDifferentServices_ProducesDifferentCiphertexts()
	{
		var service1 = CreateDataProtectionService();
		var service2 = CreateDataProtectionService();
		var testee1 = new EncryptedStringConverter(service1);
		var testee2 = new EncryptedStringConverter(service2);
		var convertToProvider1 = testee1.ConvertToProviderExpression.Compile();
		var convertToProvider2 = testee2.ConvertToProviderExpression.Compile();
		var plaintext = "same-value";

		var encrypted1 = convertToProvider1(plaintext);
		var encrypted2 = convertToProvider2(plaintext);

		await Assert.That(encrypted1).IsNotEqualTo(encrypted2);
	}

	[Test]
	public async Task ConvertFromProvider_WithEmptyString_ReturnsEmptyString()
	{
		var service = CreateDataProtectionService();
		var testee = new EncryptedStringConverter(service);
		var convertFromProvider = testee.ConvertFromProviderExpression.Compile();

		var decrypted = convertFromProvider("");

		await Assert.That(decrypted).IsEmpty();
	}
}
