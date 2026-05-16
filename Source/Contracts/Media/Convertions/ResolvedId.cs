namespace Contracts.Media.Convertions;

public record Resolved(IMediaId Id, IEnumerable<IMediaId> AdditionalIds)
{
    public IEnumerable<IMediaId> AllIds => AdditionalIds.Prepend(Id).Distinct();
}

public readonly record struct Unresolved;

public union ConversionResult(Resolved, Unresolved);
