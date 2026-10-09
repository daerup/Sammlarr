namespace API.Mdblist;

internal sealed class MdblistBudgetHandler(MdblistBudget budget) : DelegatingHandler
{
	private static readonly HttpRequestOptionsKey<string> spentBy = new("sammlarr.mdblist-key");

	public static void Stamp(HttpRequestMessage request, string apiKey) => request.Options.Set(spentBy, apiKey);

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken);
		if (request.Options.TryGetValue(spentBy, out var apiKey))
		{
			budget.Record(apiKey, response.Headers);
		}

		return response;
	}
}
