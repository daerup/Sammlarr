using API.Arr.Types;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal sealed class ArrConnectionTester(ArrApis apis, ICurrentUser currentUser) : IArrConnectionTester
{
	public async Task<Result<ArrInstanceChoices>> TestAsync(ArrApp app, ArrInstanceConfig instance, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return await ArrSession
			.Connect(app, instance)
			.Match(connection => ChoicesAsync(apis.For(app), connection, cancellationToken), error => Task.FromResult<Result<ArrInstanceChoices>>(error));
	}

	private static async Task<Result<ArrInstanceChoices>> ChoicesAsync(ArrApi api, ArrConnection connection, CancellationToken cancellationToken)
	{
		var status = await api.StatusAsync(connection, cancellationToken);
		return await status
			.Map(answered => Answers(connection, answered))
			.Match(_ => ReadChoicesAsync(api, connection, cancellationToken), error => Task.FromResult<Result<ArrInstanceChoices>>(error));
	}

	private static Result<ArrSystemStatus> Answers(ArrConnection connection, ArrSystemStatus status)
	{
		var expected = connection.App.Name;
		return Optional
			.When(string.Equals(status.AppName, expected, StringComparison.OrdinalIgnoreCase), () => status)
			.ToResult(Error.From($"{connection.Address} answers as {status.AppName ?? "another app"}, not {expected}. Check the URL."));
	}

	private static async Task<Result<ArrInstanceChoices>> ReadChoicesAsync(ArrApi api, ArrConnection connection, CancellationToken cancellationToken)
	{
		var profiles = await api.QualityProfilesAsync(connection, cancellationToken);
		var folders = await api.RootFoldersAsync(connection, cancellationToken);
		return profiles.Map(found => folders.Map(roots => new ArrInstanceChoices([.. found.Select(Profile)], [.. roots.Select(Folder).WhereSome()])));
	}

	private static ArrQualityProfile Profile(ArrProfile profile) => new(profile.Id, profile.Name ?? $"Profile {profile.Id}");

	private static Optional<ArrRootFolder> Folder(ArrFolder folder) => Optional.Of(folder.Path).Map(path => new ArrRootFolder(path, folder.FreeSpace));
}
