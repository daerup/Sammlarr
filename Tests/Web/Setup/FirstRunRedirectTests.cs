using Contracts.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Web.Setup;

namespace Web.Tests.Setup;

public class FirstRunRedirectTests
{
	private readonly Mock<IOnboardingService> _onboarding = IOnboardingService.Mock();

	private bool _passedOn;

	private FirstRunRedirect Testee
	{
		get
		{
			return new FirstRunRedirect(_ =>
			{
				_passedOn = true;
				return Task.CompletedTask;
			});
		}
	}

	[Test]
	[Arguments("/")]
	[Arguments("/signin")]
	[Arguments("/settings")]
	public async Task InvokeAsync_ForAPageBeforeAnyoneExists_SendsItToSetup(string path)
	{
		_onboarding.NeedsAdminAsync(Any()).Returns(true);
		var context = Page(path);

		await Testee.InvokeAsync(context);

		await Assert.That(context.Response.Headers.Location.ToString()).IsEqualTo("/setup");
		await Assert.That(_passedOn).IsFalse();
	}

	[Test]
	[Arguments("/setup")]
	[Arguments("/auth/plex/callback")]
	[Arguments("/_blazor")]
	public async Task InvokeAsync_ForSetupItself_LetsItThrough(string path)
	{
		_onboarding.NeedsAdminAsync(Any()).Returns(true);

		await Testee.InvokeAsync(Page(path));

		await Assert.That(_passedOn).IsTrue();
	}

	[Test]
	public async Task InvokeAsync_OnceAnAdminExists_LetsEveryPageThrough()
	{
		_onboarding.NeedsAdminAsync(Any()).Returns(false);

		await Testee.InvokeAsync(Page("/"));

		await Assert.That(_passedOn).IsTrue();
	}

	private DefaultHttpContext Page(string path)
	{
		var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(_onboarding.Object).BuildServiceProvider() };
		context.Request.Method = HttpMethods.Get;
		context.Request.Path = path;
		context.Request.Headers.Accept = "text/html,application/xhtml+xml";
		return context;
	}
}
