namespace TestInfrastructure;

public static class RepositoryRoot
{
	private static readonly string Root = Find();

	public static string Resolve(params string[] parts) => Path.Combine([Root, .. parts]);

	private static string Find()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(directory.FullName, "Sammlarr.slnx")))
		{
			directory = directory.Parent!;
		}

		return directory.FullName;
	}
}
