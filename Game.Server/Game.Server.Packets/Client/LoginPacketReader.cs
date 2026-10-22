using Game.Base.Packets;
using System.IO;

namespace Game.Server.Packets.Client
{
    public static class LoginPacketReader
    {
        public static void Prepare(GSPacketIn packet, int rsaBlockBytes)
        {
            // New clients prepend isChangeChannel before version and desktopType.
            // The RSA block size distinguishes this layout from the legacy layout.
            if (packet.Length - packet.Offset == rsaBlockBytes + 9)
            {
                byte changeChannel = packet.ReadByte();
                if (changeChannel > 1)
                    throw new InvalidDataException("Invalid login change-channel flag.");
            }
        }
    }
}
