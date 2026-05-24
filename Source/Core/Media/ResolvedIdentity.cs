using Contracts.Media;
using Contracts.Media.Metadata;

namespace Core.Media;

internal sealed record ResolvedIdentity(MediaIdentity Identity, CachedMedia? Cached);
