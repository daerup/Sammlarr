using Domain.Providers;

namespace Domain.Feed.Trace;

public union TraceEvent(TitleStarted, SourceEnded, ListedWithoutIds, TitleKnown, TitleNotFoundEarlier, PathStarted, HopAsked, HopAnswered, GaveUp, TitleSettled);

public readonly record struct TitleStarted(string Name, Optional<int> Year);

public readonly record struct SourceEnded(int Requests);

public readonly record struct ListedWithoutIds;

public readonly record struct TitleKnown;

public readonly record struct TitleNotFoundEarlier;

public readonly record struct PathStarted;

public readonly record struct HopAsked(RemoteService Service, TraceStage Stage);

public readonly record struct HopAnswered(RemoteService Service, TraceStage Stage, HopOutcome Outcome, int Ms);

public readonly record struct GaveUp(RemoteService Service, TraceStage Stage);

public readonly record struct TitleSettled(TitleFate Fate);
