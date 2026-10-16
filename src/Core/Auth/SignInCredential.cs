using Contracts;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed record SignInCredential(AuthProvider Provider, Guid ClientId, [property: Sensitive] string AccessToken, DateTimeOffset ExpiresAt) : ICredential;
