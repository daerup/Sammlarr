namespace API.Mdblist;

internal static class MdblistEndpoints
{
	public const string User = "user";
	public const string ListItemsPath = "lists/{0}/items";
	public const string ListItems = $"{ListItemsPath}?limit={{1}}&offset={{2}}";
}
