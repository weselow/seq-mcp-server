using System.Net;
using FluentAssertions;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

/// <summary>
/// Pure-logic tests for <see cref="SsrfConnectFilter.IsForbidden"/> —
/// the decision the connect-time guard makes once DNS has resolved a
/// host to an IP. Real TCP connect behavior is covered separately by
/// <c>SeqConnectionFactoryTests.TrustedConfig_Handler_Has_No_ConnectCallback</c>
/// and the matching <c>HeaderOverride</c> test (handler shape).
///
/// Coverage:
/// <list type="bullet">
///   <item>Loopback (IPv4 127.x, IPv6 ::1) — always forbidden</item>
///   <item>Link-local (IPv4 169.254/16 incl. AWS IMDS, IPv6 fe80::/10) — always forbidden</item>
///   <item>RFC1918 (10/8, 172.16/12, 192.168/16) — only when <c>blockPrivateHosts</c> is on</item>
///   <item>Public IPs — never forbidden</item>
/// </list>
/// </summary>
public class SeqConnectionFactorySsrfTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.5")]
    [InlineData("127.255.255.255")]
    [InlineData("::1")]
    public void Should_Block_Loopback_Regardless_Of_Private_Flag(string ip)
    {
        // Arrange
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: false).Should().BeTrue();
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("169.254.0.1")]
    [InlineData("169.254.169.254")] // AWS IMDS — the classic SSRF target
    [InlineData("169.254.255.255")]
    public void Should_Block_IPv4_Link_Local_Regardless_Of_Private_Flag(string ip)
    {
        // Arrange
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: false).Should().BeTrue();
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("fe80::1")]
    [InlineData("fe80::abcd:ef01")]
    public void Should_Block_IPv6_Link_Local_Regardless_Of_Private_Flag(string ip)
    {
        // Arrange
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: false).Should().BeTrue();
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")] // example.com
    [InlineData("2606:4700:4700::1111")] // public IPv6 (Cloudflare)
    public void Should_Not_Block_Public_Ips(string ip)
    {
        // Arrange
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: false).Should().BeFalse();
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: true).Should().BeFalse();
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.20.10.5")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.1.254")]
    public void Should_Allow_Rfc1918_When_Private_Flag_Off(string ip)
    {
        // Arrange
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: false).Should().BeFalse();
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.20.10.5")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.1.254")]
    public void Should_Block_Rfc1918_When_Private_Flag_On(string ip)
    {
        // Arrange
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("172.15.255.255")]  // just outside 172.16-31
    [InlineData("172.32.0.0")]      // just outside
    [InlineData("11.0.0.1")]        // not 10/8
    [InlineData("192.167.255.255")] // not 192.168/16
    [InlineData("192.169.0.1")]     // not 192.168/16
    public void Should_Allow_Near_Boundary_Public_Ips_Even_With_Private_Flag(string ip)
    {
        // Arrange — ranges adjacent to RFC1918 but not inside it
        var addr = IPAddress.Parse(ip);

        // Act + Assert
        SsrfConnectFilter.IsForbidden(addr, blockPrivateHosts: true).Should().BeFalse();
    }
}
