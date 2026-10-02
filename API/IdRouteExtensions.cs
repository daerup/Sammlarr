using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

namespace API;

internal static class IdRouteExtensions
{
	extension(IdRoute route)
	{
		public async Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertEachAsync(
			IEnumerable<IExternalMediaId> candidates,
			Func<IExternalMediaId, Task<Result<IReadOnlyList<IExternalMediaId>>>> convert,
			string reason
		)
		{
			Result<IReadOnlyList<IExternalMediaId>> outcome = Error.From(reason);
			foreach (var candidate in candidates.Where(candidate => route.From is Some<MediaProvider>(var source) && candidate.Source == source))
			{
				outcome = await convert(candidate);
				if (outcome is Success<IReadOnlyList<IExternalMediaId>> or Error { Exception: ProviderUnavailableException })
				{
					return outcome;
				}
			}
			return outcome;
		}
	}
}
