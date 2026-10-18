using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Anthro.Sim.Core;
using Anthro.Sim.Discovery;

namespace Anthro.Sim.Api
{
    public sealed class SaveHeader
    {
        public int Version { get; set; }
        public ulong Seed { get; set; }
        public int InitialBandSize { get; set; }
        public int InitialKinCamps { get; set; }
        public int InitialOtherBands { get; set; }
        public GodPolicy Policy { get; set; }
        public long Tick { get; set; }
        public int Year { get; set; }
        public Season Season { get; set; }
        public int DayOfSeason { get; set; }
        public int Population { get; set; }
        public double Favor { get; set; }
        public double GodHelp { get; set; }
        public double GodHarm { get; set; }
        public bool IsPaused { get; set; }
        public int HoursPerTick { get; set; }
        public int CommandCount { get; set; }
    }

    /// <summary>
    /// Phase 3: Deterministic Save / Load serializer for <see cref="GameSession"/>.
    /// Stores the world seed, creation parameters, save-slot preview metadata, and the exact
    /// tick-indexed <see cref="GodCommandRecord"/> log, reconstructing the exact runtime state
    /// across all subsystems (RNG streams, active hunts, mental maps, deeds, beliefs).
    /// </summary>
    public static class GameSave
    {
        const uint Magic = 0x48544E41; // "ANTH"
        const int CurrentVersion = 1;

        public static byte[] SaveToBytes(GameSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(Magic);
            bw.Write(CurrentVersion);
            bw.Write(session.Seed);
            bw.Write(session.InitialBandSize);
            bw.Write(session.InitialKinCamps);
            bw.Write(session.InitialOtherBands);
            bw.Write((int)session.Region.Policy);

            bw.Write(session.Region.Clock.Tick);
            bw.Write(session.Region.Clock.Year);
            bw.Write((int)session.Region.Clock.Season);
            bw.Write(session.Region.Clock.DayOfSeason);
            bw.Write(session.Region.People.Count(p => p.Alive));
            bw.Write(session.Region.Favor);
            bw.Write(session.Region.GodHelp);
            bw.Write(session.Region.GodHarm);
            bw.Write(session.IsPaused);
            bw.Write(session.HoursPerTick);

            var cmds = session.CommandLog;
            bw.Write(cmds.Count);
            for (int i = 0; i < cmds.Count; i++)
            {
                var c = cmds[i];
                bw.Write(c.Tick);
                bw.Write((byte)c.Kind);
                bw.Write(c.X);
                bw.Write(c.Y);
                bw.Write(c.TargetPersonId);
                bw.Write(c.BoolArg);
                bw.Write(c.IntArg);
                bw.Write(c.DoubleArg);
                bw.Write(c.TextArg ?? "");
            }

            bw.Flush();
            return ms.ToArray();
        }

        public static SaveHeader ReadHeader(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
            return ReadHeaderInternal(br);
        }

        static SaveHeader ReadHeaderInternal(BinaryReader br)
        {
            uint magic = br.ReadUInt32();
            if (magic != Magic)
                throw new InvalidDataException("Invalid Anthro save file header.");

            int version = br.ReadInt32();
            if (version != CurrentVersion)
                throw new InvalidDataException($"Unsupported Anthro save version: {version}.");

            return new SaveHeader
            {
                Version = version,
                Seed = br.ReadUInt64(),
                InitialBandSize = br.ReadInt32(),
                InitialKinCamps = br.ReadInt32(),
                InitialOtherBands = br.ReadInt32(),
                Policy = (GodPolicy)br.ReadInt32(),
                Tick = br.ReadInt64(),
                Year = br.ReadInt32(),
                Season = (Season)br.ReadInt32(),
                DayOfSeason = br.ReadInt32(),
                Population = br.ReadInt32(),
                Favor = br.ReadDouble(),
                GodHelp = br.ReadDouble(),
                GodHarm = br.ReadDouble(),
                IsPaused = br.ReadBoolean(),
                HoursPerTick = br.ReadInt32(),
                CommandCount = br.ReadInt32()
            };
        }

        public static GameSession LoadFromBytes(byte[] data, DiscoveryWeb? web = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var header = ReadHeaderInternal(br);
            var commands = new List<GodCommandRecord>(header.CommandCount);
            for (int i = 0; i < header.CommandCount; i++)
            {
                commands.Add(new GodCommandRecord
                {
                    Tick = br.ReadInt64(),
                    Kind = (GodCommandKind)br.ReadByte(),
                    X = br.ReadInt32(),
                    Y = br.ReadInt32(),
                    TargetPersonId = br.ReadInt32(),
                    BoolArg = br.ReadBoolean(),
                    IntArg = br.ReadInt32(),
                    DoubleArg = br.ReadDouble(),
                    TextArg = br.ReadString()
                });
            }

            var session = new GameSession(
                header.Seed,
                web,
                header.InitialBandSize,
                header.InitialKinCamps,
                header.InitialOtherBands,
                header.Policy);

            int cmdIndex = 0;
            while (session.Region.Clock.Tick < header.Tick)
            {
                long curTick = session.Region.Clock.Tick;
                while (cmdIndex < commands.Count && commands[cmdIndex].Tick <= curTick)
                {
                    session.RecordAndApply(commands[cmdIndex]);
                    cmdIndex++;
                }
                session.StepHours(1, ignorePause: true);
            }

            // Enqueue any commands scheduled at the current tick that haven't executed yet
            while (cmdIndex < commands.Count && commands[cmdIndex].Tick <= session.Region.Clock.Tick)
            {
                session.RecordAndApply(commands[cmdIndex]);
                cmdIndex++;
            }

            session.DrainNotifications(); // Clear historical notifications from replay
            session.IsPaused = header.IsPaused;
            session.HoursPerTick = header.HoursPerTick;
            return session;
        }
    }
}
