using System.Net;
using System.Net.Sockets;
using Domain.Identity;

namespace API.Notifications.Webhook;

internal static class WebhookConnect
{
	public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, UserRole owner, CancellationToken cancellationToken)
	{
		var host = context.DnsEndPoint.Host;
		var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
		var target = Array.Find(addresses, address => MayReach(address, owner)) ?? throw new WebhookBlockedException(Refusal(host, owner));

		var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
		try
		{
			await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), cancellationToken);
			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	internal static bool MayReach(IPAddress address, UserRole owner)
	{
		return owner switch
		{
			UserRole.Admin => !IsLinkLocal(address),
			UserRole.Member => !IsBlocked(address),
		};
	}

	private static bool IsLinkLocal(IPAddress address)
	{
		return address.IsIPv4MappedToIPv6 ? IsLinkLocal(address.MapToIPv4()) : address.IsIPv6LinkLocal || address.GetAddressBytes() is [169, 254, _, _];
	}

	private static string Refusal(string host, UserRole owner)
	{
		return owner switch
		{
			UserRole.Admin => $"The webhook host '{host}' resolves only to link-local addresses, which no webhook may reach.",
			UserRole.Member => $"The webhook host '{host}' resolves only to private addresses, which only an admin's webhook may reach.",
		};
	}

	internal static bool IsBlocked(IPAddress address)
	{
		if (address.IsIPv4MappedToIPv6)
		{
			address = address.MapToIPv4();
		}

		if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
		{
			return true;
		}

		var bytes = address.GetAddressBytes();
		if (address.AddressFamily == AddressFamily.InterNetwork)
		{
			return bytes[0] switch
			{
				0 => true,
				10 => true,
				127 => true,
				100 when bytes[1] >= 64 && bytes[1] <= 127 => true,
				169 when bytes[1] == 254 => true,
				172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
				192 when bytes[1] == 168 => true,
				_ => false,
			};
		}

		if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
		{
			return true;
		}
		if ((bytes[0] & 0xFE) == 0xFC)
		{
			return true;
		}
		if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B)
		{
			return IsBlocked(new IPAddress(bytes[12..16]));
		}
		if (bytes.Take(12).All(octet => octet == 0))
		{
			return IsBlocked(new IPAddress(bytes[12..16]));
		}
		return false;
	}
}
