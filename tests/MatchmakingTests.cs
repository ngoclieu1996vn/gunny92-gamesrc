using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Rooms;
using Game.Server.Packets;
using Game.Logic;
using Game.Server.Battle;
class MatchmakingTests
{
    class CaptureConnector : FightServerConnector
    {
        public GSPacketIn Last;
        public CaptureConnector():base(null,"127.0.0.1",1,"") {}
        public override void SendTCP(GSPacketIn packet) { Last=packet; }
    }
    static void Assert(bool value, string message) { if(!value) throw new Exception(message); }
    static int Main()
    {
        var host=(GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));
        var other=(GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));
        var room=new BaseRoom(77); room.RoomType=eRoomType.Match;
        var relayServer=new BattleServer(1,"127.0.0.1",1,"");
        foreach(var field in typeof(BattleServer).GetFields(BindingFlags.NonPublic|BindingFlags.Instance))
            if(field.FieldType==typeof(Dictionary<int,BaseRoom>))((Dictionary<int,BaseRoom>)field.GetValue(relayServer)).Add(77,room);
        var startPacket=new GSPacketIn(91);startPacket.WriteByte(99);startPacket.WriteInt(0);startPacket.WriteDateTime(new DateTime(2026,1,1));
        Assert(Object.ReferenceEquals(MatchmakingV92.UpgradeRoomResponse(startPacket,null,77),startPacket),"Unknown room must retain its start schema.");
        room.RoomType=eRoomType.Dungeon;
        Assert(Object.ReferenceEquals(MatchmakingV92.UpgradeRoomResponse(startPacket,relayServer,77),startPacket),"PvE start schema must not be adapted as PvP.");
        foreach(var kind in new[]{eRoomType.Match,eRoomType.EliteGameScore,eRoomType.EliteGameChampion}){
            room.RoomType=kind;Assert(!Object.ReferenceEquals(MatchmakingV92.UpgradeRoomResponse(startPacket,relayServer,77),startPacket),"Known remote PvP room must receive current start schema.");}
        room.RoomType=eRoomType.Match;
        typeof(BaseRoom).GetField("m_host",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(room,host);
        MatchmakingV92.SetBotPreference(host,true);
        Assert(MatchmakingV92.WantsBot(room),"Bot selection must be retained for the room host.");
        MatchmakingV92.SetBotPreference(other,false);
        Assert(MatchmakingV92.WantsBot(room),"Another session must not overwrite host preference.");
        MatchmakingV92.SetBotPreference(host,false);
        Assert(!MatchmakingV92.WantsBot(room),"Selecting people must clear bot preference.");
        MatchmakingV92.SetBotPreference(host,true); room.RoomType=eRoomType.Dungeon;
        Assert(!MatchmakingV92.WantsBot(room),"Dungeon must not be converted into bot matchmaking.");
        var p=MatchmakingV92.CreateRival(true); p.Offset=20;
        Assert(p.Code==94 && p.ReadByte()==36 && p.ReadBoolean() && p.DataLeft==0,"Current client rival response contract.");
        room.RoomType=eRoomType.Match; MatchmakingV92.SetBotPreference(host,false);
        typeof(BaseRoom).GetField("m_playerCount",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(room,1);
        MatchmakingV92.PrepareMatch(room);
        var connector=new CaptureConnector(); MatchmakingV92.SendRemoveRoom(connector,room);
        var request=connector.Last; int generation=request.Parameter2;
        Assert(request.Code==65 && request.Parameter1==77 && generation>0,"Cancellation must identify the matching attempt.");
        Assert(MatchmakingV92.AcceptCancellation(room,generation),"Current cancellation must acknowledge.");
        Assert(!MatchmakingV92.AcceptCancellation(room,0),"Untagged stale acknowledgement must not clear a pending match.");
        MatchmakingV92.PrepareMatch(room);
        Assert(!MatchmakingV92.AcceptCancellation(room,generation),"Prior attempt acknowledgement must not affect a new match.");
        Assert(!MatchmakingV92.AcceptCancellation(room,0),"Old untagged cleanup must not remove a new attempt.");
        typeof(BaseRoom).GetField("m_playerCount",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(room,0);
        MatchmakingV92.SendRemoveRoom(connector,room);
        Assert(connector.Last.ClientID==1,"Empty-room cleanup must be distinguished from user cancellation.");
        Assert(MatchmakingV92.AcceptCancellation(room,connector.Last.Parameter2),"Tagged cleanup acknowledgement must be accepted.");
        Console.WriteLine("PASS: bot choice is per session, follows the host, respects room type and matches the client wire response.");
        return 0;
    }
}
