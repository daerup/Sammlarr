using API.Http.Lanes;

namespace API.Mdblist;

internal sealed class MdblistBudgetHandler(MdblistBudget budget) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken);
		if (request.Options.TryGetValue(LaneHandler.Owner, out var apiKey))
		{
			budget.Record(apiKey, response);
		}

		return response;
	}
}
