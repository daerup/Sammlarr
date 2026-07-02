using Reqnroll;

namespace Integration.Tests.Infrastructure.Common;

[Binding]
public class ExceptionSteps(ExceptionContext ctx)
{
	[Then("an exception of type {string} occurred")]
	public async Task ThenExceptionOfType(string typeName) => await Assert.That(ctx.Exception?.GetType().Name).IsEqualTo(typeName);

	[Then("an exception occurred with message containing {string}")]
	public async Task ThenExceptionMessageContains(string text) => await Assert.That(ctx.Exception?.Message).Contains(text);
}
