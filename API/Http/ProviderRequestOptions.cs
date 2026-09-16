using Contracts.Identity;
using Domain.Identity;

namespace API.Http;

internal static class ProviderRequestOptions
{
	private static readonly HttpRequestOptionsKey<UserId> triggeredBy = new("sammlarr.triggered-by");

	public static void StampTriggeredBy(this HttpRequestMessage request, ICurrentUser currentUser)
	{
		if (currentUser.IsAuthenticated)
		{
			request.Options.Set(triggeredBy, currentUser.Id);
		}
	}

	public static UserId? TriggeredBy(this HttpResponseMessage? response) =>
		response?.RequestMessage is { } request && request.Options.TryGetValue(triggeredBy, out var userId) ? userId : null;
}
