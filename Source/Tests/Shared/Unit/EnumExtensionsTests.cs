using System.ComponentModel;

namespace Shared.Tests.Unit;

public class EnumExtensionsTests
{
	[Test]
	public async Task GetDescription_WhenDescriptionAttributeExists_ReturnsDescription()
	{
		var testee = TestEnum.WithDescription;

		var result = testee.GetDescription();

		await Assert.That(result).IsEqualTo("Friendly description");
	}

	[Test]
	public async Task GetDescription_WhenDescriptionAttributeDoesNotExist_ReturnsEnumName()
	{
		var testee = TestEnum.WithoutDescription;

		var result = testee.GetDescription();

		await Assert.That(result).IsEqualTo(nameof(TestEnum.WithoutDescription));
	}

	private enum TestEnum
	{
		[Description("Friendly description")]
		WithDescription,
		WithoutDescription,
	}
}
