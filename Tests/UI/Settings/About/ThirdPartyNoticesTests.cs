using TestInfrastructure;
using TUnit.Assertions.Enums;
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
		File.Copy(Path.Combine(AppContext.BaseDirectory, "Settings", "About", "third-party-notices.json"), file);
		await Testee.GetAsync();
		File.Delete(file);

		var notices = await Assert.That(await Testee.GetAsync()).IsSome();

		await Assert.That(notices!.Licences.Select(licence => licence.Id)).IsEquivalentTo(["Apache-2.0", "MIT", "MIT OR Apache-2.0"], CollectionOrdering.Matching);
	}
}
