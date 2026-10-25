using Contracts.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed record PendingLogin(AuthProvider Provider, ExternalLogin Login);
