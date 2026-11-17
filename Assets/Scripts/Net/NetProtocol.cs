using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BrainHockey.Net
{
    public enum MsgType : byte
    {
        Hello = 1,          // client -> host : version, player name
        Welcome = 2,        // host -> client : host name
        ClientInput = 3,    // client -> host : paddle y, anchor
        ClaimPowerUp = 4,   // client -> host : target id
        StartRequest = 5,   // client -> host : SPACE pressed on the client
        Snapshot = 10,      // host -> client : full game state
        PowerUpEvent = 11,  // host -> client : a power-up was triggered (type, owner)
        RoundEvent = 12,    // host -> client : round started / ended
    }

    public enum PowerUpType : byte
    {
        FasterInOpponentField = 0,
        SlowerInOwnField = 1,
        BigPaddle = 2,
        ShrinkOpponentPaddle = 3,
        MultiBall = 4,
    }

    public struct BallState
    {
        public int id;
        public Vector2 pos;
        public Vector2 vel;
    }

    public struct EffectState
    {
        public PowerUpType type;
        public int owner;        // 1 = left/blue (host), 2 = right/red (client)
        public float remaining;  // seconds, < 0 = "until next goal"
    }

    /// <summary>
    /// Complete game state sent by the host ~30x per second.
    /// Same world coordinates on both machines (no mirroring). Left = host (blue), right = client (red).
    /// </summary>
    public class Snapshot
    {
        public uint tick;
        public bool isPlaying;
        public bool gameOver;
        public int leftScore;
        public int rightScore;
        public float leftPaddleY;
        public float leftPaddleScale = 1f;
        public float rightPaddleScale = 1f;
        public bool targetActive;
        public int targetId;
        public Vector2 targetPos;
        public readonly List<BallState> balls = new List<BallState>();
        public readonly List<EffectState> effects = new List<EffectState>();

        public void Clear()
        {
            balls.Clear();
            effects.Clear();
            targetActive = false;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(tick);
            w.Write(isPlaying);
            w.Write(gameOver);
            w.Write(leftScore);
            w.Write(rightScore);
            w.Write(leftPaddleY);
            w.Write(leftPaddleScale);
            w.Write(rightPaddleScale);
            w.Write(targetActive);
            w.Write(targetId);
            NetProtocol.WriteVec2(w, targetPos);

            w.Write((byte)balls.Count);
            foreach (var b in balls)
            {
                w.Write(b.id);
                NetProtocol.WriteVec2(w, b.pos);
                NetProtocol.WriteVec2(w, b.vel);
            }

            w.Write((byte)effects.Count);
            foreach (var e in effects)
            {
                w.Write((byte)e.type);
                w.Write((byte)e.owner);
                w.Write(e.remaining);
            }
        }

        public static Snapshot Read(BinaryReader r)
        {
            var s = new Snapshot();
            s.tick = r.ReadUInt32();
            s.isPlaying = r.ReadBoolean();
            s.gameOver = r.ReadBoolean();
            s.leftScore = r.ReadInt32();
            s.rightScore = r.ReadInt32();
            s.leftPaddleY = r.ReadSingle();
            s.leftPaddleScale = r.ReadSingle();
            s.rightPaddleScale = r.ReadSingle();
            s.targetActive = r.ReadBoolean();
            s.targetId = r.ReadInt32();
            s.targetPos = NetProtocol.ReadVec2(r);

            int nBalls = r.ReadByte();
            for (int i = 0; i < nBalls; i++)
            {
                s.balls.Add(new BallState
                {
                    id = r.ReadInt32(),
                    pos = NetProtocol.ReadVec2(r),
                    vel = NetProtocol.ReadVec2(r),
                });
            }

            int nEffects = r.ReadByte();
            for (int i = 0; i < nEffects; i++)
            {
                s.effects.Add(new EffectState
                {
                    type = (PowerUpType)r.ReadByte(),
                    owner = r.ReadByte(),
                    remaining = r.ReadSingle(),
                });
            }
            return s;
        }
    }

    public static class NetProtocol
    {
        public const int Version = 2;
        public const string DiscoveryMagic = "BRAINHOCKEY";
        public const int DefaultDiscoveryPort = 47777;
        public const int DefaultGamePort = 47778;

        public static void WriteVec2(BinaryWriter w, Vector2 v)
        {
            w.Write(v.x);
            w.Write(v.y);
        }

        public static Vector2 ReadVec2(BinaryReader r)
        {
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            return new Vector2(x, y);
        }

        /// <summary>Builds a message: [type byte][body...]. Length framing is added by LanConnection.</summary>
        public static byte[] Build(MsgType type, Action<BinaryWriter> body = null)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)type);
                if (body != null) body(w);
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
