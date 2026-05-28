namespace Contracts.Media.Convertions;

public record Identification(MediaId Id, IReadOnlyList<IExternalMediaId> Externals, IExternalMediaId TargetExternal);
