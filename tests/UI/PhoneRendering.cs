using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Layout;

namespace UI.Tests;

internal sealed record PhoneRender<TComponent>(IRenderedComponent<TComponent> Component, IRenderedComponent<BbPortalHost> Portal)
	where TComponent : IComponent;

internal static class PhoneRendering
{
	extension(BunitContext context)
	{
		internal PhoneRender<TComponent> RenderOnPhone<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>> parameters)
			where TComponent : IComponent
		{
			context.RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, true));
			var portal = context.Render<BbPortalHost>();
			return new PhoneRender<TComponent>(context.Render(parameters), portal);
		}
	}
}
