using System;
using System.Collections.Generic;
using Game.Base.Packets;
using Game.Server.GameUtils;
using SqlDataProvider.Data;

namespace Game.Server.Packets
{
    // Wire layout of PlayerManager.__updatePlayerProperty in the v92 client.
    // Its unsupported feature fields must still be present, including empty
    // strings and collection counts: omitting one shifts every later property.
    public static class PlayerPropertyResponseV92
    {
        // Legacy backend has base Totem progression, but no page-grade upgrades.
        // Initialize the separate upgrade dictionary before the property reader uses it.
        public static void InitializeTotem(GameClient client, PlayerInfo info)
        {
            if(client==null || client.Player==null || client.Player.PlayerCharacter==null ||
               info==null || client.Player.PlayerCharacter.ID!=info.ID)return;
            var state=new GSPacketIn(619,info.ID);state.WriteInt(0);client.SendTCP(state);
        }
        public static GSPacketIn Create(PlayerInfo info, PlayerProperty properties)
        {
            return Create(info, properties, properties != null && properties.Player != null
                ? properties.Player.ZoneId : GameServer.Instance.Configuration.ServerID);
        }

        public static GSPacketIn Create(PlayerInfo info, PlayerProperty properties, int zoneId)
        {
            if (info == null) throw new ArgumentNullException("info");
            if (properties == null) throw new ArgumentNullException("properties");
            var packet = new GSPacketIn(164, info.ID);
            packet.WriteInt(info.ID);
            packet.WriteInt(zoneId);

            foreach (string attribute in new[] { "Attack", "Defence", "Agility", "Luck" })
            {
                packet.WriteInt(0); // unused base value
                Write(packet, properties, attribute, "Texp", "Card", "Pet", "Suit", "Gem", "Bead",
                    "Avatar", "MagicStone", "Temple", "PetAtlas", "ElfEquip");
                Zeros(packet, 2); // consortia extended skills
            }
            Write(packet, properties, "MagicAttack", "MagicStone", "Horse", "HorsePicCherish", "Enchant", "Suit", "Texp", "Card");
            Write(packet, properties, "MagicDefence", "MagicStone", "Horse", "HorsePicCherish", "Enchant", "Suit", "Temple", "Texp", "Card");

            packet.WriteInt(0); // unused HP base value
            Write(packet, properties, "HP", "Texp", "Pet", "Bead", "Suit", "Gem", "Avatar", "Horse", "HorsePicCherish", "Temple", "ElfEquip");
            Zeros(packet, 2); // consortia extended skills
            Write(packet, properties, "HP", "petChar");

            Write(packet, properties, "Damage", "Suit");
            Write(packet, properties, "Armor", "Suit");
            Write(packet, properties, "Damage", "Bead", "Avatar", "Horse", "HorsePicCherish", "Texp", "ElfEquip", "petChar", "Pet");
            Write(packet, properties, "Armor", "Bead", "Avatar", "Horse", "HorsePicCherish", "Temple", "Texp", "Pet", "petChar");
            packet.WriteInt(properties.totalDamage);
            packet.WriteInt(properties.totalArmor);
            packet.WriteInt(info.fineSuitExp);
            Write(packet, properties, "HP", "CardAchievement");
            Write(packet, properties, "Damage", "CardAchievement");
            Write(packet, properties, "Armor", "CardAchievement");

            foreach (string attribute in new[] { "Damage", "Armor", "Attack", "Defence", "Agility", "Luck", "MagicAttack", "MagicDefence", "HP" })
                Write(packet, properties, attribute, "Mark");
            // The reader reads these twice and overwrites its first mark slots.
            Write(packet, properties, "Damage", "Mark");
            Write(packet, properties, "Armor", "Mark");
            Zeros(packet, 6); // mark king: Attack, Defence, Agility, Luck, MA, MD
            Zeros(packet, 4); // titles: Attack, Defence, Agility, Luck
            Zeros(packet, 6); // digging: Attack, Defence, Agility, Luck, MA, MD
            Zeros(packet, 2); // magic house MA, MD (client multiplies these by ten)
            Zeros(packet, 9); // Crit, Tenacity, SunderArmor, AvoidInjury, Kill,
                              // WillFight, ViolenceInjury, Speed, Guard
            foreach (string attribute in new[] { "Attack", "Defence", "MagicAttack", "Agility", "Luck", "MagicDefence", "Damage", "Armor", "HP" })
                Write(packet, properties, attribute, "SeloPet");

            packet.WriteInt(0); // card level
            packet.WriteShort(0); // empty UTF main card properties
            packet.WriteShort(0); // empty UTF card suit properties
            packet.WriteInt(0); // honor level
            packet.WriteInt(0); // honor HP
            packet.WriteInt(0); // necklace casting HP
            Zeros(packet, 12); // six elemental attack and six resistance values
            Zeros(packet, 2); // two absent emblem skill IDs (no conditional payload)
            packet.WriteInt(0); // culture entry count
            packet.WriteInt(0); // culture damage
            packet.WriteShort(0); // empty UTF spirit stone properties
            Zeros(packet, 2); // plot damage, defence
            packet.WriteInt(0); // soul mark entry count
            Zeros(packet, 5); // soul mark main/sub IDs, type, PVP main/sub IDs
            packet.WriteShort(0); // empty UTF horse core properties
            packet.WriteShort(0); // empty UTF horse core extended skills
            Zeros(packet, 9); // forces relic: Attack, Defence, Agility, Luck,
                              // MA, MD, Damage, Armor, HP
            Zeros(packet, 2); // horse core real damage and real defence additions
            Zeros(packet, 3); // realAttack, realDefence, highSpeed
            return packet;
        }

        private static void Write(GSPacketIn packet, PlayerProperty properties, string attribute, params string[] sources)
        {
            foreach (string source in sources)
            {
                // Physical and HP dictionaries are source -> attribute, while
                // magic, damage and armor dictionaries are attribute -> source.
                bool byAttribute = attribute == "MagicAttack" || attribute == "MagicDefence"
                    || attribute == "Damage" || attribute == "Armor";
                string outer = byAttribute ? attribute : source;
                string inner = byAttribute ? source : attribute;
                Dictionary<string, int> values;
                int value;
                packet.WriteInt(properties.Current != null && properties.Current.TryGetValue(outer, out values)
                    && values != null && values.TryGetValue(inner, out value) ? value : 0);
            }
        }

        private static void Zeros(GSPacketIn packet, int count)
        {
            for (int index = 0; index < count; index++) packet.WriteInt(0);
        }
    }
}
