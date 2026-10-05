namespace E2E.Tests.Staging;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class StagingAttribute()
	: SkipAttribute("Set E2E_STAGING=true to boot the staging profile: it builds the Sammlarr image and needs the internet.")
{
	public override Task<bool> ShouldSkip(TestRegisteredContext context)
	{
		return Task.FromResult(Environment.GetEnvironmentVariable("E2E_STAGING") is not "true");
	}
}
