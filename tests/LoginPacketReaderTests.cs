using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.Base.Packets;
using Game.Server.Packets.Client;

public static class LoginPacketReaderTests
{
    public static int Main()
    {
        try
        {
            foreach (int keySize in new[] { 1024, 2048 })
            using (var rsa = new RSACryptoServiceProvider(keySize))
            {
                byte[] secret = Encoding.UTF8.GetBytes("timestamp-key--user,one-time-key");
                byte[] encrypted = rsa.Encrypt(secret, false);
                foreach (int prefix in new[] { -1, 0, 1 })
                {
                    var packet = MakePacket(prefix, encrypted);
                    LoginPacketReader.Prepare(packet, encrypted.Length);
                    Assert(packet.ReadInt() == 5498628, "Login version must stay aligned: prefix=" + prefix);
                    Assert(packet.ReadInt() == 0, "Client type must stay aligned");
                    byte[] payload = packet.ReadBytes();
                    Assert(payload.Length == encrypted.Length, "RSA ciphertext must be exactly one block");
                    Assert(Convert.ToBase64String(rsa.Decrypt(payload, false)) == Convert.ToBase64String(secret),
                        "Old and new packets must decrypt to the original credentials");
                }
                bool rejected = false;
                try { LoginPacketReader.Prepare(MakePacket(2, encrypted), encrypted.Length); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected, "Invalid change-channel byte must be rejected");
            }
            Console.WriteLine("PASS: legacy/new login headers, RSA 1024/2048, and invalid prefix rejection.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }

    private static GSPacketIn MakePacket(int prefix, byte[] cipher)
    {
        var packet = new GSPacketIn((short)1);
        if (prefix >= 0) packet.WriteByte((byte)prefix);
        packet.WriteInt(5498628);
        packet.WriteInt(0);
        foreach (byte value in cipher) packet.WriteByte(value);
        packet.Offset = 20;
        return packet;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
