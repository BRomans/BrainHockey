using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BrainHockey.Net
{
    /// <summary>
    /// UDP broadcast discovery.
    /// A host broadcasts "BRAINHOCKEY|version|gamePort|sessionId|name" once per second;
    /// everyone listens on the discovery port and queues the hosts they hear.
    /// </summary>
    public class LanDiscovery : IDisposable
    {
        public struct HostInfo
        {
            public IPAddress address;
            public int port;
            public uint sessionId;
            public string name;
        }

        private UdpClient listener;
        private UdpClient sender;
        private Thread listenThread;
        private volatile bool running;
        private readonly ConcurrentQueue<HostInfo> found = new ConcurrentQueue<HostInfo>();
        private List<IPAddress> broadcastTargets;

        public void StartListening(int discoveryPort)
        {
            listener = new UdpClient();
            // ReuseAddress lets two copies of the game run on one PC for testing
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.EnableBroadcast = true;
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));

            running = true;
            listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "BrainHockey discovery" };
            listenThread.Start();
        }

        private void ListenLoop()
        {
            while (running)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = listener.Receive(ref remote);
                    string[] parts = Encoding.UTF8.GetString(data).Split('|');
                    if (parts.Length < 5 || parts[0] != NetProtocol.DiscoveryMagic) continue;

                    int version, port;
                    uint session;
                    if (!int.TryParse(parts[1], out version) || version != NetProtocol.Version) continue;
                    if (!int.TryParse(parts[2], out port)) continue;
                    if (!uint.TryParse(parts[3], out session)) continue;

                    found.Enqueue(new HostInfo
                    {
                        address = remote.Address,
                        port = port,
                        sessionId = session,
                        name = parts[4],
                    });
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception)
                {
                    if (!running) break;
                }
            }
        }

        public bool TryGetHost(out HostInfo host)
        {
            return found.TryDequeue(out host);
        }

        public void ClearFound()
        {
            HostInfo dummy;
            while (found.TryDequeue(out dummy)) { }
        }

        public void Broadcast(int discoveryPort, int gamePort, uint sessionId, string name)
        {
            if (sender == null)
            {
                sender = new UdpClient();
                sender.EnableBroadcast = true;
            }
            if (broadcastTargets == null) broadcastTargets = GetBroadcastAddresses();

            string safeName = (name ?? "").Replace("|", "/");
            byte[] msg = Encoding.UTF8.GetBytes(
                NetProtocol.DiscoveryMagic + "|" + NetProtocol.Version + "|" + gamePort + "|" + sessionId + "|" + safeName);

            foreach (var address in broadcastTargets)
            {
                try { sender.Send(msg, msg.Length, new IPEndPoint(address, discoveryPort)); }
                catch (Exception) { /* some interfaces refuse broadcast, ignore */ }
            }
        }

        /// <summary>
        /// 255.255.255.255 often only goes out of ONE network adapter (Windows picks one),
        /// so we also send to every adapter's subnet broadcast address (e.g. 192.168.1.255)
        /// and to loopback (two instances on the same PC).
        /// </summary>
        private static List<IPAddress> GetBroadcastAddresses()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        try
                        {
                            if (ua.IPv4Mask == null) continue;
                            byte[] ip = ua.Address.GetAddressBytes();
                            byte[] mask = ua.IPv4Mask.GetAddressBytes();
                            if (ip.Length != 4 || mask.Length != 4) continue;
                            var b = new byte[4];
                            for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                            var addr = new IPAddress(b);
                            if (!list.Contains(addr)) list.Add(addr);
                        }
                        catch (Exception) { /* IPv4Mask not implemented on some platforms */ }
                    }
                }
            }
            catch (Exception) { }
            return list;
        }

        /// <summary>Best guess at this machine's LAN IPv4 address, for display.</summary>
        public static string GetLocalIPv4()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                            return ua.Address.ToString();
                    }
                }
            }
            catch (Exception) { }
            return "?";
        }

        public void Dispose()
        {
            running = false;
            try { if (listener != null) listener.Close(); } catch (Exception) { }
            try { if (sender != null) sender.Close(); } catch (Exception) { }
        }
    }
}
