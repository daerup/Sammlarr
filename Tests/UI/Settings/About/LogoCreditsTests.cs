using TestInfrastructure;
using UI.Features.Settings.Components.About;

namespace UI.Tests.Settings.About;

public class LogoCreditsTests
{
	private static readonly string ProvidersFolder = RepositoryRoot.Resolve("UI", "wwwroot", "providers");

	[Test]
	public async Task All_NameEveryFileUnderProviders()
	{
		var vendored = Directory
			.EnumerateFiles(ProvidersFolder, "*", SearchOption.AllDirectories)
			.Select(file => Path.GetRelativePath(ProvidersFolder, file).Replace('\\', '/'))
			.Where(file => file != "credits.json");

		await Assert.That(vendored.Except(LogoCredits.All.Select(credit => credit.File))).IsEmpty();
	}

	[Test]
	public async Task All_NameOnlyFilesThatExist()
	{
		await Assert.That(LogoCredits.All.Where(credit => !File.Exists(Path.Combine(ProvidersFolder, credit.File)))).IsEmpty();
	}
}
