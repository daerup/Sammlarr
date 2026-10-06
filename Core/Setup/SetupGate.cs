using System.Security.Cryptography;
using System.Text;
using Shared.Options;

namespace Core.Setup;

internal sealed class SetupGate(AccountsOptions options)
{
	private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

	private const int Length = 8;

	private readonly string _code = Configured(options.SetupCode).GetValueOr(() => new string(RandomNumberGenerator.GetItems<char>(Alphabet, Length)));

	private readonly SemaphoreSlim _claim = new(1, 1);

	private volatile bool _adminSeen;

	internal string Code => $"{_code[..4]}-{_code[4..]}";

	internal bool AdminSeen => _adminSeen;

	internal void SawAdmin() => _adminSeen = true;

	internal bool Accepts(string candidate)
	{
		return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Typed(candidate)), Encoding.ASCII.GetBytes(_code));
	}

	internal async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
	{
		await _claim.WaitAsync(cancellationToken);
		return new Lease(_claim);
	}

	private static Optional<string> Configured(string setupCode)
	{
		var code = Typed(setupCode);
		return Optional.When(code.Length > 0, () => Valid(code));
	}

	private static string Valid(string code)
	{
		return code.Length == Length && code.All(Alphabet.Contains)
			? code
			: throw new InvalidOperationException(
				$"Sammlarr:Accounts:SetupCode must be {Length} characters from {Alphabet}, dashes and spaces aside, like the codes Sammlarr makes."
			);
	}

	private static string Typed(string candidate) => new([.. candidate.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);

	private sealed class Lease(SemaphoreSlim claim) : IDisposable
	{
		public void Dispose() => claim.Release();
	}
}
