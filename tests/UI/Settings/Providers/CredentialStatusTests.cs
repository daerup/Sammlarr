using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class CredentialStatusTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(CredentialState.Idle, "Not set")]
	[Arguments(CredentialState.Testing, "Testing…")]
	[Arguments(CredentialState.Success, "Verified")]
	[Arguments(CredentialState.Saving, "Verified")]
	[Arguments(CredentialState.Saved, "Connected")]
	[Arguments(CredentialState.Failed, "Invalid")]
	public async Task Badge_InEachState_NamesThatState(CredentialState state, string label)
	{
		var cut = Render<CredentialStatus>(parameters => parameters.Add(status => status.State, state).Add(status => status.IdleText, "Not set"));

		await Assert.That(cut.Nodes.OfType<IElement>().Single().TextContent.Trim()).IsEqualTo(label);
	}

	[Test]
	[Arguments(CredentialState.Idle, "Required")]
	[Arguments(CredentialState.Saved, "Connected")]
	public async Task Badge_ForARequiredKey_SaysRequiredOnlyWhileItIsMissing(CredentialState state, string label)
	{
		var cut = Render<CredentialStatus>(parameters =>
			parameters.Add(status => status.State, state).Add(status => status.IdleText, "Not set").Add(status => status.Required, true)
		);

		await Assert.That(cut.Nodes.OfType<IElement>().Single().TextContent.Trim()).IsEqualTo(label);
	}
}
