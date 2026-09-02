namespace Domain.Notifications;

public union DeliveryOutcome(Delivered, Retrying, Failed);

public readonly record struct Delivered;

public readonly record struct Retrying(string Reason, DateTimeOffset NextAttemptAt);

public readonly record struct Failed(string Reason);
