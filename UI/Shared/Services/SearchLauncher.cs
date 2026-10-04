namespace UI.Shared.Services;

public enum SearchPurpose
{
	Wantlist,
	Blocklist,
}

internal sealed class SearchLauncher
{
	public event Action<SearchPurpose>? Opened;

	public void Open(SearchPurpose purpose) => Opened?.Invoke(purpose);
}
