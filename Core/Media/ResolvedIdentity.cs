using Contracts.Media;
using Domain.Media;

namespace Core.Media;

internal sealed record ResolvedIdentity(MediaIdentity Identity, Optional<StoredMedia> Cached);
