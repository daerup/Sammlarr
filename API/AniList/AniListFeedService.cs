using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using API.AniList.Types;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Feed;
using Domain.Media;

namespace API.AniList;

internal sealed partial class AniListFeedService(AniListClient client, IMediaService media) : IFeedProvider
{
	private const string Query =
		$"query ($user: String, $status: MediaListStatus) {{ MediaListCollection(userName: $user, type: ANIME, status: $status) {{ lists {{ entries {{ media {{ {AniListMedia.Fields} }} }} }} }} }}";

	private static readonly string[] Statuses = ["current", "planning", "completed", "dropped", "paused", "repeating"];

	private static readonly Error NotAList = Error.From(
		"Paste an AniList username, username/status (current, planning, completed, dropped, paused or repeating), or the list's anilist.co link."
	);

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		var match = ListPattern().Match(input.Trim());
		var status = match.Groups["status"].Value.ToLowerInvariant();
		var normalized = Optional.When(
			match.Success && (status.Length == 0 || Statuses.Contains(status)),
			() => status.Length == 0 ? match.Groups["user"].Value : $"{match.Groups["user"].Value}/{status}"
		);
		return Task.FromResult(normalized.ToResult(NotAList));
	}

	public IAsyncEnumerable<MediaMatchResult> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default)
	{
		return media.IdentifyWithDetailsAsync(RequestsAsync(identifier, cancellationToken), cancellationToken);
	}

	private async IAsyncEnumerable<IdentifyRequest> RequestsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var parts = identifier.Split('/');
		var variables = new JsonObject { ["user"] = parts[0] };
		parts.Skip(1).FirstOrNone().Apply(status => variables["status"] = status.ToUpperInvariant());
		var answer = await client.AskAsync<AniListTypes.ListData>(Query, variables, cancellationToken);
		var collection = answer.GetOrThrow(error => Failure(identifier, error));
		foreach (var found in (collection.Collection?.Lists ?? []).SelectMany(list => list.Entries ?? []).Select(entry => entry.Media).OfType<AniListTypes.Media>())
		{
			var type = AniListMedia.TypeOf(found);
			yield return new IdentifyRequest([.. AniListMedia.Ids(found, type)], AniListMedia.Hints(found, type));
		}
	}

	private static Exception Failure(string identifier, Error error)
	{
		return error
			.CausedBy<AniListAnswerException>()
			.Match<AniListAnswerException, Exception>(
				problem =>
					problem.Message.Contains("Private", StringComparison.OrdinalIgnoreCase)
						? new InvalidOperationException($"AniList keeps the list of {identifier.Split('/')[0]} private, so Sammlarr cannot read it.")
						: new FeedIdentifierNotFoundException(identifier),
				() => error.Exception.GetValueOr(() => new InvalidOperationException(error.Reason))
			);
	}

	[GeneratedRegex(@"^(?:(?:https?://)?(?:www\.)?anilist\.co/user/)?(?<user>[A-Za-z0-9_-]+)(?:/animelist)?(?:/(?<status>[A-Za-z]+))?/?$")]
	private static partial Regex ListPattern();
}
