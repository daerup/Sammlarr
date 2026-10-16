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

	public static Optional<UserId> TriggeredBy(this HttpResponseMessage? response) => Optional.Of(response?.RequestMessage).Map(StampedUser);

	public static Optional<UserId> TriggeredBy(this HttpRequestMessage request) => StampedUser(request);

	private static Optional<UserId> StampedUser(HttpRequestMessage request)
	{
		return Optional.When(request.Options.TryGetValue(triggeredBy, out var userId), () => userId);
	}
}
