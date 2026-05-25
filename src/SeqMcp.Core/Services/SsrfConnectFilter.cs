using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SeqMcp.Core.Services;

/// <summary>
/// <see cref="SocketsHttpHandler.ConnectCallback"/> that blocks outbound
/// connections to loopback / link-local (always) and RFC1918 private
/// ranges (when <c>blockPrivateHosts</c> is on).
///
/// Per-connection DNS resolution is the enforcement mechanism: a pre-check
/// performed once would be vulnerable to DNS rebinding because
/// <see cref="SocketsHttpHandler"/> re-resolves on every connect.
/// Currently used only for <see cref="Configuration.TrustMode.HeaderOverride"/>
/// endpoints — PR-3 has no caller producing those; the wiring exists for PR-5.
/// </summary>
internal sealed class SsrfConnectFilter
{
    private readonly bool _blockPrivateHosts;
    private readonly ILogger _logger;

    public SsrfConnectFilter(bool blockPrivateHosts, ILogger logger)
    {
        _blockPrivateHosts = blockPrivateHosts;
        _logger = logger;
    }

    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        var addresses = await ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        foreach (var ip in addresses)
        {
            if (IsForbidden(ip, _blockPrivateHosts))
            {
                _logger.LogWarning(
                    "SSRF guard: rejecting connection to {Host}:{Port} — resolved to forbidden IP {Ip}",
                    host, port, ip);
                throw new SocketException((int)SocketError.AccessDenied);
            }
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses[0], port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return new[] { literal };
        }
        return await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Pure decision function — exposed as <c>internal</c> so the test
    /// suite can exercise every range without a real TCP connect.
    /// </summary>
    internal static bool IsForbidden(IPAddress ip, bool blockPrivateHosts)
    {
        if (IPAddress.IsLoopback(ip)) return true;          // 127.0.0.0/8, ::1
        if (ip.IsIPv6LinkLocal) return true;                 // fe80::/10
        if (IsIPv4LinkLocal(ip)) return true;                // 169.254.0.0/16
        if (blockPrivateHosts && IsRfc1918(ip)) return true;
        return false;
    }

    private static bool IsIPv4LinkLocal(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = ip.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool IsRfc1918(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = ip.GetAddressBytes();
        if (bytes[0] == 10) return true;
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
        if (bytes[0] == 192 && bytes[1] == 168) return true;
        return false;
    }
}
