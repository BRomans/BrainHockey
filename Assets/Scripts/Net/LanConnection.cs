using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace BrainHockey.Net
{
    /// <summary>
    /// One TCP connection with length-prefixed messages.
    /// Receiving happens on a background thread; messages are queued and
    /// consumed on Unity's main thread via TryDequeue.
    /// </summary>
    public class LanConnection : IDisposable
    {
        private const int MaxMessageSize = 1 << 20;

        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly Thread receiveThread;
        private readonly ConcurrentQueue<byte[]> inbox = new ConcurrentQueue<byte[]>();
        private readonly object sendLock = new object();
        private volatile bool connected;

        public bool Connected { get { return connected; } }
        public string RemoteAddress { get; private set; }

        public LanConnection(TcpClient tcpClient)
        {
            client = tcpClient;
            client.NoDelay = true; // we send small packets at 30 Hz, don't let Nagle batch them
            stream = client.GetStream();
            RemoteAddress = client.Client.RemoteEndPoint != null ? client.Client.RemoteEndPoint.ToString() : "?";
            connected = true;

            receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "BrainHockey TCP receive" };
            receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            var header = new byte[4];
            try
            {
                while (connected)
                {
                    if (!ReadExact(header, 4)) break;
                    int length = BitConverter.ToInt32(header, 0);
                    if (length <= 0 || length > MaxMessageSize) break;

                    var payload = new byte[length];
                    if (!ReadExact(payload, length)) break;
                    inbox.Enqueue(payload);
                }
            }
            catch (Exception)
            {
                // socket closed or reset — handled below
            }
            connected = false;
        }

        private bool ReadExact(byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int n = stream.Read(buffer, offset, count - offset);
                if (n <= 0) return false;
                offset += n;
            }
            return true;
        }

        public bool TryDequeue(out byte[] message)
        {
            return inbox.TryDequeue(out message);
        }

        public void Send(byte[] payload)
        {
            if (!connected) return;
            var frame = new byte[payload.Length + 4];
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, frame, 0, 4);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            try
            {
                lock (sendLock)
                {
                    stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception)
            {
                connected = false;
            }
        }

        public void Dispose()
        {
            connected = false;
            try { stream.Close(); } catch (Exception) { }
            try { client.Close(); } catch (Exception) { }
        }
    }
}
