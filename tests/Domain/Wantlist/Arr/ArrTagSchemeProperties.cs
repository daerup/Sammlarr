using System.Text.RegularExpressions;
using CsCheck;
using Domain.Wantlist.Arr;

namespace Domain.Tests.Wantlist.Arr;

public partial class ArrTagSchemeProperties
{
	[Test]
	public void Labels_ForAnyUserAndFeedName_UseOnlyCharactersRadarrAccepts()
	{
		Gen.Select(Gen.String, Gen.String, Gen.Guid, Gen.Guid)
			.Sample(
				(user, feed, userId, feedId) =>
					ArrTagScheme.Default.Labels(new ArrTagUser(userId, user), Some.Of(new ArrTagFeed(feedId, feed))).All(label => Accepted().IsMatch(label))
			);
	}

	[Test]
	public void Labels_ForAnySchemeAndTitle_HoldALabelTheSyncReadsBack()
	{
		Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool, Gen.String)
			.Sample(
				(perUser, perFeed, plain, name) =>
				{
					var scheme = ArrTagScheme.Default with { PerUser = perUser, PerFeed = perFeed, PlainPrefix = plain };
					var user = new ArrTagUser(Guid.NewGuid(), name);
					var labels = scheme.Labels(user, new None());
					return perUser ? labels.Contains(scheme.UserTag(user)) : labels.Any(scheme.Owns);
				}
			);
	}

	[Test]
	public void FeedTag_ForAnyFeed_IsNeverAUsersTag()
	{
		Gen.String.Sample(name => !ArrTagScheme.Default.IsUserTag(ArrTagScheme.Default.FeedTag(new ArrTagFeed(Guid.NewGuid(), name))));
	}

	[GeneratedRegex("^[a-z0-9-]+$")]
	private static partial Regex Accepted();
}
