using Microsoft.Extensions.Configuration;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistOptionsTests
{
	[Test]
	public async Task FeedReserve_ShippedInTheAppSettings_IsTheCodeDefault()
	{
		var shipped = new ConfigurationBuilder().AddJsonFile(RepositoryRoot.Resolve("src", "Web", "appsettings.json")).Build();

		var reserve = shipped.GetSection(MdblistOptions.SectionName).GetValue<int>(nameof(MdblistOptions.FeedReserve));

		await Assert.That(reserve).IsEqualTo(new MdblistOptions { BaseUrl = string.Empty }.FeedReserve);
	}
}
