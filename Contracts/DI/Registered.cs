using Domain.Providers;

namespace Contracts.DI;

public sealed record Registered(Service By, Enum Role, Service Claimed);
