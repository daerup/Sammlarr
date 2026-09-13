using System.Text.Json;

namespace E2E.Tests;

internal static class MdblistFeed
{
	private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	internal static async Task<IReadOnlyList<string>> ServedTitlesAsync(string identifier)
	{
		var body = await E2EApp.ServedBodyAsync($"/lists/{identifier}/items");
		var page =
			JsonSerializer.Deserialize<ListPage>(body, Options)
			?? throw new InvalidOperationException($"The served MDBList body for '{identifier}' is not a list page.");

		return [.. (page.Movies ?? []).Concat(page.Shows ?? []).Select(item => item.Title)];
	}

	private sealed record ListPage(ListItem[]? Movies, ListItem[]? Shows);

	private sealed record ListItem(string Title);
}
