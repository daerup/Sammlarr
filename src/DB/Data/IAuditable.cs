namespace DB.Data;

internal interface IAuditable
{
	DateTimeOffset CreatedAt { get; set; }
	DateTimeOffset UpdatedAt { get; set; }
}
