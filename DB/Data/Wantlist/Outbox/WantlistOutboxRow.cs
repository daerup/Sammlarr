using System.ComponentModel.DataAnnotations;
using DB.Data.Media;
using Domain.Wantlist.Outbox;

namespace DB.Data.Wantlist.Outbox;

internal record WantlistOutboxRow : IUserOwned
{
	public required MediaRow Media { get; set; }

	public required WantlistOperationAction Action { get; set; }

	public WantlistOutboxState Status { get; set; }

	public int Attempts { get; set; }

	[MaxLength(1000)]
	public string? LastError { get; set; }

	public DateTimeOffset AddedAt { get; set; }
}
