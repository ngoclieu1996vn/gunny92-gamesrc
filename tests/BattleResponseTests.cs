using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class BattleResponseTests
{
    static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
    static GSPacketIn Upgrade(GSPacketIn p){
#if LEGACY
        return p;
#else
        return BattleResponseV92.UpgradeCreate(p);
#endif
    }
    static void Player(GSPacketIn p,bool equipped,bool married,bool pet)
    {
        p.WriteInt(1001);p.WriteString("Máy chủ");p.WriteInt(228);p.WriteString("Xạ thủ");p.WriteBoolean(false);p.WriteInt(3);
        p.WriteByte(1);p.WriteInt(2);p.WriteBoolean(true);p.WriteInt(4);p.WriteString("style");p.WriteString("colors");p.WriteString("skin");p.WriteInt(10);p.WriteInt(99);
        p.WriteInt(equipped?7008:0);if(equipped){p.WriteInt(5);p.WriteString("Vũ khí");p.WriteDateTime(new DateTime(2000,1,1));}
        p.WriteInt(17001);p.WriteInt(6);p.WriteBoolean(true);p.WriteInt(7);p.WriteString("Bang hội");
        foreach(int n in new[]{8,9,10,11,12,123456})p.WriteInt(n);
        p.WriteInt(13);p.WriteInt(14);p.WriteString("Masters");p.WriteInt(15);p.WriteString("Honor");p.WriteInt(16);p.WriteBoolean(true);p.WriteInt(17);
        p.WriteBoolean(married);if(married){p.WriteInt(18);p.WriteString("Spouse");}
        for(int i=0;i<6;i++)p.WriteInt(i+20);p.WriteInt(1);p.WriteInt(2);p.WriteInt(3000);
        p.WriteInt(pet?1:0);if(pet){p.WriteInt(0);p.WriteInt(100);p.WriteInt(101);p.WriteString("Thú cưng");foreach(int n in new[]{228,10,2,0,555,1,556})p.WriteInt(n);}
    }
    static void Read(GSPacketIn p,bool equipped,bool married,bool pet)
    {
        E(p.ReadInt(),1001,"zone");E(p.ReadString(),"Máy chủ","zone name");E(p.ReadInt(),228,"id");E(p.ReadString(),"Xạ thủ","name");E(p.ReadBoolean(),false,"viewer");
        E(p.ReadByte(),(byte)1,"VIP must immediately follow viewer");E(p.ReadInt(),2,"VIP level");E(p.ReadBoolean(),true,"sex");E(p.ReadInt(),4,"hide");
        foreach(string s in new[]{"style","colors","skin"})E(p.ReadString(),s,"appearance");E(p.ReadInt(),10,"grade");E(p.ReadInt(),99,"repute");
        E(p.ReadInt(),equipped?7008:0,"weapon");if(equipped){E(p.ReadInt(),5,"refinery");E(p.ReadString(),"Vũ khí","weapon name");p.ReadDateTime();}
        E(p.ReadInt(),17001,"deputy");E(p.ReadInt(),0,"pvp badge");E(p.ReadInt(),6,"nimbus");E(p.ReadBoolean(),true,"show guild");E(p.ReadInt(),7,"guild");E(p.ReadString(),"Bang hội","guild name");
        foreach(int n in new[]{8,9,10,11,12,123456})E(p.ReadInt(),n,"guild and power");E(p.ReadInt(),0,"power high");E(p.ReadInt(),123456,"power low");
        E(p.ReadInt(),13,"apprentice");E(p.ReadInt(),14,"master");E(p.ReadString(),"Masters","masters");E(p.ReadInt(),15,"achievement");E(p.ReadString(),"Honor","honor");E(p.ReadInt(),16,"offer");E(p.ReadBoolean(),true,"league");E(p.ReadInt(),17,"league score");E(p.ReadInt(),0,"guard core");
        E(p.ReadBoolean(),married,"married");if(married){E(p.ReadInt(),18,"spouse");E(p.ReadString(),"Spouse","spouse name");}
        for(int i=0;i<6;i++)E(p.ReadInt(),i+20,"reserved");E(p.ReadInt(),3,"moved place");E(p.ReadInt(),1,"team");E(p.ReadInt(),2,"game id");E(p.ReadInt(),3000,"blood");
        E(p.ReadInt(),pet?1:0,"pet count");if(pet){foreach(int n in new[]{0,100,101})E(p.ReadInt(),n,"pet identity");E(p.ReadString(),"Thú cưng","pet name");foreach(int n in new[]{228,10,2,0,555,1,556})E(p.ReadInt(),n,"pet skills");}
        E(p.ReadInt(),0,"horse skills");E(p.ReadInt(),0,"battle skills");E(p.ReadBoolean(),false,"client robot flag");E(p.ReadInt(),0,"world king pet");E(p.ReadString(),"","pet skills");E(p.ReadString(),"","pet data");E(p.ReadBoolean(),false,"ring");
        for(int i=0;i<4;i++)E(p.ReadInt(),0,"love/turn/team/division");E(p.ReadString(),"","team name");E(p.ReadString(),"","team tag");for(int i=0;i<6;i++)E(p.ReadInt(),0,"team/elf/avatar");
    }
    static int Main()
    {
        foreach(bool weapon in new[]{false,true})foreach(bool married in new[]{false,true})foreach(bool pet in new[]{false,true}){
            var old=new GSPacketIn(91,77);old.Parameter1=18;old.Parameter2=19;old.WriteByte(101);foreach(int n in new[]{0,0,20,2})old.WriteInt(n);Player(old,weapon,married,pet);Player(old,!weapon,!married,!pet);
            int cursor=old.Offset;var p=Upgrade(old);E(old.Offset,cursor,"original cursor");p.Offset=20;E(p.ClientID,77,"header id");E(p.Parameter1,18,"parameter1");E(p.Parameter2,19,"parameter2");E(p.ReadByte(),(byte)101,"subcode");foreach(int n in new[]{0,0,20,2})E(p.ReadInt(),n,"room header");Read(p,weapon,married,pet);Read(p,!weapon,!married,!pet);
            E(p.ReadBoolean(),false,"guard enable");E(p.ReadInt(),0,"bomb count");E(p.ReadString(),"","small map");E(p.ReadInt(),0,"small map value");E(p.Offset,p.Length,"packet end");
#if !LEGACY
            old.WriteByte(1);Reject(old);
#endif
        }
#if !LEGACY
        var unrelated=new GSPacketIn(91);unrelated.WriteByte(102);E(Object.ReferenceEquals(Upgrade(unrelated),unrelated),true,"unrelated battle packet");
        var invalid=new GSPacketIn(91);invalid.WriteByte(101);invalid.WriteInt(0);Reject(invalid);
#endif
        Console.WriteLine("PASS: battle create client order, moved place, weapons, spouse, pet skills, multiple players and exact trailer.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed battle packet accepted");}
#endif
}
