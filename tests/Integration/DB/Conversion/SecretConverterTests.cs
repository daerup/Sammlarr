using DB.Conversion.Encryption;
using DB.Data.Auth;
using Harness;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Integration.Tests.DB.Conversion;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SecretConverterTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	private const string Value = "canary-secret";

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private SecretConverter Testee =>
		(SecretConverter)fixture.Context.Model.FindEntityType(typeof(ApiKeyConfigRow))!.FindProperty(nameof(ApiKeyConfigRow.ApiKey))!.GetValueConverter()!;

	private IEnumerable<IProperty> SecretProperties =>
		from entity in fixture.Context.Model.GetEntityTypes()
		from property in entity.GetProperties()
		where property.ClrType == typeof(Secret)
		select property;

	[Test]
	public async Task ConvertToProvider_Always_StoresNoPlainValue()
	{
		await Assert.That(Testee.ConvertToProviderTyped(new Secret(Value))).DoesNotContain(Value);
	}

	[Test]
	public async Task ConvertFromProvider_OfAStoredValue_ReadsTheSecretBack()
	{
		var stored = Testee.ConvertToProviderTyped(new Secret(Value));

		await Assert.That(Testee.ConvertFromProviderTyped(stored).Reveal()).IsEqualTo(Value);
	}

	[Test]
	public async Task Model_EverySecretProperty_IsStoredThroughTheConverter()
	{
		await Assert.That(SecretProperties.Select(property => property.Name)).Contains(nameof(ApiKeyConfigRow.ApiKey));
		await Assert.That(SecretProperties.Where(property => property.GetValueConverter() is not SecretConverter)).IsEmpty();
	}
}
