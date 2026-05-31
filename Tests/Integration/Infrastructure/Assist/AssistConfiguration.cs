using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Assist;

[Binding]
public static class AssistConfiguration
{
	[BeforeTestRun]
	public static void RegisterAssistServices()
	{
		Service.Instance.ValueComparers.Register(new EnumValueComparer());
		Service.Instance.ValueRetrievers.Register(new ExternalIdsValueRetriever());
		Service.Instance.ValueRetrievers.Register(new StringListValueRetriever());
	}
}
