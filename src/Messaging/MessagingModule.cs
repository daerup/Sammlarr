using Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Messaging;

public sealed class MessagingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IMessagePublisher, WolverineMessagePublisher>();
	}
}
