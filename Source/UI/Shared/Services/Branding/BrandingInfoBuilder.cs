using TimeWarp.Simple.Icons;

namespace UI.Shared.Services.Branding;

public static class BrandingInfoBuilder
{
	public static BrandingInfo SimpleIcon<TIcon>(string displayName, string simpleIconName, string brandColor)
		where TIcon : SimpleIconBase
	{
		return new BrandingInfo(
			displayName,
			simpleIconName,
			brandColor,
			builder =>
			{
				builder.OpenComponent(0, typeof(TIcon));
				builder.AddAttribute(1, "Color", brandColor);
				builder.AddAttribute(2, "fill", brandColor); // Use brand color for fill
				builder.AddAttribute(3, "class", "w-8 h-8");
				builder.CloseComponent();
			}
		);
	}

	public static BrandingInfo Lucide(string displayName, string iconName, string brandColor)
	{
		return new BrandingInfo(
			displayName,
			iconName,
			brandColor,
			builder =>
			{
				builder.OpenComponent(0, typeof(BlazorBlueprint.Icons.Lucide.Components.LucideIcon));
				builder.AddAttribute(1, "Color", brandColor);
				builder.AddAttribute(2, "Stroke", brandColor);
				builder.AddAttribute(3, "Size", 32);
				builder.AddAttribute(4, "class", "w-8 h-8");
				builder.AddAttribute(5, "fill", "none");
				builder.CloseComponent();
			}
		);
	}

	public static BrandingInfo Asset(string displayName, string simpleIconName, string brandColor, string svgPath, string webRoot)
	{
		var fullPath = Path.Combine(webRoot, svgPath);
		var svg = File.ReadAllText(fullPath);
		return new BrandingInfo(
			displayName,
			simpleIconName,
			brandColor,
			builder =>
			{
				var svgWithColor = svg.Replace("<svg ", $"<svg style=\"color: {brandColor}\" ");
				builder.AddMarkupContent(0, svgWithColor);
			}
		);
	}
}
