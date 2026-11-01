using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace UI.Tests;

internal static class RenderedClicks
{
	extension<TComponent>(IRenderedComponent<TComponent> rendered)
		where TComponent : IComponent
	{
		internal Task FindAndClickAsync(Func<IElement> find) => rendered.InvokeAsync(() => find().ClickAsync(new()));
	}
}
