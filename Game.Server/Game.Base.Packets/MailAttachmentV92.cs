using System;
using System.Collections.Generic;
using System.IO;
using Game.Base.Packets;
using Game.Server;

namespace Game.Server.Packets
{
    // Translate only the wire layout; the original handler retains mail ownership,
    // bag-password checks, inventory handling and all database/reward decisions.
    public static class MailAttachmentV92
    {
        static int Int(GSPacketIn p,int offset)
        { if(offset<20 || offset>p.Length-4)throw new InvalidDataException("Truncated mail claim.");return(p.Buffer[offset]<<24)|(p.Buffer[offset+1]<<16)|(p.Buffer[offset+2]<<8)|p.Buffer[offset+3]; }
        public static GSPacketIn[] Decode(GSPacketIn request,int authenticatedPlayerId)
        {
            if(request==null || request.Code!=113 || request.Offset!=20 || request.Length<25 || authenticatedPlayerId<=0)
                throw new InvalidDataException("Invalid mail claim framing.");
            byte kind=request.Buffer[20];int count=Int(request,21);
            if(kind>7 || count<1 || count>100 || request.Length!=25+count*4)
                throw new InvalidDataException("Invalid mail claim selection.");
            var seen=new HashSet<int>();var packets=new GSPacketIn[count];
            for(int i=0;i<count;i++)
            {
                int mailId=Int(request,25+i*4);
                if(mailId<=0 || !seen.Add(mailId))throw new InvalidDataException("Invalid or duplicate mail ID.");
                var p=new GSPacketIn(113,authenticatedPlayerId);p.Parameter1=request.Parameter1;p.Parameter2=request.Parameter2;
                p.WriteInt(mailId);p.WriteByte(kind);p.Offset=20;packets[i]=p;
            }
            return packets;
        }
        public static int Dispatch(GameClient client,GSPacketIn request,Func<GameClient,GSPacketIn,int> legacy)
        {
            if(client==null || client.Player==null || client.Player.PlayerCharacter==null || legacy==null)return 0;
            GSPacketIn[] packets;
            try{packets=Decode(request,client.Player.PlayerCharacter.ID);}catch(InvalidDataException){return 0;}
            int result=0;foreach(var packet in packets)result=legacy(client,packet);return result;
        }
        public static GSPacketIn UpgradeResponse(GSPacketIn original)
        {
            if(original.Code!=113)return original;
            if(original.Length<28)throw new InvalidDataException("Truncated mail claim response.");
            int count=Int(original,24);
            if(count<0 || count>8 || original.Length!=28+count*4)throw new InvalidDataException("Invalid mail claim response.");
            var response=new GSPacketIn(113,original.ClientID);response.Parameter1=original.Parameter1;response.Parameter2=original.Parameter2;
            response.WriteInt(1);var payload=new byte[original.Length-20];System.Buffer.BlockCopy(original.Buffer,20,payload,0,payload.Length);response.Write(payload);
            return response;
        }
    }
}