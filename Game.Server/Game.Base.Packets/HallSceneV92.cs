using Game.Base.Packets;
using Game.Server.Managers;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using SqlDataProvider.Data;
namespace Game.Server.Packets
{
    public static class HallSceneV92
    {
        public static GSPacketIn CreateSceneList(int playerId, PlayerInfo[] peers)
        {
            var packet = new GSPacketIn(262, playerId);
            packet.WriteByte(0);
            packet.WriteInt(0);
            packet.WriteInt(peers.Length);
            packet.WriteInt(peers.Length);
            foreach (var peer in peers)
            {
                packet.WriteInt(peer.ID); packet.WriteString(peer.NickName);
                packet.WriteInt(peer.VIPLevel); packet.WriteInt(peer.typeVIP);
                packet.WriteBoolean(peer.Sex); packet.WriteString(peer.Style); packet.WriteString(peer.Colors);
                packet.WriteInt(0); packet.WriteInt(0); // unsupported mounts and pets
                packet.WriteInt(0); packet.WriteInt(0); // reserved by the current client
                packet.WriteInt(peer.ConsortiaID); packet.WriteInt(peer.badgeID);
                packet.WriteString(peer.ConsortiaName); packet.WriteString(peer.Honor); packet.WriteInt(peer.honorId);
                packet.WriteBoolean(false); packet.WriteString(""); packet.WriteBoolean(false);
                packet.WriteInt(0); packet.WriteInt(0); packet.WriteInt(0); packet.WriteInt(0);
            }
            return packet;
        }

        public static bool TryHandle(GameClient client, GSPacketIn packet)
        {
            if (packet == null || packet.Code != 262) return false;
            try
            {
                if (client == null || client.Player == null || client.Player.PlayerCharacter == null ||
                    client.TcpEndpoint == "not connected") return true;
                if (packet.DataLeft < 1) return true;
                byte command = packet.ReadByte();
                if (command == 0 && packet.DataLeft == 0)
                {
                    int id = client.Player.PlayerCharacter.ID;
                    var peers = new List<PlayerInfo>();
                    foreach (var player in WorldMgr.GetAllPlayersNoGame())
                    {
                        if (player.PlayerCharacter != null && player.PlayerCharacter.ID != id) peers.Add(player.PlayerCharacter);
                        if (peers.Count == 50) break;
                    }
                    client.SendTCP(CreateSceneList(id, peers.ToArray()));
                }
            }
            catch (Exception error) { Trace.TraceWarning("Hall response failed: {0}", error.GetType().Name); }
            // Movement is simulated locally by this client. Do not reuse combat X/Y
            // fields for hall coordinates or allow this packet to mutate character data.
            return true;
        }
    }
}
