using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace AppHost.Staging;

internal sealed record StagingResources(
	StagingLayout Layout,
	StagingScenarioChoice Scenario,
	IResourceBuilder<ContainerResource> Radarr,
	IResourceBuilder<ContainerResource> Sonarr,
	IResourceBuilder<ProjectResource> Seed
);

internal static class StagingCommands
{
	private static readonly TimeSpan Budget = TimeSpan.FromMinutes(15);

	private static readonly SemaphoreSlim Running = new(1, 1);

	internal static void WithStagingCommands(this IResourceBuilder<ContainerResource> sammlarr, StagingResources staging)
	{
		sammlarr.WithCommand(
			"restage",
			"Restage",
			context => ExclusiveAsync(context, run => ResetAsync(run, sammlarr.Resource, staging, StagingScenarioChoice.Configured)),
			new CommandOptions { ConfirmationMessage = "Wipe Sammlarr, Radarr and Sonarr and seed the set-up scenario again?", IconName = "ArrowCounterclockwise" }
		);
		sammlarr.WithCommand(
			"reset",
			"Reset",
			context => ExclusiveAsync(context, run => ResetAsync(run, sammlarr.Resource, staging, StagingScenarioChoice.Fresh)),
			new CommandOptions { ConfirmationMessage = "Wipe Sammlarr, Radarr and Sonarr and start Sammlarr with no admin?", IconName = "Delete" }
		);
		sammlarr.WithCommand(
			"rebuild",
			"Rebuild",
			context => ExclusiveAsync(context, run => RebuildAsync(run, sammlarr)),
			new CommandOptions { ConfirmationMessage = "Rebuild the Sammlarr image from the working tree and restart it?", IconName = "ArrowSync" }
		);
	}

	private static async Task<ExecuteCommandResult> ExclusiveAsync(ExecuteCommandContext context, Func<CommandRun, Task<ExecuteCommandResult>> command)
	{
		if (!await Running.WaitAsync(TimeSpan.Zero, context.CancellationToken))
		{
			return CommandResults.Failure("Another staging command is still running.");
		}

		try
		{
			using var budget = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
			budget.CancelAfter(Budget);
			return await command(
				new CommandRun(
					context.Services.GetRequiredService<ResourceCommandService>(),
					context.Services.GetRequiredService<ResourceNotificationService>(),
					budget.Token
				)
			);
		}
		finally
		{
			Running.Release();
		}
	}

	[SuppressMessage("ApiDesign", "RS0030", Justification = "The AppHost has no IDateTimeProvider; the seed's start compares against Aspire's own start stamps.")]
	private static async Task<ExecuteCommandResult> ResetAsync(CommandRun run, IResource sammlarr, StagingResources staging, string scenario)
	{
		foreach (var resource in new[] { sammlarr, staging.Radarr.Resource, staging.Sonarr.Resource })
		{
			if (await run.StopAsync(resource) is { Success: false } refused)
			{
				return refused;
			}
		}

		staging.Layout.Wipe();
		staging.Scenario.Value = scenario;
		foreach (var resource in new[] { staging.Radarr.Resource, staging.Sonarr.Resource })
		{
			if (await run.StartHealthyAsync(resource) is { Success: false } refused)
			{
				return refused;
			}
		}

		var started = DateTime.UtcNow;
		if (await run.ExecuteAsync(staging.Seed.Resource, KnownResourceCommands.StartCommand) is { Success: false } seedRefused)
		{
			return seedRefused;
		}

		var seeded = await run.Notifications.WaitForResourceAsync(
			staging.Seed.Resource.Name,
			update => KnownResourceStates.TerminalStates.Contains(update.Snapshot.State?.Text) && update.Snapshot.StartTimeStamp > started,
			run.Token
		);
		if (seeded.Snapshot.State?.Text != KnownResourceStates.Finished || seeded.Snapshot.ExitCode != 0)
		{
			return CommandResults.Failure($"The seeder ended {seeded.Snapshot.State?.Text} with exit code {seeded.Snapshot.ExitCode}; its log has the reason.");
		}

		return await run.StartHealthyAsync(sammlarr);
	}

	private static async Task<ExecuteCommandResult> RebuildAsync(CommandRun run, IResourceBuilder<ContainerResource> sammlarr)
	{
		sammlarr.Resource.TryGetContainerImageName(out var image);
		using var build = Process.Start(
			new ProcessStartInfo("docker", ["build", "--tag", image!, "--file", StagingEnvironment.Dockerfile, "."])
			{
				WorkingDirectory = StagingEnvironment.RepositoryRoot(sammlarr.ApplicationBuilder),
				RedirectStandardError = true,
				UseShellExecute = false,
			}
		)!;
		await using var cancel = run.Token.Register(() => build.Kill(entireProcessTree: true));
		var errors = await build.StandardError.ReadToEndAsync(run.Token);
		await build.WaitForExitAsync(run.Token);
		if (build.ExitCode != 0)
		{
			return CommandResults.Failure($"docker build failed: {errors[^Math.Min(errors.Length, 500)..]}");
		}

		if (await run.StopAsync(sammlarr.Resource) is { Success: false } refused)
		{
			return refused;
		}

		return await run.StartHealthyAsync(sammlarr.Resource);
	}

	private sealed record CommandRun(ResourceCommandService Commands, ResourceNotificationService Notifications, CancellationToken Token)
	{
		internal Task<ExecuteCommandResult> ExecuteAsync(IResource resource, string command) => Commands.ExecuteCommandAsync(resource, command, Token);

		internal async Task<ExecuteCommandResult> StopAsync(IResource resource)
		{
			var stopped = await ExecuteAsync(resource, KnownResourceCommands.StopCommand);
			if (stopped.Success)
			{
				await Notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.TerminalStates, Token);
			}

			return stopped;
		}

		internal async Task<ExecuteCommandResult> StartHealthyAsync(IResource resource)
		{
			var started = await ExecuteAsync(resource, KnownResourceCommands.StartCommand);
			if (started.Success)
			{
				await Notifications.WaitForResourceHealthyAsync(resource.Name, Token);
			}

			return started;
		}
	}
}
