using System;
using Game.Base.Packets;
using Game.Server.Packets;
class RoomResponseTests
{
    static void Equal(object a, object b, string name) { if (!a.Equals(b)) throw new Exception(name + ": " + a + " != " + b); }
    static GSPacketIn OldPlayer(bool married, bool pet)
    {
        var p = new GSPacketIn(94, 228); p.Parameter1 = 19; p.Parameter2 = 20;
        p.WriteByte(4); p.WriteBoolean(true); p.WriteByte(0); p.WriteByte(1); p.WriteBoolean(false);
        foreach (int n in new[] {31,7,8,9,10,4,228}) p.WriteInt(n);
        p.WriteString("Người chơi"); p.WriteByte(1); p.WriteInt(2); p.WriteBoolean(true);
        p.WriteString("1,2"); p.WriteString("red,blue"); p.WriteString("skin");
        foreach (int n in new[] {1101,0,5}) p.WriteInt(n); p.WriteString("Bang hội");
        foreach (int n in new[] {6,20,30,1,3,4}) p.WriteInt(n);
        p.WriteBoolean(married); if (married) { p.WriteInt(777); p.WriteString("Bạn đời"); }
        p.WriteString("account"); foreach (int n in new[] {12,12345,1,55}) p.WriteInt(n);
        p.WriteString("Masters"); p.WriteInt(2); p.WriteString("Honour"); p.WriteBoolean(false); p.WriteInt(1);
        p.WriteInt(pet ? 1 : 0);
        if (pet) { foreach (int n in new[] {0,1001,23}) p.WriteInt(n); p.WriteString("Thú cưng");
            foreach (int n in new[] {228,10,2,0,100,1,101}) p.WriteInt(n); }
        return p;
    }
    static void CheckPlayer(bool married, bool pet)
    {
        var p = RoomResponseV92.UpgradePlayer(OldPlayer(married, pet)); p.Offset = 20;
        Equal(p.ClientID,228,"player id"); Equal(p.Parameter1,19,"parameter1"); Equal(p.Parameter2,20,"parameter2");
        Equal(p.ReadByte(),(byte)4,"subcode"); Equal(p.ReadBoolean(),true,"in room");
        Equal(p.ReadByte(),(byte)0,"place"); Equal(p.ReadByte(),(byte)1,"team"); Equal(p.ReadBoolean(),false,"first entry");
        foreach (int n in new[] {31,0,7,8,9,10,4,0,0}) Equal(p.ReadInt(),n,"current room player prefix");
        Equal(p.ReadString(),"","peak skills"); Equal(p.ReadInt(),228,"identity"); Equal(p.ReadString(),"Người chơi","nickname");
        Equal(p.ReadByte(),(byte)1,"vip type"); Equal(p.ReadInt(),2,"vip level"); Equal(p.ReadBoolean(),true,"sex");
        foreach (string s in new[] {"1,2","red,blue","skin"}) Equal(p.ReadString(),s,"appearance");
        foreach (int n in new[] {1101,0,5}) Equal(p.ReadInt(),n,"equipment/guild"); Equal(p.ReadString(),"Bang hội","guild name");
        foreach (int n in new[] {6,20,30,1,3,4}) Equal(p.ReadInt(),n,"guild/statistics");
        Equal(p.ReadBoolean(),married,"married"); if(married) { Equal(p.ReadInt(),777,"spouse");Equal(p.ReadString(),"Bạn đời","spouse name"); }
        Equal(p.ReadString(),"account","account"); foreach (int n in new[] {12,12345,1,55}) Equal(p.ReadInt(),n,"power/master");
        Equal(p.ReadString(),"Masters","master list");Equal(p.ReadInt(),2,"graduates");Equal(p.ReadString(),"Honour","master honour");
        Equal(p.ReadBoolean(),false,"league");Equal(p.ReadInt(),1,"league score");
        Equal(p.ReadInt(),0,"guard core");Equal(p.ReadBoolean(),false,"old player"); Equal(p.ReadInt(),pet ? 1 : 0,"pet count");
        if(pet) { foreach(int n in new[] {0,1001,23})Equal(p.ReadInt(),n,"pet identity");Equal(p.ReadString(),"Thú cưng","pet name");
            foreach(int n in new[] {228,10,2,0,100,1,101})Equal(p.ReadInt(),n,"pet owner/skills"); }
        Equal(p.ReadInt(),0,"room border"); Equal(p.ReadInt(),0,"elf"); Equal(p.Offset,p.Length,"player packet end");
    }
    static int Main()
    {
        var p = new GSPacketIn(94); p.WriteByte(0); p.WriteInt(123);
        foreach(byte n in new byte[] {0,0,3,1,0,8})p.WriteByte(n);
        p.WriteBoolean(false);p.WriteInt(11);p.WriteBoolean(false);p.WriteString("Test Phòng");p.WriteByte(0);p.WriteInt(20);
        p.WriteBoolean(false);p.WriteBoolean(false);p.WriteBoolean(false);p.WriteString("map.png");
        var q=RoomResponseV92.UpgradeCreate(p);
        Equal(q.Length,p.Length+5,"create response required trailer");
        for(int i=20;i<p.Length;i++)Equal(q.Buffer[i],p.Buffer[i],"create legacy fields preserved");
        q.Offset=p.Length;Equal(q.ReadBoolean(),false,"direct loading");Equal(q.ReadInt(),0,"room parameter");Equal(q.Offset,q.Length,"create packet end");
        foreach(bool married in new[] {false,true})foreach(bool pet in new[] {false,true})CheckPlayer(married,pet);
        Console.WriteLine("PASS: room creation and player payloads match the current client, including spouse and pet/skill branches.");return 0;
    }
}
