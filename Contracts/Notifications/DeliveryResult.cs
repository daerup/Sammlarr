namespace Contracts.Notifications;

public union DeliveryResult(DeliverySucceeded, DeliveryRetryable, DeliveryUnrecoverable);

public readonly record struct DeliverySucceeded;

public readonly record struct DeliveryRetryable(string Reason);

public readonly record struct DeliveryUnrecoverable(string Reason);
