using Microsoft.AspNetCore.Components;

namespace UI.Shared.Services.Branding;

public record BrandingInfo(string DisplayName, string NickName, string BrandColor, RenderFragment Icon);
