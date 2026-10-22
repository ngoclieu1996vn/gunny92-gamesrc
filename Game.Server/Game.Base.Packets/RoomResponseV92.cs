using Game.Base.Packets;
using System.IO;
namespace Game.Server.Packets
{
    public static class RoomResponseV92
    {
        public static GSPacketIn UpgradeCreate(GSPacketIn packet)
        {
            CheckKind(packet, 0);
            int offset = 21;
            Skip(packet, ref offset, 16); // id, type/hard/time/counts, lock, map, started
            SkipFields(packet, ref offset, "s b i b b b s");
            if (offset != packet.Length) throw new InvalidDataException("Unexpected legacy room creation payload length.");
            var result = Header(packet);
            result.Write(packet.Buffer, 20, packet.Length - 20);
            result.WriteBoolean(false); // no direct game-loading transition for a newly created room
            result.WriteInt(0); // current client room parameter
            return result;
        }

        public static GSPacketIn UpgradePlayer(GSPacketIn packet)
        {
            CheckKind(packet, 4);
            int offset = 20;
            var result = Header(packet);
            Copy(packet, result, ref offset, 9); // flags/place/team + grade
            result.WriteInt(0); // DDT King grade is not supported by the legacy character model.
            Copy(packet, result, ref offset, 20); // offer/hide/repute/ping/zone
            result.WriteInt(0); result.WriteInt(0); result.WriteString(""); // activity/pet/peak skills
            int recordStart = offset;
            SkipFields(packet, ref offset, "i s b i b s s s i i i s i i i i i i");
            Require(packet, offset, 1);
            byte married = packet.Buffer[offset++];
            if (married > 1) throw new InvalidDataException("Invalid legacy spouse flag.");
            if (married != 0) SkipFields(packet, ref offset, "i s");
            SkipFields(packet, ref offset, "s i i i i s i s b i");
            result.Write(packet.Buffer, recordStart, offset - recordStart);
            result.WriteInt(0); result.WriteBoolean(false); // guard core / old player
            int petStart = offset;
            int pets = Count(packet, ref offset, 1);
            for (int index = 0; index < pets; index++)
            {
                SkipFields(packet, ref offset, "i i i s i i");
                int skills = Count(packet, ref offset, 64);
                Skip(packet, ref offset, skills * 8);
            }
            if (offset != packet.Length) throw new InvalidDataException("Unexpected legacy room player payload length.");
            result.Write(packet.Buffer, petStart, offset - petStart);
            result.WriteInt(0); result.WriteInt(0); // room border / elf
            return result;
        }

        private static void CheckKind(GSPacketIn packet, byte command)
        {
            Require(packet, 20, 1);
            if (packet.Code != 94 || packet.Buffer[20] != command)
                throw new InvalidDataException("Unexpected room packet kind.");
        }
        private static GSPacketIn Header(GSPacketIn packet)
        {
            var result = new GSPacketIn(packet.Code, packet.ClientID);
            result.Parameter1 = packet.Parameter1; result.Parameter2 = packet.Parameter2;
            return result;
        }
        private static void Require(GSPacketIn packet, int offset, int size)
        {
            if (offset < 20 || size < 0 || size > packet.Length - offset)
                throw new InvalidDataException("Truncated legacy room payload.");
        }
        private static void Skip(GSPacketIn packet, ref int offset, int size)
        { Require(packet, offset, size); offset += size; }
        private static void Copy(GSPacketIn packet, GSPacketIn result, ref int offset, int size)
        { Require(packet, offset, size); result.Write(packet.Buffer, offset, size); offset += size; }
        private static void SkipFields(GSPacketIn packet, ref int offset, string fields)
        {
            foreach (char type in fields)
            {
                if (type == ' ') continue;
                if (type == 'i') Skip(packet, ref offset, 4);
                else if (type == 'b') Skip(packet, ref offset, 1);
                else if (type == 's')
                {
                    Require(packet, offset, 2);
                    int size = 2 + ((packet.Buffer[offset] << 8) | packet.Buffer[offset + 1]);
                    Skip(packet, ref offset, size);
                }
                else throw new InvalidDataException("Unknown room field type.");
            }
        }
        private static int Count(GSPacketIn packet, ref int offset, int maximum)
        {
            Require(packet, offset, 4);
            int count = (packet.Buffer[offset] << 24) | (packet.Buffer[offset + 1] << 16) |
                (packet.Buffer[offset + 2] << 8) | packet.Buffer[offset + 3];
            offset += 4;
            if (count < 0 || count > maximum) throw new InvalidDataException("Invalid room collection count.");
            return count;
        }
    }
}
