namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class MediaCatalogSteps(MediaCatalogContext catalog)
{
	[Given("the movie {string}")]
	public void GivenMovie(string title) => catalog.RegisterMovie(title);

	[Given("the person {string}")]
	public void GivenPerson(string title) => catalog.RegisterPerson(title);
}
