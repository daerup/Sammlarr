using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed record DetailsRun(IReadOnlySet<RemoteService> Connected, FieldNeeds Needs, LibraryWriter Writer);
