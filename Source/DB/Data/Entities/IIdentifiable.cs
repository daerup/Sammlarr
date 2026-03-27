namespace DB.Data.Entities;

public interface IIdentifiable<TId>
{
	TId Id { get; set; }
}
