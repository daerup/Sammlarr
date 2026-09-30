using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Core.Wantlist.Sync;

internal readonly record struct PushOutcome(MediaId MediaId, WantlistOperationAction Action, WantlistOutboxEventKind Kind);
