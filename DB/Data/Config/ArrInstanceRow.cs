using System.ComponentModel.DataAnnotations;
using DB.Conversion.Encryption;

namespace DB.Data.Config;

internal sealed record ArrInstanceRow
{
	[MaxLength(2048)]
	public string? Url { get; set; }

	[MaxLength(4096)]
	[Encrypted]
	public string? ApiKey { get; set; }

	public int? QualityProfileId { get; set; }

	[MaxLength(1024)]
	public string? RootFolderPath { get; set; }

	public bool Monitored { get; set; } = true;

	public bool SearchOnAdd { get; set; } = true;
}
