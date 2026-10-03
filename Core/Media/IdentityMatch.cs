using Contracts.Media;
using Domain.Media;

namespace Core.Media;

internal sealed record IdentityMatch(MediaIdentity Identity, Optional<StoredMedia> Stored);
