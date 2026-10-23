using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class BattleStartTests
{
    static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
    static GSPacketIn Upgrade(GSPacketIn p){
#if LEGACY
        return p;
#else
        return BattleResponseV92.UpgradeStart(p);
#endif
    }
    static GSPacketIn Packet(bool buffs)
    {
        var p=new GSPacketIn(91,77);p.Parameter1=18;p.Parameter2=19;p.WriteByte(99);p.WriteInt(2);
        for(int id=1;id<=2;id++){
            foreach(int n in new[]{id,123,456,-1,1500,2000,id,3,50,20})p.WriteInt(n);
            p.WriteInt(buffs?2:0);if(buffs)foreach(int n in new[]{16,10,20,30})p.WriteInt(n);
            p.WriteInt(buffs?1:0);if(buffs)p.WriteInt(21);
            p.WriteBoolean(true);p.WriteBoolean(false);p.WriteBoolean(true);p.WriteBoolean(false);
            p.WriteInt(buffs?1:0);if(buffs){p.WriteString("Thuộc tính");p.WriteString("Giá trị");}
        }
        p.WriteDateTime(new DateTime(2026,10,23,10,0,0));return p;
    }
    static int Main()
    {
        foreach(bool buffs in new[]{false,true}){
            var old=Packet(buffs);int cursor=old.Offset;var p=Upgrade(old);p.Offset=20;
            E(p.ReadByte(),(byte)99,"command");E(p.ReadInt(),2,"players");
            for(int id=1;id<=2;id++){
                foreach(int n in new[]{id,123,456,-1,1500,2000,id,3})E(p.ReadInt(),n,"player position/health/team/refinery");
                E(p.ReadInt(),0,"deputy weapon count");E(p.ReadInt(),50,"power ratio");E(p.ReadInt(),20,"dander");E(p.ReadInt(),0,"wish count");E(p.ReadInt(),0,"wish energy");
                E(p.ReadInt(),buffs?2:0,"buff count");if(buffs)foreach(int n in new[]{16,10,20,30})E(p.ReadInt(),n,"buff and value");
                E(p.ReadInt(),buffs?1:0,"turn buffs");if(buffs)E(p.ReadInt(),21,"turn buff id");
                E(p.ReadBoolean(),true,"frozen");E(p.ReadBoolean(),false,"hidden");E(p.ReadBoolean(),true,"no hole");E(p.ReadBoolean(),false,"fourth flag");
                E(p.ReadInt(),0,"possess to");E(p.ReadInt(),0,"possessed by");E(p.ReadInt(),buffs?1:0,"properties");if(buffs){E(p.ReadString(),"Thuộc tính","property name");E(p.ReadString(),"Giá trị","property value");}
                for(int i=0;i<5;i++)E(p.ReadInt(),0,"mark/exit/ling stone");
            }
            E(p.ReadInt(),0,"out bombs");E(p.ReadInt(),0,"out boxes");E(p.ReadDateTime(),new DateTime(2026,10,23,10,0,0),"start time");E(p.ReadInt(),0,"map card label");E(p.Offset,p.Length,"packet end");
            E(old.Offset,cursor,"original cursor");E(p.ClientID,77,"header id");E(p.Parameter1,18,"parameter1");E(p.Parameter2,19,"parameter2");
#if !LEGACY
            old.WriteByte(1);Reject(old);
#endif
        }
#if !LEGACY
        var truncated=new GSPacketIn(91);truncated.WriteByte(99);truncated.WriteInt(2);Reject(truncated);
        var empty=new GSPacketIn(91);empty.WriteByte(99);empty.WriteInt(0);empty.WriteDateTime(new DateTime(2026,1,1));var zero=Upgrade(empty);zero.Offset=21;E(zero.ReadInt(),0,"empty players");E(zero.ReadInt(),0,"empty bombs");E(zero.ReadInt(),0,"empty boxes");zero.ReadDateTime();E(zero.ReadInt(),0,"empty label");E(zero.Offset,zero.Length,"empty end");
#endif
        Console.WriteLine("PASS: battle start current-client order, player position/health, buff branches, properties and trailer.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed start packet accepted");}
#endif
}
