namespace UI.Tests;

internal static class RenderedText
{
	extension(string text)
	{
		internal string Words() => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

		internal string Squeezed() => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
	}
}
