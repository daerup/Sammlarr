using Domain.Notifications;
using Microsoft.AspNetCore.Components;

namespace UI.Features.Settings.Components.Notifications;

public abstract class ClientEditorBase : ComponentBase
{
	[Parameter]
	public NotificationTarget? Target { get; set; }

	protected bool IsCreate => Target is null;

	public abstract bool TryBuild(out NotificationConfig config, out string? error);

	public virtual string? CreatedSecret => null;
}
