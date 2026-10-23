using System;
using System.IO;
using Game.Base.Packets;

namespace Game.Server.Packets
{
    // PlayerManager.__updateInventorySlot in the current Flash client reads these
    // extra fields for EACH occupied slot, before it can add that item to its bag.
    public static class InventoryResponseV92
    {
        public static GSPacketIn Upgrade(GSPacketIn original)
        {
            if(original.Code!=64)return original;
            int cursor=GSPacketIn.HDR_SIZE;
            var result=new GSPacketIn(original.Code,original.ClientID,Math.Max(10240,original.Length));
            result.Parameter1=original.Parameter1;result.Parameter2=original.Parameter2;
            int bag=Int(original,cursor);Copy(original,result,ref cursor,"i");
            int count=Int(original,cursor);Copy(original,result,ref cursor,"i");
            if(count<0 || count>(original.Length-cursor)/5)throw new InvalidDataException("Invalid inventory slot count.");
            for(int i=0;i<count;i++)
            {
                Copy(original,result,ref cursor,"i");
                Need(original,cursor,1);bool occupied=original.Buffer[cursor]!=0;
                Copy(original,result,ref cursor,"b");if(!occupied)continue;
                Copy(original,result,ref cursor,"iiiiiiiiiii bb d i s s b");
                Copy(original,result,ref cursor,"iiiiii s i d i b i b i i b");
                Need(original,cursor,1);bool gold=original.Buffer[cursor]!=0;
                Copy(original,result,ref cursor,"b");if(gold)Copy(original,result,ref cursor,"id");
                Copy(original,result,ref cursor,"ssd");
                // Magic stone/jade, locking and enchantment are not implemented in
                // this legacy backend. Keep them inactive without changing stored data.
                result.WriteInt(0);result.WriteInt(0);result.WriteBoolean(false);
                result.WriteInt(0);result.WriteInt(0);result.WriteInt(0);result.WriteString("");
                if(bag!=52){result.WriteInt(0);result.WriteInt(0);result.WriteString("");result.WriteInt(0);}
            }
            if(cursor!=original.Length)throw new InvalidDataException("Unexpected legacy inventory trailer.");
            return result;
        }
        static void Need(GSPacketIn p,int cursor,int size)
        {if(cursor<0 || size<0 || cursor>p.Length-size)throw new InvalidDataException("Truncated legacy inventory.");}
        static int Int(GSPacketIn p,int cursor)
        {Need(p,cursor,4);return (p.Buffer[cursor]<<24)|(p.Buffer[cursor+1]<<16)|(p.Buffer[cursor+2]<<8)|p.Buffer[cursor+3];}
        static void Copy(GSPacketIn p,GSPacketIn result,ref int cursor,string schema)
        {
            foreach(char type in schema)
            {
                if(type==' ')continue;
                int size=type=='i'?4:type=='d'?7:1;
                if(type=='s'){Need(p,cursor,2);size=2+(p.Buffer[cursor]<<8)+p.Buffer[cursor+1];}
                Need(p,cursor,size);var bytes=new byte[size];System.Buffer.BlockCopy(p.Buffer,cursor,bytes,0,size);
                result.Write(bytes);cursor+=size;
            }
        }
    }
}
