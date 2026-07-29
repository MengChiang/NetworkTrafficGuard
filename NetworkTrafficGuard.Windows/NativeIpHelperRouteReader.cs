using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;

namespace NetworkTrafficGuard.Windows;

public sealed class NativeIpHelperRouteReader(ILogger<NativeIpHelperRouteReader> logger) : IRouteReader
{
    private const ushort AfUnspec = 0;
    private const ushort AfInet = 2;
    private const ushort AfInet6 = 23;
    private const uint NoError = 0;

    public Task<IReadOnlyList<DefaultRouteInfo>> GetDefaultRoutesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogDebug("Reading Windows default routes with IP Helper API.");

        var errorCode = GetIpForwardTable2(AfUnspec, out var tablePointer);

        if (errorCode != NoError)
        {
            throw new InvalidOperationException($"GetIpForwardTable2 failed with error code {errorCode}.");
        }

        try
        {
            var routes = ReadRoutes(tablePointer);
            return Task.FromResult<IReadOnlyList<DefaultRouteInfo>>(routes);
        }
        finally
        {
            FreeMibTable(tablePointer);
        }
    }

    private static IReadOnlyList<DefaultRouteInfo> ReadRoutes(IntPtr tablePointer)
    {
        var routeCount = Marshal.ReadInt32(tablePointer);
        var rowSize = Marshal.SizeOf<MibIpForwardRow2>();
        var rowPointer = IntPtr.Add(tablePointer, IntPtr.Size == 8 ? 8 : 4);
        var interfaceNames = CreateInterfaceNameMap();
        var interfaceMetrics = new Dictionary<string, uint>(StringComparer.Ordinal);
        var routes = new List<DefaultRouteInfo>();

        for (var index = 0; index < routeCount; index++)
        {
            var row = Marshal.PtrToStructure<MibIpForwardRow2>(IntPtr.Add(rowPointer, index * rowSize));
            var destinationPrefix = FormatPrefix(row.DestinationPrefix);

            if (!DefaultRouteSelector.IsDefaultRoute(destinationPrefix))
            {
                continue;
            }

            var interfaceIndex = unchecked((int)row.InterfaceIndex);
            var interfaceMetric = GetInterfaceMetric(
                interfaceIndex,
                row.DestinationPrefix.Prefix.Family,
                interfaceMetrics);

            routes.Add(new DefaultRouteInfo(
                destinationPrefix,
                FormatAddress(row.NextHop),
                interfaceIndex,
                interfaceNames.TryGetValue(interfaceIndex, out var interfaceName)
                    ? interfaceName
                    : $"Interface {interfaceIndex}",
                row.Metric,
                interfaceMetric));
        }

        return routes;
    }

    private static uint GetInterfaceMetric(
        int interfaceIndex,
        ushort family,
        Dictionary<string, uint> cache)
    {
        var cacheKey = $"{family}|{interfaceIndex}";

        if (cache.TryGetValue(cacheKey, out var cachedMetric))
        {
            return cachedMetric;
        }

        var row = new MibIpInterfaceRow
        {
            Family = family,
            InterfaceIndex = unchecked((uint)interfaceIndex)
        };

        var errorCode = GetIpInterfaceEntry(ref row);
        var metric = errorCode == NoError
            ? row.Metric
            : 0u;

        cache[cacheKey] = metric;
        return metric;
    }

    private static Dictionary<int, string> CreateInterfaceNameMap()
    {
        var results = new Dictionary<int, string>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var properties = networkInterface.GetIPProperties();
            var ipv4Index = TryGetIPv4Index(properties);
            var ipv6Index = TryGetIPv6Index(properties);

            if (ipv4Index is { } v4)
            {
                results[v4] = networkInterface.Name;
            }

            if (ipv6Index is { } v6)
            {
                results[v6] = networkInterface.Name;
            }
        }

        return results;
    }

    private static int? TryGetIPv4Index(IPInterfaceProperties properties)
    {
        try
        {
            return properties.GetIPv4Properties()?.Index;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static int? TryGetIPv6Index(IPInterfaceProperties properties)
    {
        try
        {
            return properties.GetIPv6Properties()?.Index;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static string FormatPrefix(IpAddressPrefix prefix)
    {
        return $"{FormatAddress(prefix.Prefix)}/{prefix.PrefixLength}";
    }

    private static string FormatAddress(SockaddrInet address)
    {
        return address.Family switch
        {
            AfInet => new IPAddress(BitConverter.GetBytes(address.Ipv4.Address)).ToString(),
            AfInet6 => new IPAddress(address.Ipv6.GetAddressBytes(), address.Ipv6.ScopeId).ToString(),
            _ => string.Empty
        };
    }

    [DllImport("Iphlpapi.dll")]
    private static extern uint GetIpForwardTable2(ushort family, out IntPtr table);

    [DllImport("Iphlpapi.dll")]
    private static extern uint GetIpInterfaceEntry(ref MibIpInterfaceRow row);

    [DllImport("Iphlpapi.dll")]
    private static extern void FreeMibTable(IntPtr memory);

    [StructLayout(LayoutKind.Explicit, Size = 104)]
    private struct MibIpForwardRow2
    {
        [FieldOffset(0)]
        public ulong InterfaceLuid;

        [FieldOffset(8)]
        public uint InterfaceIndex;

        [FieldOffset(12)]
        public IpAddressPrefix DestinationPrefix;

        [FieldOffset(44)]
        public SockaddrInet NextHop;

        [FieldOffset(72)]
        public byte SitePrefixLength;

        [FieldOffset(76)]
        public uint ValidLifetime;

        [FieldOffset(80)]
        public uint PreferredLifetime;

        [FieldOffset(84)]
        public uint Metric;

        [FieldOffset(88)]
        public int Protocol;

        [FieldOffset(92)]
        public byte Loopback;

        [FieldOffset(93)]
        public byte AutoconfigureAddress;

        [FieldOffset(94)]
        public byte Publish;

        [FieldOffset(95)]
        public byte Immortal;

        [FieldOffset(96)]
        public uint Age;

        [FieldOffset(100)]
        public int Origin;
    }

    [StructLayout(LayoutKind.Explicit, Size = 168)]
    private struct MibIpInterfaceRow
    {
        [FieldOffset(0)]
        public ushort Family;

        [FieldOffset(16)]
        public uint InterfaceIndex;

        [FieldOffset(148)]
        public uint Metric;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct IpAddressPrefix
    {
        [FieldOffset(0)]
        public SockaddrInet Prefix;

        [FieldOffset(28)]
        public byte PrefixLength;
    }

    [StructLayout(LayoutKind.Explicit, Size = 28)]
    private struct SockaddrInet
    {
        [FieldOffset(0)]
        public ushort Family;

        [FieldOffset(0)]
        public SockaddrIn Ipv4;

        [FieldOffset(0)]
        public SockaddrIn6 Ipv6;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    private struct SockaddrIn
    {
        public ushort Family;
        public ushort Port;
        public uint Address;
        public byte Zero0;
        public byte Zero1;
        public byte Zero2;
        public byte Zero3;
        public byte Zero4;
        public byte Zero5;
        public byte Zero6;
        public byte Zero7;
    }

    [StructLayout(LayoutKind.Sequential, Size = 28)]
    private struct SockaddrIn6
    {
        public ushort Family;
        public ushort Port;
        public uint FlowInfo;
        public byte Address0;
        public byte Address1;
        public byte Address2;
        public byte Address3;
        public byte Address4;
        public byte Address5;
        public byte Address6;
        public byte Address7;
        public byte Address8;
        public byte Address9;
        public byte Address10;
        public byte Address11;
        public byte Address12;
        public byte Address13;
        public byte Address14;
        public byte Address15;
        public uint ScopeId;

        public readonly byte[] GetAddressBytes()
        {
            return
            [
                Address0,
                Address1,
                Address2,
                Address3,
                Address4,
                Address5,
                Address6,
                Address7,
                Address8,
                Address9,
                Address10,
                Address11,
                Address12,
                Address13,
                Address14,
                Address15
            ];
        }
    }
}
