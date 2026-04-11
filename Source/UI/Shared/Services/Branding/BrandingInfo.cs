using Microsoft.AspNetCore.Components;

namespace UI.Shared.Services.Branding;

public record BrandingInfo(string DisplayName, string SimpleIconName, string BrandColor, RenderFragment Icon);
