using System.Text.RegularExpressions;
using TestInfrastructure;

namespace Web.Tests.Theme;

public class HoverButtonPaletteTests
{
	private static readonly string ThemeCss = ReadThemeCss();

	[Test]
	[Arguments(".hover-button--destructive", "var(--destructive)", "var(--destructive-foreground)")]
	[Arguments(".hover-button--success", "var(--alert-success)", "white")]
	public async Task Palette_ForEachPreset_TintsWithItsThemeTokens(string selector, string tint, string foreground)
	{
		var rules = Body(ThemeCss, selector);

		await Assert.That(Declared(rules, "--tint")).IsEqualTo(tint);
		await Assert.That(Declared(rules, "--tint-foreground")).IsEqualTo(foreground);
	}

	[Test]
	public async Task Palette_WhenAtRest_DrawsTheBorderInTheTint()
	{
		var rest = Normalize(Body(ThemeCss, ".hover-button"));

		await Assert.That(rest).Contains("border: 1px solid var(--tint)");
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

	private static string Normalize(string css) => Regex.Replace(css, @"\s+", " ").Trim();

	private static string ReadThemeCss() => File.ReadAllText(RepositoryRoot.Resolve("src", "Web", "wwwroot", "styles", "themes", "default.css"));
}
