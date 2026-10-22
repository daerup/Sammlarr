using Microsoft.Extensions.Configuration;
using Shared.Options;
using TestInfrastructure;

namespace UI.Tests.Feeds;

internal static class FeedFilterStub
{
	internal static readonly FeedFilterOptions Shipped = Load();

	private static FeedFilterOptions Load()
	{
		var shipped = new ConfigurationBuilder().AddJsonFile(RepositoryRoot.Resolve("src", "Web", "appsettings.json")).Build();
		var options = new FeedFilterOptions();
		shipped.GetSection(FeedFilterOptions.SectionName).Bind(options);
		return options;
	}
}
