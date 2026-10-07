namespace API.Mdblist;

internal static class MdblistEndpoints
{
	public const string User = "user";
	public const string List = "lists/{0}";
	public const string ListItems = "lists/{0}/items";
	public const string ExternalListItems = "external/lists/{0}/items";
	public const string Page = "{0}?limit={1}&offset={2}";

	public const string Titles = "{0}/{1}";
}
