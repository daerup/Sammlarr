using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed record SignInCredential(AuthProvider Provider, Guid ClientId, Secret AccessToken, DateTimeOffset ExpiresAt) : ICredential;
