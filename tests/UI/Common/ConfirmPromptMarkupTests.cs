using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;

namespace UI.Tests.Common;

public class ConfirmPromptMarkupTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task ConfirmPrompt_KeepsItsButtonsInItsLastChild()
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<BbDialogProvider>(1);
			builder.CloseComponent();
		});

		_ = cut.InvokeAsync(() =>
			Services
				.GetRequiredService<DialogService>()
				.ConfirmAsync("Leave without saving?", "Your changes are lost.", new ConfirmDialogOptions { ConfirmText = "Leave", CancelText = "Stay" })
		);

		var prompt = cut.WaitForElement("[role=alertdialog]");
		var footer = prompt.LastElementChild!;
		await Assert.That(footer.TagName).IsEqualTo("DIV");
		await Assert
			.That(footer.QuerySelectorAll(":scope > button").Select(button => button.TextContent.Trim()))
			.IsEquivalentTo(["Stay", "Leave"], CollectionOrdering.Matching);
	}
}
