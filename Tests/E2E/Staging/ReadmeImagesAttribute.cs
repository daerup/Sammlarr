namespace E2E.Tests.Staging;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ReadmeImagesAttribute()
	: SkipAttribute(
		"Set README_SCREENSHOTS=true to shoot the README images from the staging session that is already running; restage it first. The GIF needs ffmpeg on PATH."
	)
{
	public override Task<bool> ShouldSkip(TestRegisteredContext context)
	{
		return Task.FromResult(Environment.GetEnvironmentVariable("README_SCREENSHOTS") is not "true");
	}
}
