namespace Contracts.Media.Convertions;

public record ResolvedId(IMediaId Id, IEnumerable<IMediaId> AdditionalIds)
{
	public bool Successful => Id is not Erroneous;
	public IEnumerable<IMediaId> AllIds => Successful ? AdditionalIds.Prepend(Id) : AdditionalIds;

	public static ResolvedId Unresolved(IEnumerable<IMediaId> additionalIds) => new(new Erroneous(), additionalIds);

	public static ResolvedId Failed => Unresolved([]);

	private class Erroneous : IMediaId
	{
		public MediaProvider Source { get; set; }
		public MediaType Type { get; set; }
		public string Value { get; set; }
	}
}
