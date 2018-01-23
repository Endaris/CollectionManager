﻿using System;
using CollectionManager.Enums;
using CollectionManager.Interfaces;
using CollectionManager.Modules.FileIO;
using CollectionManager.Modules.FileIO.OsuScoresDb;

namespace CollectionManager.DataTypes
{
    public class Replay : IReplay, ICloneable
    {
        public PlayMode PlayMode { get; set; }
        public int Version { get; set; }
        public string MapHash { get; set; }
        public string PlayerName { get; set; }
        public string ReplayHash { get; set; }
        public short C300 { get; set; }
        public short C100 { get; set; }
        public short C50 { get; set; }
        public short Geki { get; set; }
        public short Katu { get; set; }
        public short Miss { get; set; }
        public int TotalScore { get; set; }
        public short MaxCombo { get; set; }
        public bool Perfect { get; set; }
        public int Mods { get; set; }
        public virtual string ReplayData { get; set; }
        public DateTime Date { get; set; }
        public long DateTicks { get; set; }
        public int CompressedReplayLength { get; set; }
        public byte[] CompressedReplay { get; set; }
        public long OnlineScoreId { get; set; } = -1;


        public static IReplay Read(OsuBinaryReader reader, IReplay outobj = null, bool minimalLoad = true)
        {
            if (outobj == null)
                outobj = new Replay();
            outobj.PlayMode = (PlayMode)reader.ReadByte();
            outobj.Version = reader.ReadInt32();
            outobj.MapHash = reader.ReadString();
            outobj.PlayerName = reader.ReadString();
            outobj.ReplayHash = reader.ReadString();
            outobj.C300 = reader.ReadInt16();
            outobj.C100 = reader.ReadInt16();
            outobj.C50 = reader.ReadInt16();
            outobj.Geki = reader.ReadInt16();
            outobj.Katu = reader.ReadInt16();
            outobj.Miss = reader.ReadInt16();
            outobj.TotalScore = reader.ReadInt32();
            outobj.MaxCombo = reader.ReadInt16();
            outobj.Perfect = reader.ReadBoolean();
            outobj.Mods = reader.ReadInt32();
            outobj.ReplayData = reader.ReadString();
            outobj.DateTicks = reader.ReadInt64();
            outobj.Date = GetDate(outobj.DateTicks);
            outobj.CompressedReplayLength = reader.ReadInt32();
            if (outobj.CompressedReplayLength > 0)
            {
                if (minimalLoad)
                    reader.ReadBytes(outobj.CompressedReplayLength);
                else
                    outobj.CompressedReplay = reader.ReadBytes(outobj.CompressedReplayLength);
            }
            if (outobj.Version >= 20140721)
                outobj.OnlineScoreId = reader.ReadInt64();
            return outobj;
        }

        public double getAcc()
        {
            if (PlayMode == PlayMode.Osu)
            {
                int objectCount = C300 + C100 + C50 + Miss;
                int maxScore = objectCount * 300;
                int realScore = C300 * 300 + C100 * 100 + C50 * 50;
                return (double)realScore / maxScore;
            }
            else if (PlayMode == PlayMode.Taiko)
            {
                int maxScore = C300 + C100 + Miss;
                double realScore = C300 + 0.5 * C100;
                return realScore/maxScore;
            }
            else if (PlayMode == PlayMode.CatchTheBeat)
            {
                int maxScore = C300 + C100 + C50 + Katu + Miss;
                int realScore = C300 + C100 + C50;
                return (double) realScore/maxScore;
            }
            else if (PlayMode == PlayMode.OsuMania)
            {
                int objectCount = C300 + C100 + C50 + Geki + Katu + Miss;
                int maxScore = objectCount * 300;
                int realScore = 300 * (C300 + Geki) + 200 * Katu + 100 * C100 + 50 * C50;
                return (double)realScore / maxScore;
            }
            else
            {
                return 0;
            }           
        }

        private static DateTime GetDate(long ticks)
        {
            if (ticks < 0L)
            {
                return new DateTime();
            }
            try
            {
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (Exception e)
            {
                return new DateTime();
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

    }
}