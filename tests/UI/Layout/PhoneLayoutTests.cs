using Bunit;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class PhoneLayoutTests : BunitContext
{
	[Before(Test)]
	public void Setup() => JSInterop.Mode = JSRuntimeMode.Loose;

	[Test]
	public async Task IsPhone_FollowsTheViewportReport()
	{
		var cut = Render<PhoneLayout>(parameters => parameters.AddChildContent<PhoneOnly>(phone => phone.AddChildContent("<span class=\"phone-piece\"></span>")));

		await Assert.That(cut.FindAll(".phone-piece")).IsEmpty();

		await cut.InvokeAsync(() => cut.Instance.Changed(true));
		await Assert.That(cut.FindAll(".phone-piece")).HasSingleItem();

		await cut.InvokeAsync(() => cut.Instance.Changed(false));
		await Assert.That(cut.FindAll(".phone-piece")).IsEmpty();
	}
}
