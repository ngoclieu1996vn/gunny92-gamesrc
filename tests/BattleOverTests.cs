using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class BattleOverTests
{
    static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
    static GSPacketIn Upgrade(GSPacketIn p){
#if LEGACY
        return p;
#else
        return BattleResponseV92.UpgradeOver(p);
#endif
    }
    static GSPacketIn Packet(int count,int tie)
    {
        var p=new GSPacketIn(91,228);p.Parameter1=17;p.Parameter2=39;p.WriteByte(100);p.WriteInt(tie);p.WriteInt(count);
        for(int id=1;id<=count;id++){p.WriteInt(id);p.WriteBoolean(id==1);for(int field=0;field<26;field++)p.WriteInt(id*1000+field);}
        p.WriteInt(321);return p;
    }
    static int Main(){try{return Run();}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
    static int Run()
    {
        foreach(int count in new[]{0,1,2,8})foreach(int tie in new[]{0,-1}){
            var original=Packet(count,tie);int cursor=original.Offset;byte[] bytes=(byte[])original.Buffer.Clone();var p=Upgrade(original);p.Offset=20;
            E(p.ReadByte(),(byte)100,"command");E(p.ReadInt(),tie,"tie status");E(p.ReadInt(),count,"player count");
            for(int id=1;id<=count;id++){
                E(p.ReadInt(),id,"player id");E(p.ReadBoolean(),id==1,"winner");
                // GameOverAction.readInfo: grade/GP and 15 experience fields.
                for(int field=0;field<17;field++)E(p.ReadInt(),id*1000+field,"grade/GP and experience");
                E(p.ReadInt(),0,"gpCSMUser");E(p.ReadInt(),0,"gpAddWell");
                for(int field=17;field<25;field++)E(p.ReadInt(),id*1000+field,"offer fields");
                E(p.ReadInt(),0,"offerCSMUser");E(p.ReadInt(),0,"offerAddWell");E(p.ReadInt(),id*1000+25,"card count");
                E(p.ReadBoolean(),false,"double reward inactive");E(p.ReadInt(),0,"prestige inactive");E(p.ReadInt(),id*1000+2,"actual kill count");
            }
            E(p.ReadInt(),321,"guild riches");E(p.ReadInt(),0,"result trailer1");E(p.ReadInt(),0,"result trailer2");E(p.Offset,p.Length,"exact client end");
            E(original.Offset,cursor,"input cursor");for(int i=0;i<original.Length;i++)E(original.Buffer[i],bytes[i],"input bytes");
            E(p.ClientID,228,"client id");E(p.Parameter1,17,"room");E(p.Parameter2,39,"generation");
#if !LEGACY
            original.WriteByte(1);Reject(original);
#endif
        }
#if !LEGACY
        var shortPacket=new GSPacketIn(91);shortPacket.WriteByte(100);Reject(shortPacket);
        var badCount=Packet(0,0);badCount.Buffer[25]=255;Reject(badCount);
        var full=Packet(2,0);var truncated=new GSPacketIn(91);var payload=new byte[full.Length-21];System.Buffer.BlockCopy(full.Buffer,20,payload,0,payload.Length);truncated.Write(payload);Reject(truncated);
        var other=new GSPacketIn(91);other.WriteByte(6);E(Object.ReferenceEquals(Upgrade(other),other),true,"other commands unchanged");
#endif
        Console.WriteLine("PASS: PvP result matches client field order, preserves rewards/winners/cards/kills and headers, rejects malformed payloads.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed result accepted");}
#endif
}