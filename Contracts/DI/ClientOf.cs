using Domain.Providers;

namespace Contracts.DI;

public sealed record ClientOf(RemoteService Remote, Type Client);
