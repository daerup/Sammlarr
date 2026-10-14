using TestInfrastructure;
using UI.Features.Settings.Components.About;

namespace UI.Tests.Settings.About;

public class ThirdPartyNoticesTests
{
	private readonly DirectoryInfo _published = Directory.CreateTempSubdirectory("sammlarr-notices-");

	private ThirdPartyNotices Testee => field ??= new ThirdPartyNotices(_published.FullName);

	[After(Test)]
	public void RemovePublished() => _published.Delete(recursive: true);

	[Test]
	public async Task GetAsync_AfterTheFileIsGone_StillAnswersTheNoticesItReadFirst()
	{
		var file = Path.Combine(_published.FullName, "third-party-notices.json");
		await File.WriteAllTextAsync(file, """{ "licences": [ { "id": "MIT", "text": "MIT License", "packages": [] } ], "assets": [] }""");
		await Testee.GetAsync();
		File.Delete(file);

		await Assert.That(await Testee.GetAsync()).IsSome();
	}
}
