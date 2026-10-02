using Domain.Providers;

namespace Contracts.Providers;

public interface IProviderCapacity
{
	ProviderLoad Of(MediaProvider provider);
}
