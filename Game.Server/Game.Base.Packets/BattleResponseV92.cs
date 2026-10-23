using System;
using System.IO;
using Game.Base.Packets;
namespace Game.Server.Packets
{
    // Applied only on Fight -> Road -> human client; AI's legacy reader is unchanged.
    public static class BattleResponseV92
    {
        public static GSPacketIn UpgradeTurn(GSPacketIn p)
        {
            if(p.Code!=91 || p.Length<=20 || p.Buffer[20]!=6)return p;
            int c=20;var q=new GSPacketIn(p.Code,p.ClientID,Math.Max(10240,p.Length));q.Parameter1=p.Parameter1;q.Parameter2=p.Parameter2;
            Copy(p,q,ref c,"b");q.WriteInt(0); // no scene-effect records in the legacy game
            Copy(p,q,ref c,"bbbb");q.WriteInt(0);q.WriteInt(100);q.WriteInt(0); // wind extension, normal factor
            Copy(p,q,ref c,"bi");int boxes=Int(p,c);Copy(p,q,ref c,"i");
            if(boxes<0 || boxes>4096)throw new InvalidDataException("Invalid turn box count.");
            for(int i=0;i<boxes;i++)Copy(p,q,ref c,"iiii");
            int players=Int(p,c);Copy(p,q,ref c,"i");
            if(players<0 || players>64)throw new InvalidDataException("Invalid turn player count.");
            for(int i=0;i<players;i++)
            {
                Copy(p,q,ref c,"i b i i i b i i i i i");
                q.WriteInt(0);q.WriteInt(0); // pet flag, transformation energy inactive
                Copy(p,q,ref c,"ii");q.WriteInt(0); // shoot/fly counts, optional pet turn counter
            }
            Copy(p,q,ref c,"i");q.WriteBoolean(false); // global turn, no scripted terrain replacement
            if(c!=p.Length)throw new InvalidDataException("Unexpected legacy turn trailer.");
            return q;
        }
        public static GSPacketIn UpgradeStart(GSPacketIn p)
        {
            if(p.Code!=91 || p.Length<=20 || p.Buffer[20]!=99)return p;
            int c=20;var q=new GSPacketIn(p.Code,p.ClientID,Math.Max(10240,p.Length));q.Parameter1=p.Parameter1;q.Parameter2=p.Parameter2;
            Copy(p,q,ref c,"b");int players=Int(p,c);Copy(p,q,ref c,"i");
            if(players<0 || players>64)throw new InvalidDataException("Invalid battle start player count.");
            for(int i=0;i<players;i++)
            {
                Copy(p,q,ref c,"iiiiiiii");q.WriteInt(0); // no deputy-use count in legacy payload
                Copy(p,q,ref c,"ii");q.WriteInt(0);q.WriteInt(0); // power ratio/dander; wish system inactive
                int buffs=Int(p,c);Copy(p,q,ref c,"i");
                if(buffs<0 || buffs>4096)throw new InvalidDataException("Invalid battle buff count.");
                for(int j=0;j<buffs;j++)Copy(p,q,ref c,"ii");
                int turnBuffs=Int(p,c);Copy(p,q,ref c,"i");
                if(turnBuffs<0 || turnBuffs>4096)throw new InvalidDataException("Invalid turn buff count.");
                for(int j=0;j<turnBuffs;j++)Copy(p,q,ref c,"i");
                Copy(p,q,ref c,"bbbb");q.WriteInt(0);q.WriteInt(0); // pet possession inactive
                int properties=Int(p,c);Copy(p,q,ref c,"i");
                if(properties<0 || properties>4096)throw new InvalidDataException("Invalid battle property count.");
                for(int j=0;j<properties;j++)Copy(p,q,ref c,"ss");
                for(int j=0;j<5;j++)q.WriteInt(0); // mark, exit restrictions, spirit skills
            }
            q.WriteInt(0);q.WriteInt(0); // bombs and boxes already on the map
            Copy(p,q,ref c,"d");q.WriteInt(0); // legacy start time, default map card label
            if(c!=p.Length)throw new InvalidDataException("Unexpected legacy battle start trailer.");
            return q;
        }
        public static GSPacketIn UpgradeCreate(GSPacketIn p)
        {
            if(p.Code!=91 || p.Length<=20 || p.Buffer[20]!=101)return p;
            int c=20;var q=new GSPacketIn(p.Code,p.ClientID,Math.Max(10240,p.Length));q.Parameter1=p.Parameter1;q.Parameter2=p.Parameter2;
            Copy(p,q,ref c,"biii");int players=Int(p,c);Copy(p,q,ref c,"i");
            if(players<0 || players>64)throw new InvalidDataException("Invalid battle player count.");
            for(int i=0;i<players;i++)
            {
                Copy(p,q,ref c,"i s i s b");int place=Int(p,c);c+=4;
                Copy(p,q,ref c,"b i b i s s s i i");
                int weapon=Int(p,c);Copy(p,q,ref c,"i");if(weapon!=0)Copy(p,q,ref c,"i s d");
                Copy(p,q,ref c,"i");q.WriteInt(0); // deputy, inactive PvP badge
                Copy(p,q,ref c,"i b i s i i i i i");
                int power=Int(p,c);Copy(p,q,ref c,"i");q.WriteInt(power<0?-1:0);q.WriteInt(power);
                Copy(p,q,ref c,"i i s i s i b i");q.WriteInt(0); // guard core
                Need(p,c,1);bool married=p.Buffer[c]!=0;Copy(p,q,ref c,"b");if(married)Copy(p,q,ref c,"i s");
                Copy(p,q,ref c,"iiiiii");q.WriteInt(place);Copy(p,q,ref c,"iii");
                int pets=Int(p,c);Copy(p,q,ref c,"i");if(pets<0 || pets>16)throw new InvalidDataException("Invalid battle pet count.");
                for(int j=0;j<pets;j++)
                {
                    Copy(p,q,ref c,"i i i s i i");int skills=Int(p,c);Copy(p,q,ref c,"i");
                    if(skills<0 || skills>256)throw new InvalidDataException("Invalid battle pet skill count.");
                    for(int k=0;k<skills;k++)Copy(p,q,ref c,"ii");
                }
                // Unsupported optional systems remain inactive. Bot decisions still
                // run on the Fight server; the legacy packet exposes no robot flag.
                q.WriteInt(0);q.WriteInt(0);q.WriteBoolean(false);
                q.WriteInt(0);q.WriteString("");q.WriteString("");q.WriteBoolean(false);
                for(int j=0;j<4;j++)q.WriteInt(0);
                q.WriteString("");q.WriteString("");for(int j=0;j<6;j++)q.WriteInt(0);
            }
            if(c!=p.Length)throw new InvalidDataException("Unexpected legacy battle trailer.");
            q.WriteBoolean(false);q.WriteInt(0);q.WriteString("");q.WriteInt(0);
            return q;
        }
        static void Need(GSPacketIn p,int c,int n){if(c<0 || n<0 || c>p.Length-n)throw new InvalidDataException("Truncated battle packet.");}
        static int Int(GSPacketIn p,int c){Need(p,c,4);return(p.Buffer[c]<<24)|(p.Buffer[c+1]<<16)|(p.Buffer[c+2]<<8)|p.Buffer[c+3];}
        static void Copy(GSPacketIn p,GSPacketIn q,ref int c,string schema)
        {
            foreach(char type in schema){if(type==' ')continue;int n=type=='i'?4:type=='d'?7:1;
                if(type=='s'){Need(p,c,2);n=2+(p.Buffer[c]<<8)+p.Buffer[c+1];}Need(p,c,n);
                var bytes=new byte[n];System.Buffer.BlockCopy(p.Buffer,c,bytes,0,n);q.Write(bytes);c+=n;}
        }
    }
}
