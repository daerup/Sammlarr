using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace E2E.Tests.PlexFree;

internal sealed record TestCertificates(string AuthorityPem, string KeystorePath, string Password)
{
	private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

	internal static async Task<TestCertificates> CreateAsync(IEnumerable<string> hosts, string directory)
	{
		var now = DateTimeOffset.UtcNow;
		using var authorityKey = RSA.Create(2048);
		var authorityRequest = new CertificateRequest("CN=Sammlarr Test CA", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
		authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
		using var authority = authorityRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(7));

		using var serverKey = RSA.Create(2048);
		var serverRequest = new CertificateRequest("CN=Sammlarr test metadata", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		var names = new SubjectAlternativeNameBuilder();
		foreach (var host in hosts)
		{
			names.AddDnsName(host);
		}

		serverRequest.CertificateExtensions.Add(names.Build());
		serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthentication)], false));
		using var server = serverRequest.Create(authority, now.AddDays(-1), now.AddDays(6), RandomNumberGenerator.GetBytes(16));
		using var serverWithKey = server.CopyWithPrivateKey(serverKey);

		var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
		var keystore = Path.Combine(directory, "metadata.p12");
		await File.WriteAllBytesAsync(keystore, serverWithKey.Export(X509ContentType.Pkcs12, password));
		return new TestCertificates(authority.ExportCertificatePem(), keystore, password);
	}
}
