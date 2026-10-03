using System.Reflection;
using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;

namespace Architecture.Tests;

public class MediaModelTests
{
	private static readonly Type[] ValueColumnTypes = [typeof(string), typeof(DateOnly), typeof(TimeSpan), typeof(int)];

	[Test]
	public async Task MediaModel_OfProviderContracts_NeverAnswersWithAMatch()
	{
		Type[] providers = [typeof(IDetailsEnricher), typeof(IMediaSearchProvider)];
		Type[] matches = [typeof(MatchedMedia), typeof(MediaMatchResult)];

		var offenders = providers
			.SelectMany(provider => provider.GetMethods())
			.Where(method => Signature(method).SelectMany(Unfold).Any(matches.Contains))
			.Select(Methods.Describe);

		await offenders.AssertNone();
	}

	[Test]
	public async Task MediaModel_OfMediaDetails_HasExactlyTheMovieShowAndUnknownCases()
	{
		var cases = Projects.Domain.GetTypes().Where(type => type.BaseType == typeof(MediaDetails)).ToList();

		await Assert.That(cases).IsEquivalentTo([typeof(MovieDetails), typeof(ShowDetails), typeof(UnknownDetails)]);
		await cases.Where(type => !type.IsSealed).Select(type => type.FullName).AssertNone();
	}

	[Test]
	public async Task MediaModel_OfTheDetailsRows_KeepsEveryValueColumnNullable()
	{
		var root = Projects.Db.GetType("DB.Data.Media.MediaDetailsRow", throwOnError: true)!;
		var nullability = new NullabilityInfoContext();
		var columns = Projects
			.Db.GetTypes()
			.Where(root.IsAssignableFrom)
			.SelectMany(row => row.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			.Where(property => ValueColumnTypes.Contains(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType))
			.ToList();

		await Assert.That(columns).IsNotEmpty();
		await columns
			.Where(property => nullability.Create(property).ReadState is not NullabilityState.Nullable)
			.Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
			.AssertNone();
	}

	[Test]
	public async Task MediaModel_OfConfirmedAbsent_LivesOnlyOnTheCachedDetails()
	{
		var holders = new[] { Projects.Domain, Projects.Contracts }
			.SelectMany(assembly => assembly.GetTypes())
			.Where(type => type != typeof(CachedDetails))
			.Where(type =>
				type.GetMember(nameof(CachedDetails.ConfirmedAbsent), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length
				> 0
			)
			.Select(type => type.FullName);

		await holders.AssertNone();
	}

	private static IEnumerable<Type> Signature(MethodInfo method) => [method.ReturnType, .. method.GetParameters().Select(parameter => parameter.ParameterType)];

	private static IEnumerable<Type> Unfold(Type type)
	{
		return [type, .. type.GetGenericArguments().SelectMany(Unfold), .. type.HasElementType ? Unfold(type.GetElementType()!) : []];
	}
}
