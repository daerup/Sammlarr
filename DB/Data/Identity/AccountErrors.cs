using Microsoft.AspNetCore.Identity;

namespace DB.Data.Identity;

internal sealed class AccountErrors : IdentityErrorDescriber
{
	public override IdentityError PasswordTooShort(int length) => Described(nameof(PasswordTooShort), $"A password needs at least {length} characters.");

	public override IdentityError PasswordMismatch() => Described(nameof(PasswordMismatch), "The current password is not right.");

	public override IdentityError DuplicateUserName(string userName) => Described(nameof(DuplicateUserName), $"{userName} already has an account or an invite.");

	private static IdentityError Described(string code, string description) => new() { Code = code, Description = description };
}
