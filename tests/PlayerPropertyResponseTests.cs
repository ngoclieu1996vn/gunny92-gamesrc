using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Game.Server.GameUtils;
using Game.Server.Packets;
using SqlDataProvider.Data;

// Emits real GSPacketIn payloads for the actual Flash reader regression.
class PlayerPropertyResponseTests
{
    static void Main(string[] args)
    {
        var info = new PlayerInfo { ID = 228, fineSuitExp = 8642 };
        var properties = (PlayerProperty)FormatterServices.GetUninitializedObject(typeof(PlayerProperty));
        properties.Current = new Dictionary<string, Dictionary<string, int>> {
            {"Texp", new Dictionary<string,int>{{"Attack",101},{"Defence",102},{"Agility",103},{"Luck",104},{"HP",7654}}},
            {"Avatar", new Dictionary<string,int>{{"Attack",201},{"Defence",202},{"Agility",203},{"Luck",204},{"HP",8765}}},
            {"Gem", new Dictionary<string,int>{{"HP",3456}}},
            {"Mark", new Dictionary<string,int>{{"HP",4567},{"Attack",678}}},
            {"SeloPet", new Dictionary<string,int>{{"HP",5678}}},
            {"CardAchievement", new Dictionary<string,int>{{"HP",6789}}},
            {"MagicAttack", new Dictionary<string,int>{{"MagicStone",1234},{"Horse",2345},{"Texp",321},{"Card",432},{"Mark",543},{"SeloPet",654}}},
            {"MagicDefence", new Dictionary<string,int>{{"MagicStone",4321},{"Horse",5432},{"Temple",6543},{"Texp",765},{"Card",876},{"Mark",987}}},
            {"Damage", new Dictionary<string,int>{{"Avatar",142},{"Mark",143},{"CardAchievement",144}}},
            {"Armor", new Dictionary<string,int>{{"Avatar",242},{"Mark",243},{"CardAchievement",244}}}
        };
        properties.totalDamage = 901; properties.totalArmor = 902;
        Emit(args[0], info, properties);
        properties.Current = new Dictionary<string, Dictionary<string, int>> {
            {"Avatar", new Dictionary<string,int>{{"Attack",0},{"Defence",0},{"Agility",0},{"Luck",0},{"HP",0}}},
            {"Damage", new Dictionary<string,int>{{"Avatar",0}}},
            {"Armor", new Dictionary<string,int>{{"Avatar",0}}}
        };
        properties.totalDamage = properties.totalArmor = 0;
        info.fineSuitExp = 0;
        Emit(args[1], info, properties);
    }

    static void Emit(string file, PlayerInfo info, PlayerProperty properties)
    {
        var packet = PlayerPropertyResponseV92.Create(info, properties, 1001);
        if (packet.Code != 164 || packet.ClientID != 228) throw new Exception("Wrong packet header");
        using (var output = File.Create(file)) output.Write(packet.Buffer, 20, packet.Length - 20);
    }
}
