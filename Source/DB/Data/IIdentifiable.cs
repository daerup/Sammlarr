namespace DB.Data;

public interface IIdentifiable<TId>
{
	TId Id { get; set; }
}
