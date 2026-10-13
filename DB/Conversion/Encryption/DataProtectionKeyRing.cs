using Microsoft.Data.Sqlite;

namespace DB.Conversion.Encryption;

internal static class DataProtectionKeyRing
{
	private const string PathEnvironmentVariable = "Sammlarr__DataProtection__KeyRingPath";
	private const string DefaultDirectoryName = "dp-keys";

	internal static DirectoryInfo Resolve(string connectionString)
	{
		var configured = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
		var path = string.IsNullOrWhiteSpace(configured) ? BesideTheDatabase(connectionString) : configured;
		var directory = new DirectoryInfo(path);
		directory.Create();
		return directory;
	}

	private static string BesideTheDatabase(string connectionString)
	{
		var database = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
		return Path.Combine(Path.GetDirectoryName(database)!, DefaultDirectoryName);
	}
}
