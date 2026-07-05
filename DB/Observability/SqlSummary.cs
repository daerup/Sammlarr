using System.Text.RegularExpressions;

namespace DB.Observability;

internal static partial class SqlSummary
{
	[GeneratedRegex("""^\s*(?<op>SELECT|INSERT|UPDATE|DELETE)""", RegexOptions.IgnoreCase)]
	private static partial Regex OperationRegex();

	[GeneratedRegex("""(?:FROM|INTO|UPDATE)\s+"?(?<table>[A-Za-z_][A-Za-z0-9_]*)"?""", RegexOptions.IgnoreCase)]
	private static partial Regex TableRegex();

	public static (string? Operation, string? Table) Parse(string commandText)
	{
		var op = OperationRegex().Match(commandText);
		var table = TableRegex().Match(commandText);
		return (op.Success ? op.Groups["op"].Value.ToUpperInvariant() : null, table.Success ? table.Groups["table"].Value : null);
	}
}
