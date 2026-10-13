using System.Net;
using Web.Health;

namespace Web.Tests.Health;

public class ContainerHealthProbeTests
{
	[Test]
	public async Task CheckAsync_WhenSammlarrAnswersAlive_ExitsZero()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);

		var exit = await ContainerHealthProbe.CheckAsync(handler, "6769");

		await Assert.That(exit).IsEqualTo(0);
		handler.Verify(request => request.Method(HttpMethod.Get).Path("/alive"), Times.Once);
	}

	[Test]
	public async Task CheckAsync_WhenSammlarrAnswersUnhealthy_ExitsOne()
	{
		var exit = await ContainerHealthProbe.CheckAsync(Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.ServiceUnavailable), "6769");

		await Assert.That(exit).IsEqualTo(1);
	}

	[Test]
	public async Task CheckAsync_WhenNothingListens_ExitsOne()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("Connection refused"));

		var exit = await ContainerHealthProbe.CheckAsync(handler, "6769");

		await Assert.That(exit).IsEqualTo(1);
	}

	[Test]
	[Arguments("9090;9091", "http://localhost:9090/alive")]
	[Arguments(null, "http://localhost:6769/alive")]
	public async Task Alive_ForTheConfiguredPorts_AsksTheFirstOne(string? ports, string expected)
	{
		await Assert.That(ContainerHealthProbe.Alive(ports)).IsEqualTo(new Uri(expected));
	}
}
