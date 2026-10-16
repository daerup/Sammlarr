using System.ComponentModel.DataAnnotations.Schema;
using Domain.Media;
using Domain.Providers;

namespace DB.Data.Media;

[Table("IdentityConflicts")]
internal sealed record IdentityConflictRow
{
	public Guid Id { get; init; }

	public required Guid HeldId { get; init; }

	public required Guid OtherId { get; init; }

	public required MediaProvider ClaimedSource { get; init; }

	public required MediaType ClaimedType { get; init; }

	public required string ClaimedId { get; init; }

	public required DateTimeOffset SeenAt { get; set; }

	public int Attempts { get; set; }

	public required DateTimeOffset AskAfter { get; set; }

	public RemoteService? AskedService { get; set; }

	public AskOutcome? AskedOutcome { get; set; }

	public DateTimeOffset? AskedAt { get; set; }

	public DistinctKind? Distinct { get; set; }

	public DateTimeOffset? DistinctAt { get; set; }

	public MediaProvider? ContradictedBy { get; set; }

	public RemoteService? AnsweredBy { get; set; }

	public MediaProvider? AnswerSource { get; set; }

	public MediaType? AnswerType { get; set; }

	public string? AnswerId { get; set; }

	public Guid? KeptApartBy { get; set; }

	public List<IdentityConflictSourceRow> Sources { get; init; } = [];
}

[Table("IdentityConflictSources")]
internal sealed record IdentityConflictSourceRow
{
	public Guid ConflictId { get; init; }

	public required RemoteService Service { get; init; }
}

internal enum DistinctKind
{
	Contradiction,
	TypesDiffer,
	AnsweredElsewhere,
	KeptApartByHand,
}
