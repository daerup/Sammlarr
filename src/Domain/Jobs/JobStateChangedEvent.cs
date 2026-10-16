using Domain.Announcing;
using Domain.Identity;

namespace Domain.Jobs;

public sealed record JobStateChangedEvent(UserId UserId, JobKind Kind, string Key, JobState State) : IEvent;
