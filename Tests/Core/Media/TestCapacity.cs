using Contracts.Providers;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

internal static class TestCapacity
{
	internal static IProviderCapacity Open() => With(_ => new ProviderLoad(new None(), 0));

	internal static IProviderCapacity With(Func<MediaProvider, ProviderLoad> load)
	{
		var capacity = IProviderCapacity.Mock();
		capacity.Of(Any()).Returns(provider => load(provider));
		return capacity.Object;
	}
}
