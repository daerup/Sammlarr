using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal readonly union Resolution(Resolved, NoMatchAnywhere, NotFoundEarlier, NotRun);

internal sealed record Resolved(IReadOnlyList<IExternalMediaId> Ids, ValueSet<RemoteService> Sources);

internal sealed record NoMatchAnywhere;

internal sealed record NotFoundEarlier;

internal sealed record NotRun;
