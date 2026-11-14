using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsNetworkWatchdogService : INetworkWatchdogService
{
    private readonly object _gate = new();
    private readonly List<NetworkDropEvent> _drops = new();
    private string _lastTarget = "Gateway";
    private bool _isOnline = true;
    private long? _lastLatencyMs;
    private int _checkCount;
    private int _dropCount;

    public NetworkWatchdogSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NetworkWatchdogSnapshot(
                _lastTarget,
                _isOnline,
                _lastLatencyMs,
                _checkCount,
                _dropCount,
                _drops.ToArray());
        }
    }

    public async Task<NetworkWatchdogSnapshot> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var gateway = await Task.Run(FindActiveIpv4Gateway, cancellationToken);
        if (gateway is null)
        {
            lock (_gate)
            {
                _checkCount++;
                _lastTarget = "Gateway";
                _isOnline = false;
                _lastLatencyMs = null;
                _dropCount++;
                RecordDropLocked("Gateway", "No active gateway");
            }
            return GetSnapshot();
        }

        var targetText = gateway.ToString();
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(gateway, 1200).WaitAsync(cancellationToken);
            lock (_gate)
            {
                _checkCount++;
                _lastTarget = targetText;
                if (reply.Status == IPStatus.Success)
                {
                    _isOnline = true;
                    _lastLatencyMs = reply.RoundtripTime;
                }
                else
                {
                    _isOnline = false;
                    _lastLatencyMs = null;
                    _dropCount++;
                    RecordDropLocked(targetText, reply.Status.ToString());
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _checkCount++;
                _lastTarget = targetText;
                _isOnline = false;
                _lastLatencyMs = null;
                _dropCount++;
                RecordDropLocked(targetText, ex.GetType().Name);
            }
        }

        return GetSnapshot();
    }

    private void RecordDropLocked(string target, string reason)
    {
        _drops.Insert(0, new NetworkDropEvent(DateTime.Now, target, reason));
        if (_drops.Count > 15)
            _drops.RemoveAt(_drops.Count - 1);
    }

    private static IPAddress? FindActiveIpv4Gateway()
    {
        try
        {
            IPAddress? fallback = null;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var name = $"{nic.Name} {nic.Description}";
                var isVirtual = name.Contains("radmin", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("hamachi", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("zerotier", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("tailscale", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("wireguard", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("openvpn", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("tap-", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("vpn", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("vethernet", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("virtual", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("hyper-v", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("wsl", StringComparison.OrdinalIgnoreCase);

                var props = nic.GetIPProperties();
                foreach (var gw in props.GatewayAddresses)
                {
                    if (gw.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.Any.Equals(gw.Address)) continue;
                    if (!isVirtual)
                        return gw.Address;
                    fallback ??= gw.Address;
                }
            }
            return fallback;
        }
        catch
        {
            // Network interface enumeration may fail transiently
        }
        return null;
    }
}
