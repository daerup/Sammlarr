using System.Security.Cryptography;
using System.Text;

namespace Core.Setup;

internal sealed class SetupGate
{
	private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

	private readonly string _code = new(RandomNumberGenerator.GetItems<char>(Alphabet, 8));

	private readonly SemaphoreSlim _claim = new(1, 1);

	private volatile bool _adminSeen;

	internal string Code => $"{_code[..4]}-{_code[4..]}";

	internal bool AdminSeen => _adminSeen;

	internal void SawAdmin() => _adminSeen = true;

	internal bool Accepts(string candidate)
	{
		var typed = new string([.. candidate.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);
		return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(typed), Encoding.ASCII.GetBytes(_code));
	}

	internal async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
	{
		await _claim.WaitAsync(cancellationToken);
		return new Lease(_claim);
	}

	private sealed class Lease(SemaphoreSlim claim) : IDisposable
	{
		public void Dispose() => claim.Release();
	}
}
