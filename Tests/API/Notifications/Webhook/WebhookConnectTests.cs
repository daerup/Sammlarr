using System.Net;
using API.Notifications.Webhook;
using Domain.Identity;

namespace API.Tests.Notifications.Webhook;

public class WebhookConnectTests
{
	[Test]
	[Arguments("127.0.0.1", true)]
	[Arguments("::1", true)]
	[Arguments("10.0.0.5", true)]
	[Arguments("172.16.0.1", true)]
	[Arguments("172.31.255.255", true)]
	[Arguments("192.168.1.1", true)]
	[Arguments("169.254.169.254", true)]
	[Arguments("0.0.0.0", true)]
	[Arguments("::ffff:127.0.0.1", true)]
	[Arguments("::ffff:10.0.0.1", true)]
	[Arguments("fe80::1", true)]
	[Arguments("fc00::1", true)]
	[Arguments("64:ff9b::7f00:1", true)]
	[Arguments("::7f00:1", true)]
	[Arguments("100.64.0.1", true)]
	[Arguments("100.127.255.255", true)]
	[Arguments("100.63.255.255", false)]
	[Arguments("100.128.0.1", false)]
	[Arguments("8.8.8.8", false)]
	[Arguments("172.15.0.1", false)]
	[Arguments("172.32.0.1", false)]
	[Arguments("2606:4700:4700::1111", false)]
	public async Task IsBlocked_ForAnAddress_ClassifiesIt(string address, bool blocked)
	{
		await Assert.That(WebhookConnect.IsBlocked(IPAddress.Parse(address))).IsEqualTo(blocked);
	}

	[Test]
	[Arguments("172.18.0.7", UserRole.Admin, true)]
	[Arguments("172.18.0.7", UserRole.Member, false)]
	[Arguments("127.0.0.1", UserRole.Admin, true)]
	[Arguments("169.254.169.254", UserRole.Admin, false)]
	[Arguments("::ffff:169.254.169.254", UserRole.Admin, false)]
	[Arguments("fe80::1", UserRole.Admin, false)]
	[Arguments("8.8.8.8", UserRole.Member, true)]
	public async Task MayReach_ForTheOwnersRole_LetsAnAdminReachAnythingButLinkLocalAndAMemberOnlyPublicAddresses(string address, UserRole owner, bool reached)
	{
		await Assert.That(WebhookConnect.MayReach(IPAddress.Parse(address), owner)).IsEqualTo(reached);
	}
}
