using System.Text.RegularExpressions;

namespace Web.Tests.Theme;

public class HoverButtonPaletteTests
{
	private static readonly string ThemeCss = ReadThemeCss();

	[Test]
	[Arguments(".hover-button--destructive", "oklch(0.577 0.15925 27.325)", "oklch(1 0 0)")]
	[Arguments(".hover-button--success", "oklch(0.55 0.13 142)", "white")]
	public async Task Palette_WhenPresetInLightMode_FlipsTextFromTintToForeground(string selector, string restingText, string hoverText)
	{
		var palette = Body(ThemeCss, ":root");
		var rules = Body(ThemeCss, selector);

		await Assert.That(Resolve(Declared(rules, "--tint"), palette)).IsEqualTo(restingText);
		await Assert.That(Resolve(Declared(rules, "--tint-foreground"), palette)).IsEqualTo(hoverText);
	}

	[Test]
	[Arguments(".hover-button--destructive", "oklch(0.63 0.1625 28.552)", "oklch(0.985 0 0)")]
	[Arguments(".hover-button--success", "oklch(0.60 0.117 142)", "white")]
	public async Task Palette_WhenPresetInDarkMode_FlipsTextFromTintToForeground(string selector, string restingText, string hoverText)
	{
		var palette = Body(ThemeCss, ".dark");
		var rules = Body(ThemeCss, selector);

		await Assert.That(Resolve(Declared(rules, "--tint"), palette)).IsEqualTo(restingText);
		await Assert.That(Resolve(Declared(rules, "--tint-foreground"), palette)).IsEqualTo(hoverText);
	}

	[Test]
	public async Task Palette_WhenAtRest_IsTransparentWithTintedTextAndBorder()
	{
		var rest = Normalize(Body(ThemeCss, ".hover-button"));

		await Assert.That(rest).Contains("background-color: transparent");
		await Assert.That(rest).Contains("color: var(--tint)");
		await Assert.That(rest).Contains("border: 1px solid var(--tint)");
	}

	[Test]
	public async Task Palette_WhenHovered_FillsTheBackgroundAndFlipsTextToForeground()
	{
		var hover = Normalize(Body(ThemeCss, ".hover-button:hover"));

		await Assert.That(hover).Contains("background-color: var(--tint)");
		await Assert.That(hover).Contains("color: var(--tint-foreground, white)");
	}

	[Test]
	public async Task Palette_WhenCustom_DerivesAContrastingForegroundFromTheTint()
	{
		var custom = Normalize(Body(ThemeCss, ".hover-button--custom"));

		await Assert.That(custom).Contains("color: var(--tint-foreground, oklch(from var(--tint)");
	}

	[Test]
	public async Task Palette_WhenBorderless_DropsTheBorder()
	{
		var borderless = Normalize(Body(ThemeCss, ".hover-button--borderless"));

		await Assert.That(borderless).Contains("border: none");
	}

	private static string Body(string css, string selector)
	{
		var open = css.IndexOf('{', css.IndexOf(selector, StringComparison.Ordinal));
		var close = css.IndexOf('}', open);
		return css[(open + 1)..close];
	}

	private static string Declared(string body, string property) => Regex.Match(body, Regex.Escape(property) + @"\s*:\s*([^;]+);").Groups[1].Value.Trim();

	private static string Resolve(string value, string palette)
	{
		var reference = Regex.Match(value, @"var\(\s*(--[\w-]+)\s*\)");
		return reference.Success ? Declared(palette, reference.Groups[1].Value) : value;
	}

	private static string Normalize(string css) => Regex.Replace(css, @"\s+", " ").Trim();

	private static string ReadThemeCss()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(directory.FullName, "Sammlarr.slnx")))
		{
			directory = directory.Parent!;
		}
		return File.ReadAllText(Path.Combine(directory.FullName, "Web", "wwwroot", "styles", "themes", "default.css"));
	}
}
