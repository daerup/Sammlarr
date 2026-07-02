using System.Globalization;
using Contracts;
using Integration.Tests.Infrastructure.Hosting;
using Reqnroll;
using TUnit.Mocks;

namespace Integration.Tests.Infrastructure.Common;

[Binding]
public class ClockSteps(SammlarrScenarioContext sammlarr)
{
	[Given("current time is {string}")]
	public void GivenCurrentTime(string timestamp) =>
		sammlarr
			.Resolve<Mock<IDateTimeProvider>>()
			.Now.Returns(DateTimeOffset.Parse(timestamp, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.AssumeUniversal));
}
