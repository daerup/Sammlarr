using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class CredentialActionsTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(CredentialState.Idle, false, "Test|Reconnect")]
	[Arguments(CredentialState.Idle, true, "Test|Reconnect|Cancel")]
	[Arguments(CredentialState.Testing, true, "Testing…|Reconnect")]
	[Arguments(CredentialState.Success, true, "Save|Reconnect|Cancel")]
	[Arguments(CredentialState.Saving, true, "Saving…|Reconnect")]
	[Arguments(CredentialState.Failed, true, "Test again|Reconnect|Cancel")]
	[Arguments(CredentialState.Saved, false, "Reconnect|Remove")]
	public async Task Actions_InEachState_OfferTheNextStepThenTheExtrasThenCancelOrRemove(CredentialState state, bool hasChange, string buttons)
	{
		var cut = RenderActions(state, hasChange);

		await Assert.That(string.Join('|', cut.FindAll("button").Select(button => button.TextContent.Trim()))).IsEqualTo(buttons);
	}

	[Test]
	public async Task Test_WithNothingTyped_StaysDisabled()
	{
		var cut = RenderActions(CredentialState.Idle, hasChange: false);

		await Assert.That(cut.FindAll("button").First().HasAttribute("disabled")).IsTrue();
	}

	private IRenderedComponent<CredentialActions> RenderActions(CredentialState state, bool hasChange) =>
		Render<CredentialActions>(parameters =>
			parameters
				.Add(actions => actions.State, state)
				.Add(actions => actions.HasChange, hasChange)
				.AddChildContent("<button>Reconnect</button>")
				.Add(actions => actions.Remove, "<button>Remove</button>")
		);
}
