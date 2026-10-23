using System;
using System.Reflection;
using System.Runtime.Serialization;
using Game.Base.Packets;
using Game.Server;
using Game.Server.GameObjects;
using Game.Server.Packets;
using SqlDataProvider.Data;

// Compile with /define:LEGACY to demonstrate the missing-handler failure.
class PetFormsTests
{
    sealed class CaptureClient : GameClient
    {
        public GSPacketIn Last;
        public int Sends;
        public CaptureClient() : base(null, new byte[8192], new byte[8192]) { }
        public override void SendTCP(GSPacketIn packet) { Last = packet; Sends++; }
    }

    static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    static bool Handle(GamePlayer player, GSPacketIn packet)
    {
#if LEGACY
        // The legacy dispatcher has no command 24 handler and produces no reply.
        return false;
#elif PATCHED
        var processor=(Game.Server.Pet.PetLogicProcessor)FormatterServices.GetUninitializedObject(typeof(Game.Server.Pet.PetLogicProcessor));
        processor.OnGameData(player,packet);
        return true;
#else
        return PetFormsV92.TryHandle(player, packet);
#endif
    }

    static GSPacketIn Request(params byte[] payload)
    {
        var packet = new GSPacketIn(68, 999); // Never trust the request's player ID.
        foreach (byte value in payload) packet.WriteByte(value);
        packet.Offset = 20;
        return packet;
    }

    static int Main()
    {
        try { Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
    }

    static void Run()
    {
        var client = new CaptureClient();
        var player = (GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));
        var info = new PlayerInfo();
        info.ID = 228; info.Money = 123; info.Gold = 456; info.GiftToken = 789;
        info.PasswordTwo = "unchanged"; info.CheckCode = "unchanged"; info.CheckError = 3;
        typeof(GamePlayer).GetField("m_character", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, info);
        typeof(GamePlayer).GetField("m_client", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, client);
        client.Player = player;
        var fields = typeof(GamePlayer).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var before = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) before[i] = fields[i].GetValue(player);

        var request = Request(24);
        bool handled = Handle(player, request);
        Assert(client.Sends == 1 && client.Last != null, "Pet forms 68/24 must reply so the bag can finish opening.");
        Assert(handled, "Pet forms request must bypass the legacy dispatcher.");
        Assert(request.Offset == request.Length, "Consume the recognized command.");
        var reply = client.Last;
        Assert(reply.Code == 68 && reply.ClientID == 228, "Reply must use pet packet 68 and the authenticated player ID.");
        reply.Offset = 20;
        Assert(reply.ReadByte() == 24, "Reply must dispatch to the pet forms event.");
        Assert(reply.ReadInt() == 0, "Legacy accounts have zero permanent advanced forms.");
        Assert(reply.ReadInt() == 0, "Legacy accounts have zero timed advanced forms.");
        Assert(reply.Offset == reply.Length, "The client must consume the entire reply without trailing data.");
#if PATCHED
        Console.WriteLine("PASS: deployed PetLogicProcessor dispatches 68/24 through the compatibility handler.");
        return;
#else

        // A different pet command must reach the original handler at its original cursor.
        var unrelated = Request(25, 1, 0, 0, 0, 7);
        int offset = unrelated.Offset;
        Assert(!Handle(player, unrelated) && unrelated.Offset == offset, "Unrelated commands must preserve the read cursor.");
        var shifted = Request(99, 25, 1);
        shifted.Offset = 21;
        Assert(!Handle(player, shifted) && shifted.Offset == 21, "Preserve nonstandard incoming cursors for unrelated commands.");
        var wrongCode = Request(24); wrongCode.Code = 69;
        Assert(!Handle(player, wrongCode) && wrongCode.Offset == 20, "Do not intercept another packet family.");
        Assert(!Handle(player, null), "A null packet must be ignored.");
        var empty = Request();
        Assert(Handle(player, empty), "An empty pet payload must be safely consumed before the legacy byte read.");
        Assert(Handle(player, Request(24, 0)), "Reject trailing bytes without falling through to the legacy dispatcher.");
        var shiftedForms = Request(99, 24); shiftedForms.Offset = 21;
        Assert(Handle(player, shiftedForms), "Reject a forms command outside the packet payload start.");
        Assert(Handle(null, Request(24)), "A missing player must not cause a reply or exception.");
        var noClient = (GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));
        Assert(Handle(noClient, Request(24)), "A missing client must be safely ignored.");
        typeof(GamePlayer).GetField("m_client", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(noClient, client);
        Assert(Handle(noClient, Request(24)), "A missing character must be safely ignored.");
        Assert(client.Sends == 1, "Unrelated, malformed, and unauthenticated requests must not send replies.");
        for (int i = 0; i < fields.Length; i++)
            Assert(object.Equals(before[i], fields[i].GetValue(player)), "Do not mutate player state: " + fields[i].Name);
        Assert(info.Money == 123 && info.Gold == 456 && info.GiftToken == 789 && info.PasswordTwo == "unchanged" && info.CheckCode == "unchanged" && info.CheckError == 3,
            "The forms query must not change currency, inventory security, or CAPTCHA state.");
        Assert(player.EquipBag == null && player.PropBag == null && player.PetBag == null,
            "The forms query must not initialize or award inventory or pets.");
        Console.WriteLine("PASS: strict empty pet-forms reply; unrelated cursors preserved; malformed/null requests ignored; no player/inventory/security mutations.");
#endif
    }
}
