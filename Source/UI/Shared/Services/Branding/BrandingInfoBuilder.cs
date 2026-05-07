using TimeWarp.Simple.Icons;
using LucideIcon = BlazorBlueprint.Icons.Lucide.Components.LucideIcon;

namespace UI.Shared.Services.Branding;

public static class BrandingInfoBuilder
{
	private const string DefaultIconClass = "w-9 h-9";

	public static BrandingInfo SimpleIcon<TIcon>(string displayName, string nickName, string brandColor)
		where TIcon : SimpleIconBase =>
		new(
			displayName,
			nickName,
			brandColor,
			cssClass =>
				builder =>
				{
					builder.OpenComponent(0, typeof(TIcon));
					builder.AddAttribute(1, "Color", brandColor);
					builder.AddAttribute(2, "fill", brandColor);
					builder.AddAttribute(3, "class", cssClass ?? DefaultIconClass);
					builder.CloseComponent();
				}
		);

	public static BrandingInfo Lucide(string displayName, string nickName, string iconName, string brandColor) =>
		new(
			displayName,
			nickName,
			brandColor,
			cssClass =>
				builder =>
				{
					builder.OpenComponent(0, typeof(LucideIcon));
					builder.AddAttribute(1, "Color", brandColor);
					builder.AddAttribute(2, "Size", 32);
					builder.AddAttribute(3, "class", cssClass ?? DefaultIconClass);
					builder.AddAttribute(4, "Name", iconName);
					builder.CloseComponent();
				}
		);

	public static BrandingInfo Asset(string displayName, string nickName, string brandColor, string svgPath, string webRoot)
	{
		var fullPath = Path.Combine(webRoot, svgPath);
		var svg = File.ReadAllText(fullPath);
		return new BrandingInfo(
			displayName,
			nickName,
			brandColor,
			cssClass =>
				builder =>
				{
					var svgWithAttributes = svg.Replace("<svg ", $"<svg style=\"color: {brandColor}\" class=\"{cssClass ?? DefaultIconClass}\" ");
					builder.AddMarkupContent(0, svgWithAttributes);
				}
		);
	}
}
