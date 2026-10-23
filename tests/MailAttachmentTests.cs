using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class MailAttachmentTests
{
    static void E(object a,object b,string message){if(!a.Equals(b))throw new Exception(message+": "+a+" != "+b);}
    static GSPacketIn[] Decode(GSPacketIn request){
#if LEGACY
        request.Offset=20;int id=request.ReadInt();byte kind=request.ReadByte();var p=new GSPacketIn(113,228);p.WriteInt(id);p.WriteByte(kind);p.Offset=20;return new[]{p};
#else
        return MailAttachmentV92.Decode(request,228);
#endif
    }
    static GSPacketIn Request(byte kind,params int[] ids){var p=new GSPacketIn(113,999);p.Parameter1=17;p.Parameter2=31;p.WriteByte(kind);p.WriteInt(ids.Length);foreach(int id in ids)p.WriteInt(id);p.Offset=20;return p;}
    static int Main(){try{Run();return 0;}catch(Exception error){Console.Error.WriteLine("FAIL: "+error.Message);return 1;}}
    static void Run()
    {
        foreach(byte kind in new byte[]{0,1,5,6,7})foreach(int[] ids in new[]{new[]{85092},new[]{85092,85090}}){
            var request=Request(kind,ids);int cursor=request.Offset;var packets=Decode(request);E(packets.Length,ids.Length,"mail count");
            for(int i=0;i<ids.Length;i++){var p=packets[i];p.Offset=20;E(p.ReadInt(),ids[i],"exact intended mail ID");E(p.ReadByte(),kind,"selected attachment kind");E(p.Offset,p.Length,"legacy payload end");E(p.ClientID,228,"authenticated player, not forged request header");}
#if !LEGACY
            E(request.Offset,cursor,"input cursor unchanged");
#endif
        }
#if !LEGACY
        var maximum=new int[100];for(int i=0;i<100;i++)maximum[i]=i+1;E(Decode(Request(0,maximum)).Length,100,"bounded bulk request");
        Reject(Request(0));Reject(Request(0,85092,85092));Reject(Request(0,-1));Reject(Request(0,0));Reject(Request(255,85092));
        var truncated=Request(0,85092);truncated.Buffer[24]=2;Reject(truncated);
        var trailing=Request(0,85092);trailing.WriteByte(1);Reject(trailing);
        var wrongCode=Request(0,85092);wrongCode.Code=112;Reject(wrongCode);
        var shortRequest=new GSPacketIn(113);shortRequest.WriteInt(1);shortRequest.Offset=20;Reject(shortRequest);
        var ids101=new int[101];for(int i=0;i<ids101.Length;i++)ids101[i]=i+1;Reject(Request(0,ids101));
        foreach(int[] claims in new[]{new int[0],new[]{1},new[]{1,2,6,7}}){
            var old=new GSPacketIn(113,228);old.Parameter1=17;old.Parameter2=31;old.WriteInt(85092);old.WriteInt(claims.Length);foreach(int id in claims)old.WriteInt(id);int cursor=old.Offset;
            var response=MailAttachmentV92.UpgradeResponse(old);response.Offset=20;E(response.ReadInt(),1,"current client result count");E(response.ReadInt(),85092,"actual result mail ID");E(response.ReadInt(),claims.Length,"actual claimed slot count");foreach(int id in claims)E(response.ReadInt(),id,"actual claimed slot");E(response.Offset,response.Length,"exact current reader end");E(old.Offset,cursor,"response input cursor unchanged");E(response.ClientID,228,"response player header");
        }
        var other=new GSPacketIn(64);E(Object.ReferenceEquals(MailAttachmentV92.UpgradeResponse(other),other),true,"unrelated response unchanged");
#endif
        Console.WriteLine("PASS: modern mail selection maps to exact authorized legacy requests; results match client framing; malformed and duplicate claims rejected.");
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Decode(p);}catch(InvalidDataException){return;}throw new Exception("Malformed mail claim accepted");}
#endif
}