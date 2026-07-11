namespace DB.Data;

internal interface IIdentifiable<TId>
{
	TId Id { get; set; }
}
