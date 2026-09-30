using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class CardHeadTests : BunitContext
{
	[Test]
	public async Task Head_WithTrailingContent_ShowsTheTitleThenThatContent()
	{
		var cut = Render<CardHead>(parameters =>
			parameters
				.Add(head => head.Icon, "network")
				.Add(head => head.Title, "Which service does what")
				.Add(head => head.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"note\">Managed by an admin</span>")))
		);

		await Assert.That(cut.Find("span.font-semibold").TextContent).IsEqualTo("Which service does what");
		await Assert.That(cut.Find(".note").TextContent).IsEqualTo("Managed by an admin");
	}
}
