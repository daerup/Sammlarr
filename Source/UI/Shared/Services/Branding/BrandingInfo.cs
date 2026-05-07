using Microsoft.AspNetCore.Components;

namespace UI.Shared.Services.Branding;

public record BrandingInfo(string DisplayName, string NickName, string BrandColor, Func<string?, RenderFragment> IconRenderer)
{
	public RenderFragment Icon => IconRenderer(null);

	public RenderFragment IconWithClass(string cssClass) => IconRenderer(cssClass);
}
