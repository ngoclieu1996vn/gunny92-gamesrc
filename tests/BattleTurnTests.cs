using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class BattleTurnTests
{
    static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
    static GSPacketIn Upgrade(GSPacketIn p){
#if LEGACY
        return p;
#else
        return BattleResponseV92.UpgradeTurn(p);
#endif
    }
    static GSPacketIn Packet(bool wind,bool box)
    {
        var p=new GSPacketIn(91,2);p.Parameter1=2;p.Parameter2=27;p.WriteByte(6);p.WriteBoolean(wind);p.WriteByte(1);p.WriteByte(2);p.WriteByte(3);
        p.WriteBoolean(true);p.WriteInt(20);p.WriteInt(box?1:0);if(box)foreach(int n in new[]{55,123,456,2})p.WriteInt(n);
        p.WriteInt(2);for(int id=1;id<=2;id++){p.WriteInt(id);p.WriteBoolean(true);foreach(int n in new[]{100*id,200*id,1500})p.WriteInt(n);p.WriteBoolean(false);foreach(int n in new[]{240,20,30,100,70,3,4})p.WriteInt(n);}
        p.WriteInt(7);return p;
    }
    static int Main()
    {
        foreach(bool wind in new[]{false,true})foreach(bool box in new[]{false,true}){
            var old=Packet(wind,box);int cursor=old.Offset;var p=Upgrade(old);p.Offset=20;
            E(p.ReadByte(),(byte)6,"command");E(p.ReadInt(),0,"scene count must not be read from wind bytes");
            E(p.ReadBoolean(),wind,"wind sign");foreach(byte n in new byte[]{1,2,3})E(p.ReadByte(),n,"wind digit");
            E(p.ReadInt(),0,"wind extension");E(p.ReadInt(),100,"wind factor");E(p.ReadInt(),0,"wind extension2");E(p.ReadBoolean(),true,"hidden");E(p.ReadInt(),20,"turn time");
            E(p.ReadInt(),box?1:0,"boxes");if(box)foreach(int n in new[]{55,123,456,2})E(p.ReadInt(),n,"box data");
            E(p.ReadInt(),2,"players");for(int id=1;id<=2;id++){
                E(p.ReadInt(),id,"player id");E(p.ReadBoolean(),true,"alive");foreach(int n in new[]{100*id,200*id,1500})E(p.ReadInt(),n,"position/blood");E(p.ReadBoolean(),false,"no hole");foreach(int n in new[]{240,20,30,100,70})E(p.ReadInt(),n,"energy/psychic/dander/petMP");
                E(p.ReadInt(),0,"pet flag");E(p.ReadInt(),0,"transform energy");E(p.ReadInt(),3,"shoot count");E(p.ReadInt(),4,"fly count");E(p.ReadInt(),0,"pet turn count");
            }
            E(p.ReadInt(),7,"turn index");E(p.ReadBoolean(),false,"dig out crater");E(p.Offset,p.Length,"packet end");E(old.Offset,cursor,"original cursor");E(p.ClientID,2,"client id");E(p.Parameter1,2,"current player");E(p.Parameter2,27,"lifetime");
#if !LEGACY
            old.WriteByte(1);Reject(old);
#endif
        }
#if !LEGACY
        var invalid=new GSPacketIn(91);invalid.WriteByte(6);Reject(invalid);
#endif
        Console.WriteLine("PASS: turn schema preserves wind, box/player positions, health, energy, ammunition and header without unbounded scene counts.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed turn packet accepted");}
#endif
}
