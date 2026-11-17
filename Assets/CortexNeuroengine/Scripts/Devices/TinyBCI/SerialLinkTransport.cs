// Byte transports for bci_link.
//
// Requires Player Settings > Other Settings > Api Compatibility Level = .NET Framework
// (System.IO.Ports is not in .NET Standard 2.1). Windows and macOS.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace TinyBCI.Link
{
    public interface ILinkTransport : IDisposable
    {
        string Name { get; }

        /// Blocks for at most the read timeout. Returns 0 on timeout.
        /// Throws IOException (or InvalidOperationException) when the link is lost.
        int Read(byte[] buffer, int offset, int count);

        void Write(byte[] data);
    }

    public sealed class SerialLinkTransport : ILinkTransport
    {
        // Never 1200: on the engine, 1200 baud means "reboot into BOOTSEL".
        // Over USB CDC the baud rate is otherwise ignored.
        const int BaudRate = 115200;

        readonly SerialPort port;

        public string Name { get; private set; }

        /// assertDtr: the engine only sends to ports whose DTR is set
        /// (TinyUSB's "connected"). Only tests on pseudo-terminals turn it off.
        public SerialLinkTransport(string portName, int readTimeoutMs = 50, bool assertDtr = true)
        {
            Name = portName;
            port = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = readTimeoutMs,
                WriteTimeout = 500,
                DtrEnable = assertDtr,
                RtsEnable = assertDtr,
            };
            port.Open();   // throws if missing or held by another program (e.g. the collector)
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            // Mono doesn't implement DataReceived or a reliable BytesToRead:
            // a blocking read with a timeout is the portable way.
            try { return port.Read(buffer, offset, count); }
            catch (TimeoutException) { return 0; }
        }

        public void Write(byte[] data) { port.Write(data, 0, data.Length); }

        public void Dispose()
        {
            try { if (port.IsOpen) port.Close(); } catch (Exception) { }
            port.Dispose();
        }

        /// Serial ports worth offering, most likely first.
        /// macOS: /dev/cu.* (Mono's GetPortNames() lists only /dev/tty.*, which
        /// block on open waiting for carrier). Windows: COMx in numeric order.
        public static List<string> ListPorts()
        {
            var ports = new List<string>();

            if (Directory.Exists("/dev"))   // macOS and Linux
            {
                try
                {
                    ports.AddRange(Directory.GetFiles("/dev", "cu.usbmodem*").OrderBy(p => p, StringComparer.Ordinal));
                    ports.AddRange(Directory.GetFiles("/dev", "ttyACM*").OrderBy(p => p, StringComparer.Ordinal));
                    ports.AddRange(Directory.GetFiles("/dev", "cu.*")
                        .Where(p => !p.Contains("usbmodem") && !p.Contains("Bluetooth-Incoming-Port") && !p.Contains("debug-console"))
                        .OrderBy(p => p, StringComparer.Ordinal));
                }
                catch (Exception) { }
                if (ports.Count > 0) return ports;
            }

            ports.AddRange(SerialPort.GetPortNames()
                .Distinct()
                .OrderBy(ComPortNumber)
                .ThenBy(p => p, StringComparer.Ordinal));
            return ports;
        }

        static int ComPortNumber(string name)
        {
            int number;
            return name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.Substring(3), out number) ? number : int.MaxValue;
        }
    }
}