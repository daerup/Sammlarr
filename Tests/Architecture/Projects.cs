using Assembly = System.Reflection.Assembly;

namespace Architecture.Tests;

internal static class Projects
{
	internal static readonly Assembly Shared = Of<Shared.SharedModule>();
	internal static readonly Assembly Domain = Of<Domain.Media.MediaId>();
	internal static readonly Assembly Contracts = Of<Contracts.Media.IMediaService>();
	internal static readonly Assembly Core = Of<Core.CoreModule>();
	internal static readonly Assembly Db = Of<DB.DbModule>();
	internal static readonly Assembly Api = Of<API.ApiModule>();
	internal static readonly Assembly Observability = Of<Observability.ObservabilityModule>();
	internal static readonly Assembly Ui = Of<UI.Shared.Layout.MainLayout>();
	internal static readonly Assembly Messaging = Of<Messaging.MessagingModule>();

	internal static readonly Assembly[] All = [Shared, Domain, Contracts, Core, Db, Api, Observability, Ui];

	internal static readonly Assembly[] Consumers = [Core, Db, Api, Ui, Messaging];

	private static Assembly Of<T>() => typeof(T).Assembly;
}
