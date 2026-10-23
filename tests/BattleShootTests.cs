using System;
using System.IO;
using Game.Base.Packets;
#if !LEGACY
using Game.Server.Packets;
#endif
class BattleShootTests
{
    static void E(object a,object b,string name){if(!a.Equals(b))throw new Exception(name+": "+a+" != "+b);}
    static GSPacketIn Upgrade(GSPacketIn p){
#if LEGACY
        return p;
#else
        return BattleResponseV92.UpgradeShoot(p);
#endif
    }
    static GSPacketIn Packet(int wind,int bombs,bool pets)
    {
        var p=new GSPacketIn(91,7);p.Parameter1=7;p.Parameter2=12;p.WriteByte(2);p.WriteInt(wind);p.WriteBoolean(wind>0);
        foreach(byte n in new byte[]{1,2,3})p.WriteByte(n);p.WriteInt(bombs);
        for(int b=0;b<bombs;b++){
            p.WriteInt(bombs);p.WriteInt(2);p.WriteBoolean(true);p.WriteInt(100+b);
            foreach(int n in new[]{123,456,-789,-321,7008})p.WriteInt(n);p.WriteString("lua-đạn");p.WriteInt(250);p.WriteInt(2000);
            p.WriteInt(3);foreach(int type in new[]{5,27,3})foreach(int n in new[]{150,type,11,22,33,44})p.WriteInt(n);
        }
        p.WriteInt(pets?1:0);if(pets)foreach(int n in new[]{2,30,1470,20})p.WriteInt(n);p.WriteInt(pets?1:0);return p;
    }
    static int Main()
    {
        foreach(int wind in new[]{-42,36})foreach(int bombs in new[]{1,3})foreach(bool pets in new[]{false,true}){
            var old=Packet(wind,bombs,pets);int cursor=old.Offset;byte[] original=(byte[])old.Buffer.Clone();var p=Upgrade(old);p.Offset=20;
            E(p.ReadByte(),(byte)2,"shoot command");E(p.ReadInt(),wind,"wind");E(p.ReadBoolean(),wind>0,"wind sign");foreach(byte n in new byte[]{1,2,3})E(p.ReadByte(),n,"wind digit");
            E(p.ReadInt(),0,"wind extension");E(p.ReadInt(),100,"wind factor");E(p.ReadInt(),bombs,"bomb count");
            for(int b=0;b<bombs;b++){
                E(p.ReadInt(),bombs,"number");E(p.ReadInt(),2,"shoot count");E(p.ReadBoolean(),true,"hole");E(p.ReadInt(),100+b,"bomb id");E(p.ReadInt(),-1,"root projectile must render without a parent");
                foreach(int n in new[]{123,456,-789,-321,7008})E(p.ReadInt(),n,"trajectory/template");E(p.ReadString(),"lua-đạn","particle");E(p.ReadInt(),250,"radius");E(p.ReadInt(),2000,"power");
                E(p.ReadInt(),0,"optional action context");E(p.ReadInt(),3,"actions");foreach(int type in new[]{5,27,3}){
                    E(p.ReadInt(),150,"action time");E(p.ReadInt(),type,"action type");E(p.ReadInt(),11,"param1");E(p.ReadInt(),22,"param2");if(type!=27){E(p.ReadInt(),33,"param3");E(p.ReadInt(),44,"param4");}
                }
            }
            E(p.ReadInt(),pets?1:0,"pet action count");if(pets)foreach(int n in new[]{2,30,1470,20})E(p.ReadInt(),n,"pet damage");E(p.ReadInt(),pets?1:0,"pet attack type");E(p.Offset,p.Length,"exact client packet end");E(p.ClientID,7,"id");E(p.Parameter1,7,"shooter");E(p.Parameter2,12,"header");E(old.Offset,cursor,"source cursor");
            for(int i=0;i<old.Length;i++)E(old.Buffer[i],original[i],"source bytes");
#if !LEGACY
            old.WriteByte(0);Reject(old);
#endif
        }
#if !LEGACY
        var wrong=new GSPacketIn(91);wrong.WriteByte(6);E(Object.ReferenceEquals(wrong,Upgrade(wrong)),true,"other command");
        var shortPacket=new GSPacketIn(91);shortPacket.WriteByte(2);Reject(shortPacket);
        var huge=new GSPacketIn(91);huge.WriteByte(2);huge.WriteInt(0);huge.WriteInt(0);huge.WriteInt(int.MaxValue);Reject(huge);
#endif
        Console.WriteLine("PASS: shoot schema renders root bombs and preserves trajectories, typed actions, particles, damage and pet attacks.");return 0;
    }
#if !LEGACY
    static void Reject(GSPacketIn p){try{Upgrade(p);}catch(InvalidDataException){return;}throw new Exception("Malformed shot accepted");}
#endif
}
