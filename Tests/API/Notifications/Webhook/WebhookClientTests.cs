using System.Net;
using API.Notifications.Webhook;
using Contracts.Identity;
using Domain.Identity;
using TUnit.Mocks.Http;

namespace API.Tests.Notifications.Webhook;

public class WebhookClientTests
{
	[Test]
	[Arguments(UserRole.Member, "WebhookMember")]
	[Arguments(UserRole.Admin, "WebhookAdmin")]
	public async Task SendAsync_ForTheOwnersRole_SendsThroughThatRolesClient(UserRole role, string client)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.OK);
		var clients = IHttpClientFactory.Mock();
		clients.CreateClient(Any()).Returns(handler.CreateClient("https://example.test/"));
		var owner = ICurrentUser.Mock();
		owner.Role.Returns(role);

		await new WebhookClient(clients.Object, owner.Object).SendAsync(
			new HttpRequestMessage(HttpMethod.Post, "https://example.test/hook"),
			CancellationToken.None
		);

		clients.CreateClient(client).WasCalled(Times.Once);
	}
}
