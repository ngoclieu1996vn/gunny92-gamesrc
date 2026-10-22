using System;
using Game.Base.Packets;
using Game.Server.Packets;
using SqlDataProvider.Data;
class HallSceneTests
{
    static void Equal(object actual, object expected, string name)
    { if (!actual.Equals(expected)) throw new Exception(name + ": " + actual + " != " + expected); }
    static void CheckHeader(GSPacketIn packet, int count)
    {
        Equal(packet.Code, (short)262, "code"); Equal(packet.ClientID, 227, "owner");
        packet.Offset = 20; Equal(packet.ReadByte(), (byte)0, "subcode");
        Equal(packet.ReadInt(), 0, "page"); Equal(packet.ReadInt(), count, "total");
        Equal(packet.ReadInt(), count, "count");
    }
    static int Main()
    {
        Equal(HallSceneV92.TryHandle(null, new GSPacketIn(262)), true, "unauthenticated hall request ignored");
        Equal(HallSceneV92.TryHandle(null, new GSPacketIn(1)), false, "unrelated packet retained");
        var empty = HallSceneV92.CreateSceneList(227, new PlayerInfo[0]);
        CheckHeader(empty, 0); Equal(empty.Offset, empty.Length, "empty response end");
        var peer = new PlayerInfo { ID = 228, NickName = "Người chơi", VIPLevel = 2,
            typeVIP = 1, Sex = true, Style = "1,2", Colors = "red,blue", ConsortiaID = 13,
            badgeID = 4, ConsortiaName = "Bang hội", Honor = "Danh hiệu", honorId = 17 };
        var packet = HallSceneV92.CreateSceneList(227, new[] { peer });
        CheckHeader(packet, 1);
        Equal(packet.ReadInt(), 228, "peer id"); Equal(packet.ReadString(), peer.NickName, "name");
        Equal(packet.ReadInt(), 2, "vip level"); Equal(packet.ReadInt(), 1, "vip type");
        Equal(packet.ReadBoolean(), true, "sex"); Equal(packet.ReadString(), peer.Style, "style");
        Equal(packet.ReadString(), peer.Colors, "colors");
        for (int i = 0; i < 4; i++) Equal(packet.ReadInt(), 0, "unsupported mount/pet/reserved");
        Equal(packet.ReadInt(), 13, "guild"); Equal(packet.ReadInt(), 4, "badge");
        Equal(packet.ReadString(), peer.ConsortiaName, "guild name");
        Equal(packet.ReadString(), peer.Honor, "honor"); Equal(packet.ReadInt(), 17, "honor id");
        Equal(packet.ReadBoolean(), false, "attest"); Equal(packet.ReadString(), "", "image path");
        Equal(packet.ReadBoolean(), false, "show image");
        for (int i = 0; i < 4; i++) Equal(packet.ReadInt(), 0, "unsupported wardrobe/stage");
        Equal(packet.Offset, packet.Length, "peer response end");
        Console.WriteLine("PASS: hall list packet triggers self creation, preserves public player data and matches client read order.");
        return 0;
    }
}
