using Contracts.Media;
using Contracts.Media.Metadata;
using Domain.Media;

namespace Core.Media;

internal sealed record ResolvedIdentity(MediaIdentity Identity, CachedMedia? Cached);
