namespace Domain.Notifications;

public union DeliveryResult(DeliverySucceeded, DeliveryTransient, DeliveryPermanent);

public readonly record struct DeliverySucceeded;

public readonly record struct DeliveryTransient(string Reason);

public readonly record struct DeliveryPermanent(string Reason);
