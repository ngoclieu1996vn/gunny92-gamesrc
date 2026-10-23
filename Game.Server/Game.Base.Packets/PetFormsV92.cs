using Game.Base.Packets;
using Game.Server.GameObjects;

namespace Game.Server.Packets
{
    public static class PetFormsV92
    {
        // Run before PetLogicProcessor reads its command byte.
        public static bool TryHandle(GamePlayer player, GSPacketIn packet)
        {
            if (packet == null || packet.Code != 68) return false;
            int offset = packet.Offset;
            if (offset < 20 || packet.DataLeft < 1) return true;
            byte command = packet.ReadByte();
            if (command != 24)
            {
                packet.Offset = offset;
                return false;
            }

            // The current client's forms query contains only the subcommand.
            if (offset != 20 || packet.DataLeft != 0 || player == null ||
                player.Client == null || player.PlayerCharacter == null) return true;

            // Legacy accounts have no advanced permanent or timed pet forms.
            var reply = new GSPacketIn(68, player.PlayerCharacter.ID);
            reply.WriteByte(24);
            reply.WriteInt(0);
            reply.WriteInt(0);
            player.Client.SendTCP(reply);
            return true;
        }
    }
}
