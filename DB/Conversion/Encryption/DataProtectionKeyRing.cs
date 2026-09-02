namespace DB.Conversion.Encryption;

internal static class DataProtectionKeyRing
{
	private const string PathEnvironmentVariable = "Sammlarr__DataProtection__KeyRingPath";
	private const string DefaultDirectoryName = "dp-keys";

	internal static DirectoryInfo Resolve()
	{
		var configured = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
		var path = string.IsNullOrWhiteSpace(configured) ? Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectoryName) : configured;
		var directory = new DirectoryInfo(path);
		directory.Create();
		return directory;
	}
}
