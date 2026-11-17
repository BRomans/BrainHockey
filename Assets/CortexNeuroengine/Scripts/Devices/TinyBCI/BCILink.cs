// bci_link protocol: C# port of include/link/bci_link.h and src/link/bci_link.c.
// Plain C#, no Unity dependencies. Keep in step with the C header: the byte
// offsets in the comments below ARE the wire layout.
//
//   [A5 5A] [type] [seq] [payload: 48 bytes] [crc16 LE] [0D 0A]   = 56 bytes
//
// CRC16-CCITT (poly 0x1021, init 0xFFFF) over type, seq and payload.
// Multi-byte fields little-endian, floats IEEE-754 binary32.

using System;

namespace TinyBCI.Link
{
    public enum BciLinkType : byte
    {
        // client -> engine
        Ack = 0x01,
        Trigger = 0x02,
        Ping = 0x03,

        // engine -> clients
        Status = 0x81,
        Eeg = 0x82,
        Inference = 0x83,
        TriggerEcho = 0x84,
        Log = 0x85,
        Pong = 0x86,
    }

    public enum BciLinkRole : byte { Presenter = 1, Collector = 2 }

    public enum BciEngineState : byte
    {
        Booting = 0,
        ConnectingHeadset,
        Stabilizing,
        Ready,
        Fault,
    }

    public struct BciLinkMessage
    {
        public BciLinkType Type;
        public byte Sequence;
        public byte[] Payload;   // BciLink.PayloadSize bytes
    }

    public struct BciLinkStatus
    {
        public BciEngineState State;   // 0
        public byte ClientCount;       // 1
        public uint SamplesReceived;   // 2
        public uint FramesDropped;     // 6
        public uint MaxTickUs;         // 10
        public ulong EngineTimeUs;     // 14
        public int LinkId;             // 22  -1 if the firmware doesn't report it
    }

    public struct BciLinkInference
    {
        public ulong TimestampUs;      // 0
        public uint DeltaTimeUs;       // 8
        public ushort TrueLabel;       // 12  1-based
        public ushort PredictedLabel;  // 14  1-based
        public ushort RoundNumber;     // 16
        public byte ClassCount;        // 18
        public float Confidence;       // 20
        public float[] Probabilities;  // 24
    }

    public static class BciLink
    {
        public const ushort ProtocolVersion = 1;
        public const int PayloadSize = 48;
        public const int FrameSize = 4 + PayloadSize + 4;   // 56
        public const int MaxClasses = 6;

        public const byte StartByte0 = 0xA5, StartByte1 = 0x5A;
        public const byte StopByte0 = 0x0D, StopByte1 = 0x0A;

        // STATUS byte 22: bit 7 set = valid, bits 0-6 = link number
        // (0 = USB Link A, 1 = USB Link B, 2+ = TCP clients)
        public const int StatusLinkIdOffset = 22;
        public const byte LinkIdValid = 0x80;
        public const int LinkA = 0, LinkB = 1;

        const int HeaderSize = 4;
        const int CrcOffset = HeaderSize + PayloadSize;   // 52

        // --- framing

        public static byte[] Encode(BciLinkType type, byte sequence, byte[] payload)
        {
            var frame = new byte[FrameSize];
            frame[0] = StartByte0;
            frame[1] = StartByte1;
            frame[2] = (byte)type;
            frame[3] = sequence;
            if (payload != null) Buffer.BlockCopy(payload, 0, frame, HeaderSize, Math.Min(payload.Length, PayloadSize));
            PutU16(frame, CrcOffset, Crc16(frame, 2, CrcOffset - 2));
            frame[CrcOffset + 2] = StopByte0;
            frame[CrcOffset + 3] = StopByte1;
            return frame;
        }

        public static bool TryDecode(byte[] frame, int offset, out BciLinkMessage message)
        {
            message = default(BciLinkMessage);
            if (frame == null || frame.Length - offset < FrameSize) return false;
            if (frame[offset] != StartByte0 || frame[offset + 1] != StartByte1) return false;
            if (frame[offset + FrameSize - 2] != StopByte0 || frame[offset + FrameSize - 1] != StopByte1) return false;
            if (GetU16(frame, offset + CrcOffset) != Crc16(frame, offset + 2, CrcOffset - 2)) return false;

            var payload = new byte[PayloadSize];
            Buffer.BlockCopy(frame, offset + HeaderSize, payload, 0, PayloadSize);
            message = new BciLinkMessage
            {
                Type = (BciLinkType)frame[offset + 2],
                Sequence = frame[offset + 3],
                Payload = payload,
            };
            return true;
        }

        // --- builders (client -> engine)

        public static byte[] AckPayload(BciLinkRole role)
        {
            var p = new byte[PayloadSize];
            PutU16(p, 0, ProtocolVersion);
            p[2] = (byte)role;
            return p;
        }

        public static byte[] TriggerPayload(ushort code)
        {
            var p = new byte[PayloadSize];
            PutU16(p, 0, code);
            return p;
        }

        // --- readers (engine -> client)

        public static BciLinkStatus ReadStatus(byte[] p)
        {
            byte linkId = p[StatusLinkIdOffset];
            return new BciLinkStatus
            {
                State = (BciEngineState)p[0],
                ClientCount = p[1],
                SamplesReceived = GetU32(p, 2),
                FramesDropped = GetU32(p, 6),
                MaxTickUs = GetU32(p, 10),
                EngineTimeUs = GetU64(p, 14),
                LinkId = (linkId & LinkIdValid) != 0 ? linkId & 0x7F : -1,
            };
        }

        public static BciLinkInference ReadInference(byte[] p)
        {
            byte count = Math.Min(p[18], (byte)MaxClasses);
            var result = new BciLinkInference
            {
                TimestampUs = GetU64(p, 0),
                DeltaTimeUs = GetU32(p, 8),
                TrueLabel = GetU16(p, 12),
                PredictedLabel = GetU16(p, 14),
                RoundNumber = GetU16(p, 16),
                ClassCount = count,
                Confidence = GetF32(p, 20),
                Probabilities = new float[count],
            };
            for (int i = 0; i < count; i++) result.Probabilities[i] = GetF32(p, 24 + 4 * i);
            return result;
        }

        public static string ReadLog(byte[] p)
        {
            int length = Math.Min(p[0], (byte)(PayloadSize - 1));
            return System.Text.Encoding.ASCII.GetString(p, 1, length);
        }

        public static string StateName(BciEngineState state)
        {
            switch (state)
            {
                case BciEngineState.Booting: return "booting";
                case BciEngineState.ConnectingHeadset: return "connecting to headset";
                case BciEngineState.Stabilizing: return "filters settling";
                case BciEngineState.Ready: return "ready";
                case BciEngineState.Fault: return "FAULT (see its debug port)";
                default: return "unknown state";
            }
        }

        // --- little-endian helpers

        public static void PutU16(byte[] b, int o, ushort v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        public static ushort GetU16(byte[] b, int o) { return (ushort)(b[o] | (b[o + 1] << 8)); }
        public static uint GetU32(byte[] b, int o)
        {
            return (uint)b[o] | ((uint)b[o + 1] << 8) | ((uint)b[o + 2] << 16) | ((uint)b[o + 3] << 24);
        }
        public static ulong GetU64(byte[] b, int o) { return GetU32(b, o) | ((ulong)GetU32(b, o + 4) << 32); }

        public static float GetF32(byte[] b, int o)
        {
            if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(b, o);
            var tmp = new[] { b[o + 3], b[o + 2], b[o + 1], b[o] };
            return BitConverter.ToSingle(tmp, 0);
        }

        static ushort Crc16(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= (ushort)(data[offset + i] << 8);
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }
    }

    /// Stream parser: feed bytes one at a time; resyncs after corruption.
    /// Same rules as the C parser.
    public sealed class BciLinkParser
    {
        readonly byte[] buffer = new byte[BciLink.FrameSize];
        int count;

        public int RejectedFrames { get; private set; }

        public void Reset() { count = 0; RejectedFrames = 0; }

        public bool Push(byte value, out BciLinkMessage message)
        {
            message = default(BciLinkMessage);
            buffer[count++] = value;

            while (count > 0)
            {
                if (buffer[0] != BciLink.StartByte0) { Resynchronize(); continue; }
                if (count >= 2 && buffer[1] != BciLink.StartByte1) { Resynchronize(); continue; }
                if (count < BciLink.FrameSize) return false;

                if (BciLink.TryDecode(buffer, 0, out message))
                {
                    count = 0;
                    return true;
                }
                RejectedFrames++;
                Resynchronize();
            }
            return false;
        }

        // Drop the first byte and slide to the next possible start byte.
        void Resynchronize()
        {
            int next = 1;
            while (next < count && buffer[next] != BciLink.StartByte0) next++;
            count -= next;
            Buffer.BlockCopy(buffer, next, buffer, 0, count);
        }
    }
}