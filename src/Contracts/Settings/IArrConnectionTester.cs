using Domain.Wantlist.Arr;

namespace Contracts.Settings;

public interface IArrConnectionTester
{
	Task<Result<ArrInstanceChoices>> TestAsync(ArrApp app, ArrInstanceConfig instance, CancellationToken cancellationToken = default);
}

public sealed record ArrInstanceChoices(IReadOnlyList<ArrQualityProfile> Profiles, IReadOnlyList<ArrRootFolder> Folders);

public sealed record ArrQualityProfile(int Id, string Name);

public sealed record ArrRootFolder(string Path, long FreeSpace);
