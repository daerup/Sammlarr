namespace DB.Data.Entities;

public interface IAuditable
{
	DateTimeOffset CreatedAt { get; set; }
	DateTimeOffset UpdatedAt { get; set; }
}
