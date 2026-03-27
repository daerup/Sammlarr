using System;
using System.Threading;
using System.Threading.Tasks;

namespace UI.Data.Stores;

    Uri GetAuthorizationUri(string state);

    string BuildAuthUrl(string code, string? forwardUrl = null);

    Task<PlexTokenResponse> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);
}
