using Contracts.Media;
using Core.Media;
using Domain.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;

namespace Core.Tests.Media;

public class LibraryWriterTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task Save_ForAnUnchangedTitle_QueuesNothing()
	{
		var repository = IMediaRepository.Mock();
		var details = Cached("Heat");

		await using (var testee = new LibraryWriter(repository.Object, NullLogger<LibraryWriter>.Instance))
		{
			testee.Save(new DetailsChange(MediaId.New(), Some.Of(details), details));
		}

		repository.SaveDetailsAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task DisposeAsync_AfterManySaves_CommitsThemInGroupsOfAtMost256()
	{
		var repository = IMediaRepository.Mock();
		var held = Held(repository);

		await using (var testee = new LibraryWriter(repository.Object, NullLogger<LibraryWriter>.Instance))
		{
			testee.Save(held.Change);
			await held.Entered.Task;
			foreach (var _ in Enumerable.Range(0, 600))
			{
				testee.Save(New());
			}

			held.Release.SetResult();
		}

		await Assert.That(Committed(repository).Select(group => group.Count)).IsEquivalentTo([1, 256, 256, 88], CollectionOrdering.Matching);
	}

	[Test]
	public async Task CommitAsync_WhenAGroupFails_WritesEachChangeAloneAndLogsOnlyTheOneThatFails()
	{
		var repository = IMediaRepository.Mock();
		var logger = ILogger<LibraryWriter>.Mock();
		var (first, broken, last) = (New(), New(), New());
		var held = Held(repository);
		repository
			.SaveDetailsAsync(Is<IReadOnlyList<DetailsChange>>(changes => changes.Contains(broken)), Any())
			.Throws(new InvalidOperationException("broken row"));

		await using (var testee = new LibraryWriter(repository.Object, logger.Object))
		{
			testee.Save(held.Change);
			await held.Entered.Task;
			testee.Save(first);
			testee.Save(broken);
			testee.Save(last);
			held.Release.SetResult();
		}

		await Assert.That(Committed(repository).Where(group => group.Count == 1).Select(group => group[0])).Contains(first).And.Contains(last);
		await Assert
			.That(Warnings(logger))
			.IsEquivalentTo([
				"A group commit of 3 details failed, so each is written on its own",
				$"The details of {broken.Id} could not be written and will be fetched again",
			]);
	}

	private static DetailsChange New() => new(MediaId.New(), new None(), Cached("Heat"));

	private static HeldCommit Held(Mock<IMediaRepository> repository)
	{
		var held = new HeldCommit(New(), new TaskCompletionSource(), new TaskCompletionSource());
		repository
			.SaveDetailsAsync(Is<IReadOnlyList<DetailsChange>>(changes => changes.Contains(held.Change)), Any())
			.Returns(() =>
			{
				held.Entered.TrySetResult();
				return held.Release.Task;
			});
		return held;
	}

	private static CachedDetails Cached(string title) => new(new MovieDetails { Title = title }, Now, Now, []);

	private static List<IReadOnlyList<DetailsChange>> Committed(Mock<IMediaRepository> repository)
	{
		return [.. Mock.Invocations(repository).Select(call => call.Arguments[0]).OfType<IReadOnlyList<DetailsChange>>()];
	}

	private static IEnumerable<string> Warnings(Mock<ILogger<LibraryWriter>> logger)
	{
		return Mock.Invocations(logger).Where(call => call.Arguments[0] is LogLevel.Warning).Select(call => $"{call.Arguments[2]}");
	}

	private sealed record HeldCommit(DetailsChange Change, TaskCompletionSource Entered, TaskCompletionSource Release);
}
