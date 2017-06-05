﻿namespace Heroes.ReplayParser
{
    using System.IO;
    using Streams;
    using System;
    using System.Text;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Linq;

    /// <summary> Parses the replay.server.battlelobby file in the replay file. </summary>
    public class ReplayServerBattlelobby
    {
        public const string FileName = "replay.server.battlelobby";

        /// <summary> Parses the replay.server.battlelobby file in a replay file. </summary>
        /// <param name="replay"> The replay file to apply the parsed data to. </param>
        /// <param name="buffer"> The buffer containing the replay.server.battlelobby file. </param>
        public static void Parse(Replay replay, byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                var bitReader = new BitReader(stream);

                // 52124 and 52381 are non-tested ptr builds
                if (replay.ReplayBuild < 38793 ||
                    replay.ReplayBuild == 52124 || replay.ReplayBuild == 52381 ||
                    replay.GameMode == GameMode.Unknown)
                {
                    GetBattleTags(replay, bitReader);
                    return;
                }

                int s2mArrayLength = bitReader.ReadByte();
                int stringLength = bitReader.ReadByte();

                bitReader.ReadString(stringLength);

                for (var i = 1; i < s2mArrayLength; i++)
                {
                    bitReader.Read(16);
                    bitReader.ReadString(stringLength);
                }

                if (bitReader.ReadByte() != s2mArrayLength)
                    throw new Exception("s2ArrayLength not equal");

                for (var i = 0; i < s2mArrayLength; i++)
                {
                    bitReader.ReadString(4); // s2m
                    bitReader.ReadBytes(2); // 0x00 0x00
                    bitReader.ReadString(2); // Realm
                    bitReader.ReadBytes(32);
                }

                // seems to be in all replays
                bitReader.ReadInt16();
                bitReader.stream.Position = bitReader.stream.Position + 684;

                // seems to be in all replays
                bitReader.ReadInt16();
                bitReader.stream.Position = bitReader.stream.Position + 1944;

                if (bitReader.ReadString(8) != "HumnComp")
                    throw new Exception("Not HumnComp");

                // seems to be in all replays
                bitReader.stream.Position = bitReader.stream.Position = bitReader.stream.Position + 19859;

                // next section is language libraries?
                // ---------------------------------------
                bitReader.Read(8);
                bitReader.Read(8);

                for (int i = 0; ; i++) // no idea how to determine the count
                {
                    if (bitReader.ReadString(4).Substring(0, 2) != "s2") // s2mv; not sure if its going to be 'mv' all the time
                    {
                        bitReader.stream.Position = bitReader.stream.Position - 4;
                        break;
                    }

                    bitReader.ReadBytes(2); // 0x00 0x00
                    bitReader.ReadString(2); // Realm
                    bitReader.ReadBytes(32);
                }

                bitReader.Read(32);
                bitReader.Read(8);

                bitReader.ReadByte();
                for (int i = 0; ; i++) // no idea how to determine the count
                {
                    if (bitReader.ReadString(4).Substring(0, 2) != "s2") // s2ml
                    {
                        bitReader.stream.Position = bitReader.stream.Position - 4;
                        break;
                    }

                    bitReader.ReadBytes(2); // 0x00 0x00
                    bitReader.ReadString(2); // Realm
                    bitReader.ReadBytes(32);
                }

                for (int k = 0; k < 11; k++)
                {
                    // ruRU, zhCN, plPL, esMX, frFR, esES
                    // ptBR, itIT, enUs, deDe, koKR
                    bitReader.ReadString(4);

                    bitReader.ReadByte();
                    for (int i = 0; ; i++)
                    {
                        if (bitReader.ReadString(4).Substring(0, 2) != "s2") // s2ml
                        {
                            bitReader.stream.Position = bitReader.stream.Position - 4;
                            break;
                        }
                        bitReader.ReadString(4); // s2ml
                        bitReader.ReadBytes(2); // 0x00 0x00
                        bitReader.ReadString(2); // Realm
                        bitReader.ReadBytes(32);
                    }
                }

                // new section, can't find a pattern
                // has blizzmaps#1, Hero, s2mv
                // --------------------
                bitReader.ReadBytes(8); // all 0x00

                for (;;)
                {
                    // we're just going to skip all the way down to the s2mh 
                    if (bitReader.ReadString(4) == "s2mh")
                    {
                        bitReader.stream.Position = bitReader.stream.Position - 4;
                        break;
                    }
                    else
                        bitReader.stream.Position = bitReader.stream.Position - 3;
                }

                for (var i = 0; i < s2mArrayLength; i++)
                {
                    bitReader.ReadString(4); // s2mh
                    bitReader.ReadBytes(2); // 0x00 0x00
                    bitReader.ReadString(2); // Realm
                    bitReader.ReadBytes(32);
                }

                // Player collections - starting with HOTS 2.0 (live build 52860)
                // --------------------------------------------------------------
                List<string> playerCollection = new List<string>();

                int collectionSize = 0;

                if (replay.ReplayBuild >= 48027)
                    collectionSize = bitReader.ReadInt16();
                else
                    collectionSize = bitReader.ReadInt32();

                if (collectionSize > 5000)
                    throw new Exception("collectionSize is an unusually large number");

                for (int i = 0; i < collectionSize; i++)
                {
                    playerCollection.Add(bitReader.ReadString(bitReader.ReadByte()));
                }

                // use to determine if the collection item is usable by the player (owns/free to play/internet cafe)
                if (bitReader.ReadInt32() != collectionSize)
                    throw new Exception("skinArrayLength not equal");

                for (int i = 0; i < collectionSize; i++)
                {
                    for (int j = 0; j < 16; j++) // 16 is total player slots
                    {
                        bitReader.ReadByte();

                        var num = bitReader.Read(8);
                        if (replay.ClientListByUserID[j] != null)
                        {
                            if (num > 0)
                            {
                                replay.ClientListByUserID[j].PlayerCollectionDictionary.Add(playerCollection[i], true);
                            }
                            else if (num == 0)
                            {
                                replay.ClientListByUserID[j].PlayerCollectionDictionary.Add(playerCollection[i], false);
                            }
                            else
                                throw new NotImplementedException();
                        }
                    }
                }

                // Player info 
                // ------------------------
                if (replay.ReplayBuild <= 43259 || replay.ReplayBuild == 47801)
                {
                    // Builds that are not yet supported for detailed parsing
                    // build 47801 is a ptr build that had new data in the battletag section, the data was changed in 47944 (patch for 47801)
                    GetBattleTags(replay, bitReader);
                    return;
                }

                bitReader.ReadInt32(); // m_randomSeed 
                bitReader.ReadBytes(32);

                bitReader.ReadInt32(); // 0x19

                if (replay.ReplayBuild <= 47479 || replay.ReplayBuild == 47903)
                {
                    ExtendedBattleTagParsingOld(replay, bitReader);
                    return;
                }

                for (int player = 0; player < replay.ClientListByUserID.Length; player++)
                {
                    if (replay.ClientListByUserID[player] == null)
                        break;

                    string TId;

                    if (player == 0)
                    {
                        var offset = bitReader.ReadByte();
                        bitReader.ReadString(2); // T:
                        TId = bitReader.ReadString(12 + offset);
                    }
                    else
                    {
                        ReadByte0x00(bitReader);
                        ReadByte0x00(bitReader);
                        ReadByte0x00(bitReader);
                        bitReader.Read(6);

                        // get XXXXXXXX#YYY
                        TId = Encoding.UTF8.GetString(ReadSpecialBlob(bitReader, 8));
                    }

                    replay.ClientListByUserID[player].BattleNetTId = TId;

                    // next 18 bytes
                    bitReader.ReadBytes(4); // same for all players
                    bitReader.ReadBytes(14);

                    if (replay.ReplayBuild >= 53548 || replay.ReplayBuild == 53270)
                    {
                        bitReader.ReadBytes(232);
                        bitReader.Read(1);
                    }
                    else if (replay.ReplayBuild >= 52860)
                    {
                        bitReader.ReadBytes(228);
                        bitReader.Read(7);
                    }
                    else if (replay.ReplayVersionMajor == 2 && replay.ReplayBuild >= 52561)
                    {
                        bitReader.ReadBytes(227);
                        bitReader.Read(4);
                    }
                    else if (replay.ReplayVersionMajor == 2 && replay.ReplayBuild >= 51978)
                    {
                        bitReader.ReadBytes(224);
                        bitReader.Read(0);
                    }
                    else if (replay.ReplayBuild >= 52124)
                    {
                        bitReader.ReadBytes(59);
                        bitReader.Read(4);
                    }
                    else if (replay.ReplayBuild >= 51609)
                    {
                        bitReader.ReadBytes(58);
                        bitReader.Read(7);
                    }
                    else
                    {
                        bitReader.ReadBytes(12);

                        // each byte has a max value of 0x7F (127)
                        if (replay.ReplayBuild >= 48027)
                            bitReader.ReadInt16();
                        else
                            bitReader.ReadInt32();


                        // this data is a repeat of the usable skins section above
                        // bitReader.stream.Position = bitReader.stream.Position = bitReader.stream.Position + (skinArrayLength * 2);
                        for (int i = 0; i < collectionSize; i++)
                        {
                            // each byte has a max value of 0x7F (127)
                            int value = 0;
                            int x = (int)bitReader.Read(8);
                            if (x > 0)
                            {
                                value += x + 127;
                            }
                            value += (int)bitReader.Read(8);
                        }

                        bitReader.Read(1);
                    }

                    if (bitReader.ReadBoolean())
                    {
                        // use this to determine who is in a party
                        // those in the same party will have the same exact 8 bytes of data
                        // the party leader is the first one (in the order of the client list)
                        replay.ClientListByUserID[player].PartyValue = bitReader.ReadInt32() + bitReader.ReadInt32();
                    }

                    bitReader.Read(1);
                    var battleTag = Encoding.UTF8.GetString(bitReader.ReadBlobPrecededWithLength(7)).Split('#'); // battleTag <name>#xxxxx

                    if (battleTag.Length != 2 || battleTag[0] != replay.ClientListByUserID[player].Name)
                        throw new Exception("Couldn't find BattleTag");

                    replay.ClientListByUserID[player].BattleTag = int.Parse(battleTag[1]);

                    if (replay.ReplayBuild >= 52860 || (replay.ReplayVersionMajor == 2 && replay.ReplayBuild >= 51978))
                        replay.ClientListByUserID[player].AccountLevel = bitReader.ReadInt32(); // player's account level, not available in custom games

                    bitReader.ReadBytes(27); // these similar bytes don't occur for last player
                }

                // some more data after this
            }
        }

        // used for builds <= 47479 and 47903
        private static void ExtendedBattleTagParsingOld(Replay replay, BitReader bitReader)
        {
            bool changed47479 = false;

            if (replay.ReplayBuild == 47479 && DetectBattleTagChangeBuild47479(replay, bitReader))
                changed47479 = true;

            for (int i = 0; i < replay.ClientListByUserID.Length; i++)
            {
                if (replay.ClientListByUserID[i] == null)
                    break;

                string TId;
                string TId_2;

                // this first one is weird, nothing to indicate the length of the string
                if (i == 0)
                {
                    var offset = bitReader.ReadByte();
                    bitReader.ReadString(2); // T:
                    TId = bitReader.ReadString(12 + offset);

                    if (replay.ReplayBuild <= 47479 && !changed47479)
                    {
                        bitReader.ReadBytes(6);
                        ReadByte0x00(bitReader);
                        ReadByte0x00(bitReader);
                        ReadByte0x00(bitReader);
                        bitReader.Read(6);

                        // get T: again
                        TId_2 = Encoding.UTF8.GetString(ReadSpecialBlob(bitReader, 8));

                        if (TId != TId_2)
                            throw new Exception("TID dup not equal");
                    }
                }
                else
                {
                    ReadByte0x00(bitReader);
                    ReadByte0x00(bitReader);
                    ReadByte0x00(bitReader);
                    bitReader.Read(6);

                    // get XXXXXXXX#YYY
                    TId = Encoding.UTF8.GetString(ReadSpecialBlob(bitReader, 8));

                    if (replay.ReplayBuild <= 47479 && !changed47479)
                    {
                        bitReader.ReadBytes(6);
                        ReadByte0x00(bitReader);
                        ReadByte0x00(bitReader);
                        ReadByte0x00(bitReader);
                        bitReader.Read(6);

                        // get T: again
                        TId_2 = Encoding.UTF8.GetString(ReadSpecialBlob(bitReader, 8));

                        if (TId != TId_2)
                            throw new Exception("TID dup not equal");
                    }
                }
                replay.ClientListByUserID[i].BattleNetTId = TId;

                // next 31 bytes
                bitReader.ReadBytes(4); // same for all players
                bitReader.ReadByte();
                bitReader.ReadBytes(8);  // same for all players
                bitReader.ReadBytes(4);

                bitReader.ReadBytes(14); // same for all players

                if (replay.ReplayBuild >= 47903 || changed47479)
                    bitReader.ReadBytes(40);
                else if (replay.ReplayBuild >= 47219 || replay.ReplayBuild == 47024)
                    bitReader.ReadBytes(39);
                else if (replay.ReplayBuild >= 45889)
                    bitReader.ReadBytes(38);
                else if (replay.ReplayBuild >= 45228)
                    bitReader.ReadBytes(37);
                else if (replay.ReplayBuild >= 44468)
                    bitReader.ReadBytes(36);
                else
                    bitReader.ReadBytes(35);

                if (replay.ReplayBuild >= 47903 || changed47479)
                    bitReader.Read(1);
                else if (replay.ReplayBuild >= 47219 || replay.ReplayBuild == 47024)
                    bitReader.Read(6);
                else if (replay.ReplayBuild >= 46690 || replay.ReplayBuild == 46416)
                    bitReader.Read(5);
                else if (replay.ReplayBuild >= 45889)
                    bitReader.Read(2);
                else if (replay.ReplayBuild >= 45228)
                    bitReader.Read(3);
                else
                    bitReader.Read(5);

                if (bitReader.ReadBoolean())
                {
                    // use this to determine who is in a party
                    // those in the same party will have the same exact 8 bytes of data
                    // the party leader is the first one (in the order of the client list)
                    replay.ClientListByUserID[i].PartyValue = bitReader.ReadInt32() + bitReader.ReadInt32();
                }

                bitReader.Read(1);
                var battleTag = Encoding.UTF8.GetString(bitReader.ReadBlobPrecededWithLength(7)).Split('#'); // battleTag <name>#xxxxx

                if (battleTag.Length != 2 || battleTag[0] != replay.ClientListByUserID[i].Name)
                    throw new Exception("Couldn't find BattleTag");

                replay.ClientListByUserID[i].BattleTag = int.Parse(battleTag[1]);

                // these similar bytes don't occur for last player
                bitReader.ReadBytes(27);
            }

            // some more bytes after (at least 700)
            // theres some HeroICONs and other repetitive stuff
        }

        // Detect the change that happended in build 47479 on November 2, 2016
        private static bool DetectBattleTagChangeBuild47479(Replay replay, BitReader bitReader)
        {
            if (replay.ReplayBuild != 47479)
                return false;

            bool changed = false;

            var offset = bitReader.ReadByte();
            bitReader.ReadString(2); // T:
            bitReader.ReadString(12 + offset); // TId

            bitReader.ReadBytes(6);

            for (int i = 0; i < 3; i++)
            {
                if (bitReader.Read(8) != 0)
                    changed = true;

                offset += 1;

                if (changed)
                    break;
            }

            bitReader.stream.Position = bitReader.stream.Position - 21 - offset;
            return changed;
        }

        public static void GetBattleTags(Replay replay, byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                var bitReader = new BitReader(stream);

                GetBattleTags(replay, bitReader);
            }
        }

        private static void GetBattleTags(Replay replay, BitReader reader)
        {
            // Search for the BattleTag for each player
            var battleTagDigits = new List<char>();
            for (var playerNum = 0; playerNum < replay.Players.Length; playerNum++)
            {
                var player = replay.Players[playerNum];
                if (player == null)
                    continue;

                // Find each player's name, and then their associated BattleTag
                battleTagDigits.Clear();
                var playerNameBytes = Encoding.UTF8.GetBytes(player.Name);
                while (!reader.EndOfStream)
                {
                    var isFound = true;
                    for (var i = 0; i < playerNameBytes.Length + 1; i++)
                        if ((i == playerNameBytes.Length && reader.ReadByte() != 35 /* '#' Character */) || (i < playerNameBytes.Length && reader.ReadByte() != playerNameBytes[i]))
                        {
                            isFound = false;
                            break;
                        }

                    if (isFound)
                        break;
                }

                // Get the digits from the BattleTag
                while (!reader.EndOfStream)
                {
                    var currentCharacter = (char)reader.ReadByte();

                    if (playerNum == 9 && (currentCharacter == 'z' || currentCharacter == 'Ø'))
                    {
                        // If player is in slot 9, there's a chance that an extra digit could be appended to the BattleTag
                        battleTagDigits.RemoveAt(battleTagDigits.Count - 1);
                        break;
                    }
                    else if (char.IsDigit(currentCharacter))
                        battleTagDigits.Add(currentCharacter);
                    else
                        break;
                }

                if (reader.EndOfStream)
                    break;

                player.BattleTag = int.Parse(string.Join("", battleTagDigits));
            }
        }

        public static byte[] ReadSpecialBlob(BitReader bitReader, int numBitsForLength)
        {
            var stringLength = bitReader.Read(numBitsForLength);
            bitReader.AlignToByte();
            bitReader.ReadBytes(2);
            return bitReader.ReadBytes((int)stringLength);
        }

        private static void ReadByte0x00(BitReader bitReader)
        {
            if (bitReader.ReadByte() != 0)
                throw new Exception("Not 0x00");
        }
    }

    public static class StandaloneBattleLobbyParser
    {
        public static Replay Parse(byte[] data)
        {
            var stringData = Encoding.UTF8.GetString(data);

            var replay = new Replay();

            // Find player region using Regex
            var playerBattleNetRegionId = 99;
            switch (Regex.Match(stringData, @"s2mh..(US|EU|KR|CN|XX)").Value.Substring(6))
            {
                case "US":
                    playerBattleNetRegionId = 1;
                    break;
                case "EU":
                    playerBattleNetRegionId = 2;
                    break;
                case "KR":
                    playerBattleNetRegionId = 3;
                    break;
                case "CN":
                    playerBattleNetRegionId = 5;
                    break;
                case "XX":
                default:
                    break;
            }

            // Find player BattleTags using Regex
            replay.Players = Regex.Matches(stringData, @"(\p{L}|\d){3,24}#\d{4,10}[zØ]?").Cast<Match>().Select(i => i.Value.Split('#')).Select(i => new Player
            {
                Name = i[0],
                BattleTag = int.Parse(i[1].Last() == 'z' || i[1].Last() == 'Ø' ? i[1].Substring(0, i[1].Length - 2) : i[1]),
                BattleNetRegionId = playerBattleNetRegionId
            }).ToArray();

            // For all game modes other than Custom, players should be ordered by team in the lobby
            // This may be true for Custom games as well; more testing needed
            for (var i = 0; i < replay.Players.Length; i++)
                replay.Players[i].Team = i >= 5 ? 1 : 0;

            return replay;
        }

        public static string Base64EncodeStandaloneBattlelobby(Heroes.ReplayParser.Replay replay)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(",", replay.Players.Select(i => i.BattleNetRegionId + "#" + i.Name + "#" + i.BattleTag + "#" + i.Team))));
        }

        public static Replay Base64DecodeStandaloneBattlelobby(string base64EncodedData)
        {
            return new Replay
            {
                Players = Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedData)).Split(',').Select(i => i.Split('#')).Select(i => new Player
                {
                    Name = i[1],
                    BattleTag = int.Parse(i[2]),
                    BattleNetRegionId = int.Parse(i[0]),
                    Team = int.Parse(i[3])
                }).ToArray()
            };
        }
    }
}
