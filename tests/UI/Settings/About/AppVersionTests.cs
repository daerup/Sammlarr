using UI.Shared.Services;

namespace UI.Tests.Settings.About;

public class AppVersionTests
{
	[Test]
	public async Task From_BeforeAnyReleaseTag_IsADevBuildCountingTheCommitsSoFar()
	{
		var version = AppVersion.From("0.0.0-alpha.0.584+efe42c7679e09cf9b537ab5c2aeb57f0bfc2dca1");

		await Assert.That(version.Number).IsEqualTo("0.0.0-alpha.0.584");
		await Assert.That(version.IsDevBuild).IsTrue();
		await Assert.That(version.HasRelease).IsFalse();
		await Assert.That(version.CommitsSinceRelease).IsEqualTo(584);
	}

	[Test]
	public async Task From_AfterAReleaseTag_IsADevBuildCountingTheCommitsSinceIt()
	{
		var version = AppVersion.From("1.4.1-alpha.0.3+a6fa90de4af3543e9cc683cf220c4a285a136767");

		await Assert.That(version.IsDevBuild).IsTrue();
		await Assert.That(version.HasRelease).IsTrue();
		await Assert.That(version.CommitsSinceRelease).IsEqualTo(3);
	}

	[Test]
	public async Task From_ForATaggedRelease_IsNotADevBuild()
	{
		var version = AppVersion.From("1.4.0+a6fa90de4af3543e9cc683cf220c4a285a136767");

		await Assert.That(version.Number).IsEqualTo("1.4.0");
		await Assert.That(version.IsDevBuild).IsFalse();
		await Assert.That(version.HasRelease).IsTrue();
		await Assert.That(version.CommitsSinceRelease).IsEqualTo(0);
	}

	[Test]
	public async Task From_WithoutGitHistory_IsNeitherADevBuildNorARelease()
	{
		var version = AppVersion.From("0.0.0-alpha.0");

		await Assert.That(version.IsDevBuild).IsFalse();
		await Assert.That(version.HasRelease).IsFalse();
	}

	[Test]
	[Arguments("2.0.0-rc.1+a6fa90de")]
	[Arguments("2.0.0-beta+a6fa90de")]
	public async Task From_ForAPreReleaseTag_ReadsAsTheReleaseItNames(string informationalVersion)
	{
		var version = AppVersion.From(informationalVersion);

		await Assert.That(version.IsDevBuild).IsFalse();
		await Assert.That(version.CommitsSinceRelease).IsEqualTo(0);
	}
}
