using System;
using System.Reflection;
using System.Runtime.Serialization;
using Game.Base.Packets;
using Game.Server;
using Game.Server.GameObjects;
using Game.Server.Packets.Client;
using SqlDataProvider.Data;
class CaptchaTests
{
    class CaptureClient:GameClient
    {
        public GSPacketIn Last;public bool WasDisconnected;
        public CaptureClient():base(null,new byte[8192],new byte[8192]){}
        public override void SendTCP(GSPacketIn p){Last=p;}
        public override void Disconnect(){WasDisconnected=true;}
    }
    static void Assert(bool condition,string label){if(!condition)throw new Exception(label);}
    static int Main()
    {
        var client=new CaptureClient();
        var player=(GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));
        var info=new PlayerInfo();info.ID=228;info.Grade=10;info.CheckCount=10000;info.CheckError=5;info.CheckTimer=20;info.CheckCode="OLD";info.Money=123;
        typeof(GamePlayer).GetField("m_character",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,info);
        typeof(GamePlayer).GetField("m_client",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,client);
        client.Player=player;var library=new AbstractPacketLib(client);client.Out=library;player.FirstLoginCheckCode=true;
        library.SendCheckCode();Assert(client.Last==null,"Starting a game must not emit a CAPTCHA challenge.");
        var stale=new GSPacketIn(200,228);stale.WriteString("incorrect");stale.Offset=20;
        int result=new CheckCodeHandler().HandlePacket(client,stale);
        Assert(result==0 && !client.WasDisconnected,"An old challenge submission must not kick the player.");
        var close=client.Last;Assert(close!=null && close.Code==200 && close.ClientID==228,"Dismiss the legacy CAPTCHA window.");close.Offset=20;
        Assert(close.ReadByte()==1 && !close.ReadBoolean() && close.Offset==close.Length,"CAPTCHA dismissal must match the current client.");
        Assert(info.CheckCode=="" && info.CheckError==0 && info.CheckCount==0 && info.CheckTimer==0 && !player.FirstLoginCheckCode,"Clear obsolete challenge state.");
        Assert(info.Money==123,"Removing CAPTCHA must not grant currency.");
        Console.WriteLine("PASS: no CAPTCHA on start; stale submissions close safely without kicks or rewards.");return 0;
    }
}
