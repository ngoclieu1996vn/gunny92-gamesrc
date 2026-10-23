using System;
using System.Reflection;
using System.Runtime.Serialization;
using Game.Base.Packets;
using Game.Server;
using Game.Server.GameObjects;
using Game.Server.Packets.Client;
using SqlDataProvider.Data;
class MailAttachmentHookTests
{
    sealed class CaptureClient:GameClient
    {
        public int Sends;
        public CaptureClient():base(null,new byte[8192],new byte[8192]){}
        public override void SendTCP(GSPacketIn packet){Sends++;}
    }
    static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
    static GSPacketIn Request(params int[] ids){var p=new GSPacketIn(113,999);p.WriteByte(0);p.WriteInt(ids.Length);foreach(int id in ids)p.WriteInt(id);p.Offset=20;return p;}
    static int Main(){try{Run();return 0;}catch(Exception error){Console.Error.WriteLine(error.ToString());return 1;}}
    static void Run()
    {
        var client=new CaptureClient();client.Out=new AbstractPacketLib(client);
        var player=(GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));
        var info=new PlayerInfo{ID=228,PasswordTwo="locked-test",IsLocked=true,Money=123,Gold=456,GiftToken=789};
        typeof(GamePlayer).GetField("m_character",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,info);
        typeof(GamePlayer).GetField("m_client",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,client);client.Player=player;
        var handler=new MailGetAttachHandler();handler.HandlePacket(client,Request(85092,85090));
        E(client.Sends,2,"Both modern mail IDs must reach original bag-password guard without database access");
        client.Sends=0;handler.HandlePacket(client,Request(85092,85092));E(client.Sends,0,"Reject whole duplicate batch before any legacy side effect");
        var shortRequest=new GSPacketIn(113);shortRequest.WriteByte(0);shortRequest.Offset=20;handler.HandlePacket(client,shortRequest);E(client.Sends,0,"Reject malformed packet before original reader");
        handler.HandlePacket(null,Request(85092));E(client.Sends,0,"Missing client ignored");
        E(info.Money,123,"money unchanged");E(info.Gold,456,"gold unchanged");E(info.GiftToken,789,"gift tokens unchanged");E(info.IsLocked,true,"bag remains locked");
        Console.WriteLine("PASS: patched mail dispatcher invokes the original ownership/inventory handler with bag-lock checks; malformed/duplicate requests have no side effects.");
    }
}