namespace E2E.Tests.Recording;

public class JsonContractTests
{
	[Test]
	public async Task Incompatibilities_WhenOnlyValuesDiffer_ReportsNone()
	{
		var live = """{"page":2,"results":[{"id":99,"title":"Other"}]}""";
		var committed = """{"page":1,"results":[{"id":1,"title":"Practical Magic 2"}]}""";

		await Assert.That(JsonContract.Incompatibilities(live, committed)).IsEmpty();
	}

	[Test]
	public async Task Incompatibilities_WhenLiveHasANewField_ReportsIt()
	{
		var live = """{"results":[{"id":1,"title":"X","streaming":true}]}""";
		var committed = """{"results":[{"id":1,"title":"X"}]}""";

		var problems = JsonContract.Incompatibilities(live, committed);

		await Assert.That(problems).IsNotEmpty();
		await Assert.That(string.Join(";", problems)).Contains("streaming");
	}

	[Test]
	public async Task Incompatibilities_WhenAFieldChangesType_ReportsIt()
	{
		var live = """{"results":[{"id":"1"}]}""";
		var committed = """{"results":[{"id":1}]}""";

		var problems = JsonContract.Incompatibilities(live, committed);

		await Assert.That(problems).IsNotEmpty();
		await Assert.That(string.Join(";", problems)).Contains("id");
	}

	[Test]
	public async Task Incompatibilities_WhenArrayMixesMovieAndShow_UnionsTheShapes()
	{
		var live = """{"results":[{"id":1,"title":"Movie"},{"id":2,"name":"Show"}]}""";
		var committed = """{"results":[{"id":9,"name":"Show"},{"id":8,"title":"Movie"}]}""";

		await Assert.That(JsonContract.Incompatibilities(live, committed)).IsEmpty();
	}

	[Test]
	public async Task Incompatibilities_WhenLiveArrayIsASubsetOfTheFixture_ReportsNone()
	{
		var live = """{"results":[{"id":1,"title":"Movie"}]}""";
		var committed = """{"results":[{"id":1,"title":"Movie"},{"id":2,"name":"Show"}]}""";

		await Assert.That(JsonContract.Incompatibilities(live, committed)).IsEmpty();
	}

	[Test]
	public async Task Incompatibilities_WhenLiveIsNullForATypedField_ReportsNone()
	{
		var live = """{"results":[{"poster_path":null}]}""";
		var committed = """{"results":[{"poster_path":"/x.jpg"}]}""";

		await Assert.That(JsonContract.Incompatibilities(live, committed)).IsEmpty();
	}
}
