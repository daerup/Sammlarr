namespace Contracts.Media.Convertions;

public record Resolved(MediaId Id, IReadOnlyList<IExternalMediaId> Externals, IExternalMediaId TargetExternal);

public readonly record struct Unresolved;

public union ConversionResult(Resolved resolved, Unresolved unresolved);
