using System.Linq.Expressions;
using Contracts.Media;

namespace DB.Data.MediaGroup;

internal static class MediaIdentifierGroupEntryExpressions
{
	public static Expression<Func<MediaIdentifierGroupEntry, bool>> MatchesId(IMediaId mediaId)
	{
		var identifier = MediaIdentifier.From(mediaId);
		return entry => entry.Identifier == identifier;
	}

	public static Expression<Func<MediaIdentifierGroupEntry, bool>> MatchesAnyIdentifier(IEnumerable<MediaIdentifier> identifiers)
	{
		var identifierList = identifiers.ToList();
		if (identifierList.Count is 0)
		{
			return _ => false;
		}

		var parameter = Expression.Parameter(typeof(MediaIdentifierGroupEntry), "entry");
		var identifierProperty = Expression.Property(parameter, nameof(MediaIdentifierGroupEntry.Identifier));

		Expression? body = null;
		foreach (var identifier in identifierList)
		{
			var equalsExpression = Expression.Equal(identifierProperty, Expression.Constant(identifier));
			body = body is null ? equalsExpression : Expression.OrElse(body, equalsExpression);
		}

		return Expression.Lambda<Func<MediaIdentifierGroupEntry, bool>>(body!, parameter);
	}
}
