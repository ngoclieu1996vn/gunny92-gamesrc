using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class InventoryResponseTests
{
    static void Equal(object actual, object expected, string label)
    { if (!actual.Equals(expected)) throw new Exception(label + ": " + actual + " != " + expected); }
    static GSPacketIn Upgrade(GSPacketIn p)
    {
#if LEGACY
        return p;
#else
        return InventoryResponseV92.Upgrade(p);
#endif
    }
    static void Item(GSPacketIn p, int slot, bool gold)
    {
        p.WriteInt(slot); p.WriteBoolean(true);
        foreach(int n in new[]{228,455409,1,slot,7008,11,12,13,14,5,123})p.WriteInt(n);
        p.WriteBoolean(true);p.WriteBoolean(true);p.WriteDateTime(new DateTime(2026,1,2));p.WriteInt(365);
        p.WriteString("màu đỏ");p.WriteString("");p.WriteBoolean(true);
        for(int i=0;i<6;i++)p.WriteInt(i+10);
        p.WriteString("weapon");p.WriteInt(3);p.WriteDateTime(new DateTime(2026,2,3));p.WriteInt(8);
        p.WriteByte(2);p.WriteInt(20);p.WriteByte(3);p.WriteInt(30);p.WriteInt(99);p.WriteBoolean(false);
        p.WriteBoolean(gold);if(gold){p.WriteInt(7);p.WriteDateTime(new DateTime(2026,3,4));}
        p.WriteString("current");p.WriteString("new");p.WriteDateTime(new DateTime(2026,4,5));
    }
    static void ReadItem(GSPacketIn p,int slot,bool gold,int bag)
    {
        Equal(p.ReadInt(),slot,"slot");Equal(p.ReadBoolean(),true,"exists");
        foreach(int n in new[]{228,455409,1,slot,7008,11,12,13,14,5,123})Equal(p.ReadInt(),n,"item data");
        Equal(p.ReadBoolean(),true,"bound");Equal(p.ReadBoolean(),true,"judged");
        p.ReadDateTime();Equal(p.ReadInt(),365,"validity");Equal(p.ReadString(),"màu đỏ","unicode");
        Equal(p.ReadString(),"","skin");Equal(p.ReadBoolean(),true,"used");
        for(int i=0;i<6;i++)Equal(p.ReadInt(),i+10,"hole");
        Equal(p.ReadString(),"weapon","picture");Equal(p.ReadInt(),3,"refinery");p.ReadDateTime();Equal(p.ReadInt(),8,"strengthen times");
        Equal(p.ReadByte(),(byte)2,"hole5");Equal(p.ReadInt(),20,"hole5 exp");Equal(p.ReadByte(),(byte)3,"hole6");Equal(p.ReadInt(),30,"hole6 exp");
        Equal(p.ReadInt(),99,"exp");Equal(p.ReadBoolean(),false,"cell lock");Equal(p.ReadBoolean(),gold,"gold");
        if(gold){Equal(p.ReadInt(),7,"gold days");p.ReadDateTime();}
        Equal(p.ReadString(),"current","latent");Equal(p.ReadString(),"new","new latent");p.ReadDateTime();
        Equal(p.ReadInt(),0,"magic attack");Equal(p.ReadInt(),0,"magic defence");Equal(p.ReadBoolean(),false,"goods lock");
        Equal(p.ReadInt(),0,"magic exp");Equal(p.ReadInt(),0,"magic level");Equal(p.ReadInt(),0,"enchant level");Equal(p.ReadString(),"","magic jade");
        if(bag!=52){Equal(p.ReadInt(),0,"pour exp");Equal(p.ReadInt(),0,"breakthrough");Equal(p.ReadString(),"","properties");Equal(p.ReadInt(),0,"deputy refine");}
    }
    static GSPacketIn Packet(int bag)
    {
        var p=new GSPacketIn(64,228);p.Parameter1=19;p.Parameter2=20;p.WriteInt(bag);p.WriteInt(3);
        Item(p,6,false);p.WriteInt(7);p.WriteBoolean(false);Item(p,8,true);return p;
    }
    static int Main()
    {
        foreach(int bag in new[]{0,1,52}){
            var old=Packet(bag);var before=(byte[])old.Buffer.Clone();int offset=old.Offset;
            var p=Upgrade(old);p.Offset=20;Equal(p.ClientID,228,"player");Equal(p.Parameter1,19,"parameter1");Equal(p.Parameter2,20,"parameter2");
            Equal(p.ReadInt(),bag,"bag");Equal(p.ReadInt(),3,"count");ReadItem(p,6,false,bag);
            Equal(p.ReadInt(),7,"removed slot");Equal(p.ReadBoolean(),false,"removed");ReadItem(p,8,true,bag);Equal(p.Offset,p.Length,"packet end");
            Equal(old.Offset,offset,"original cursor");for(int i=0;i<old.Length;i++)Equal(old.Buffer[i],before[i],"original bytes");
        }
#if !LEGACY
        var unrelated=new GSPacketIn(65);Equal(Object.ReferenceEquals(Upgrade(unrelated),unrelated),true,"other packet");
        var extra=Packet(0);extra.WriteByte(1);Reject(extra);
        var truncated=new GSPacketIn(64);truncated.WriteInt(0);truncated.WriteInt(1);truncated.WriteInt(6);truncated.WriteBoolean(true);Reject(truncated);
        var negative=new GSPacketIn(64);negative.WriteInt(0);negative.WriteInt(-1);Reject(negative);
#endif
        Console.WriteLine("PASS: current-client inventory parsing, multiple slots, removal, gold, bag52, unicode and malformed packets.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed inventory accepted");}
#endif
}
