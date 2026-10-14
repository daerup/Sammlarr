using API.Notifications.Webhook;
using Contracts.Identity;
using Contracts.Notifications;
using Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Notifications.Webhook;

public class WebhookClientTests
{
	[Test]
	public async Task SendAsync_ToALoopbackServer_IsBlockedForAMemberAndReachesItForAnAdmin()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		await using var receiver = builder.Build();
		receiver.MapPost("/hook", () => Results.Ok());
		await receiver.StartAsync();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new WebhookOptions());
		services.AddModule<WebhookModule>();
		await using var provider = services.BuildServiceProvider();
		var clients = provider.GetRequiredService<IHttpClientFactory>();
		var hook = $"{receiver.Urls.Single()}/hook";

		var member = await SendAsync(clients, UserRole.Member, hook);
		var admin = await SendAsync(clients, UserRole.Admin, hook);

		var blocked = await Assert.That(member).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(blocked!.Reason).Contains("only an admin's webhook may reach");
		await Assert.That(admin).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
	}

	private static async Task<DeliveryResult> SendAsync(IHttpClientFactory clients, UserRole role, string hook)
	{
		var owner = ICurrentUser.Mock();
		owner.Role.Returns(role);
		using var request = new HttpRequestMessage(HttpMethod.Post, hook);
		return await new WebhookClient(clients, owner.Object).SendAsync(request, CancellationToken.None);
	}
}
