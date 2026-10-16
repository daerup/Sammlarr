using Domain.Identity;

namespace Harness;

public sealed record SeededUser(Guid Id, string DisplayName, UserRole Role);
