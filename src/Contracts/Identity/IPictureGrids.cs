namespace Contracts.Identity;

public interface IPictureGrids
{
	Task<Optional<byte[]>> ReadAsync(string url, CancellationToken cancellationToken = default);
}
