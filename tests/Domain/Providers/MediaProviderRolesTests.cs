using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Providers;

public class MediaProviderRolesTests
{
	private static readonly IExternalMediaId[] EveryId =
	[
		TmdbId.From("550", MediaType.Movie),
		ImdbId.From("tt0137523", MediaType.Movie),
		TvdbId.From("81189", MediaType.Show),
		PlexId.From("5d776", MediaType.Movie),
		MdblistId.From("a5as", MediaType.Movie),
		TraktId.From("432", MediaType.Movie),
		MalId.From("5114", MediaType.Show),
		TvmazeId.From("169", MediaType.Show),
		AniListId.From("5114", MediaType.Show),
		RottenTomatoesId.From("m/fight_club", MediaType.Movie),
		MetacriticId.From("fight-club", MediaType.Movie),
		LetterboxdId.From("fight-club", MediaType.Movie),
	];

	public static IEnumerable<Func<IExternalMediaId>> Ids() => EveryId.Select<IExternalMediaId, Func<IExternalMediaId>>(id => () => id);

	[Test]
	[MethodDataSource(nameof(Ids))]
	public async Task Role_ForEveryProvider_MatchesTheInterfaceOfItsIdType(IExternalMediaId id)
	{
		var expected = id is IIdentityKey ? ExternalIdRole.IdentityKey : ExternalIdRole.PageLink;

		await Assert.That(id.Source.Role).IsEqualTo(expected);
	}

	[Test]
	public async Task Ids_CoverEveryProvider() => await Assert.That(EveryId.Select(id => id.Source)).IsEquivalentTo(Enum.GetValues<MediaProvider>());
}
