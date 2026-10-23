using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class BattleHealthTests
{
    static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
    static GSPacketIn Upgrade(GSPacketIn p){
#if LEGACY
        return p;
#else
        return BattleResponseV92.UpgradeHealth(p);
#endif
    }
    static GSPacketIn Packet(int blood,int delta){var p=new GSPacketIn(91,228);p.Parameter1=228;p.Parameter2=17;p.WriteByte(11);p.WriteByte(7);p.WriteInt(blood);p.WriteInt(delta);return p;}
    static int Main(){try{return Run();}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
    static int Run()
    {
        foreach(int blood in new[]{1200,0,int.MaxValue,-1})foreach(int delta in new[]{375,-120,0}){
            var original=Packet(blood,delta);int cursor=original.Offset;var p=Upgrade(original);p.Offset=20;
            E(p.ReadByte(),(byte)11,"command");E(p.ReadByte(),(byte)7,"health change type");
            E(p.ReadInt(),blood<0?-1:0,"health long high word");E(p.ReadInt(),blood,"actual health low word");
            E(p.ReadInt(),delta,"actual health change");E(p.ReadInt(),0,"inactive optional damage context");E(p.Offset,p.Length,"exact client end");
            E(original.Offset,cursor,"input cursor");E(original.Length,30,"input unchanged");E(p.ClientID,228,"living id");E(p.Parameter1,228,"living header");E(p.Parameter2,17,"header2");
#if !LEGACY
            original.WriteByte(1);Reject(original);
#endif
        }
#if !LEGACY
        var shortPacket=new GSPacketIn(91);shortPacket.WriteByte(11);Reject(shortPacket);
        var other=new GSPacketIn(91);other.WriteByte(100);E(Object.ReferenceEquals(Upgrade(other),other),true,"other command unchanged");
#endif
        Console.WriteLine("PASS: health updates use the current long/int/context schema, preserving damage, death, healing and headers.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed health packet accepted");}
#endif
}