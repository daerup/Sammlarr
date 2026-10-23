using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Architecture.Tests;

public partial class SecretNameTests
{
	private static readonly Type[] Plain = [typeof(string), typeof(Optional<string>)];

	private static readonly string[] Allowed = ["UI.Features.Settings.Components.Notifications.TemplateVariable.Token"];

	[Test]
	public async Task Naming_OfSecretMembers_RequiresTheSecretType()
	{
		await Projects
			.All.Append(Projects.Messaging)
			.SelectMany(assembly => assembly.GetTypes())
			.Where(type => !IsEdge(type))
			.SelectMany(Members)
			.Where(member => Plain.Contains(member.Type) && SecretName().IsMatch(member.Name) && !member.Name.EndsWith("k__BackingField", StringComparison.Ordinal))
			.Select(member => $"{member.Owner.FullName}.{member.Name}")
			.Except(Allowed)
			.AssertNone();
	}

	private static IEnumerable<(Type Owner, string Name, Type Type)> Members(Type owner)
	{
		return
		[
			.. owner.GetProperties(Absence.Declared).Select(property => (owner, property.Name, property.PropertyType)),
			.. owner.GetFields(Absence.Declared).Where(field => !field.IsLiteral).Select(field => (owner, field.Name, field.FieldType)),
		];
	}

	private static bool IsEdge(Type type)
	{
		var space = type.Namespace ?? string.Empty;
		return typeof(ComponentBase).IsAssignableFrom(type)
			|| (space.StartsWith("API.", StringComparison.Ordinal) && space.EndsWith(".Types", StringComparison.Ordinal))
			|| type.IsDefined(typeof(CompilerGeneratedAttribute));
	}

	[GeneratedRegex("secret|token|apikey|password|passphrase|confirmation|clientid|userkey|apptoken|setupcode|credential", RegexOptions.IgnoreCase)]
	private static partial Regex SecretName();
}
