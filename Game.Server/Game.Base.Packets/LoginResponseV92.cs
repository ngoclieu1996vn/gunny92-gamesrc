using System;
using System.Collections.Generic;
using System.IO;
using Game.Base.Packets;

namespace Game.Server.Packets
{
    // Wire compatibility only: preserve persisted legacy fields; unsupported systems stay inactive.
    public static class LoginResponseV92
    {
        public const string LegacySchema = "b:status i:ZoneID i:Attack i:Defence i:Agility i:Luck i:GP i:Repute i:Gold i:Money i:medal i:Score i:Hide i:FightPower i:apprenticeshipState i:masterID s:masterOrApprentices i:graduatesCount s:honourOfMaster d:freezesDate b:typeVIP i:VIPLevel i:VIPExp d:VIPExpireDay d:LastDate i:VIPNextLevelDaysNeeded d:systemDate b:canTakeVipReward i:OptionOnOff i:AchievementPoint s:honor i:honorId i:totalGameTime b:Sex s:styleColors s:Skin i:ConsortiaID s:ConsortiaName i:badgeID i:DutyLevel s:DutyName i:Right s:ChairmanName i:Honor i:Riches b:bagPwdState s:questionOne s:questionTwo i:leftTimes s:LoginName i:Nimbus s:PvePermission s:fightLibMission i:userGuildProgress i:BoxProgression i:GetBoxLevel i:AlreadyGetBox d:LastSpaDate d:shopFinallyGottenTime i:UseOffer i:dailyScore i:dailyWinCount i:dailyGameCount b:DailyLeagueFirst i:DailyLeagueLastScore i:weeklyScore i:weeklyGameCount i:weeklyRanking i:spdTexpExp i:attTexpExp i:defTexpExp i:hpTexpExp i:lukTexpExp i:texpTaskCount i:texpCount d:texpTaskDate i:fineSuitExp b:isOldPlayerHasValidEquitAtLogin i:badLuckNumber i:luckyNum d:lastLuckyNumDate i:lastLuckNum i:accumulativeLoginDays i:accumulativeAwardDays i:totemId i:necklaceExp i:RingExp i:manual_Level i:pro_Agile i:pro_Armor i:pro_Attack i:pro_Damage i:pro_Defense i:pro_HP i:pro_Lucky i:pro_MagicAttack i:pro_MagicResistance i:pro_Stamina";
        public const string ClientSchema = "b:status i:ZoneID i:Attack i:Defence i:Agility i:Luck i:GP i:Repute i:Gold i:Money i:DDTMoney i:BandMoney i:Score i:Hide i:FightPower l:FightPower_Show i:apprenticeshipState i:masterID s:masterOrApprentices i:graduatesCount s:honourOfMaster d:freezesDate b:typeVIP i:VIPLevel i:VIPExp d:VIPExpireDay d:LastDate i:VIPNextLevelDaysNeeded d:systemDate b:canTakeVipReward i:OptionOnOff i:AchievementPoint s:honor i:honorId i:totalGameTime b:Sex s:styleColors s:Skin i:ConsortiaID s:ConsortiaName i:badgeID i:DutyLevel s:DutyName i:Right s:ChairmanName i:Honor i:Riches b:bagPwdState s:questionOne s:questionTwo i:leftTimes s:LoginName i:Nimbus s:PvePermission s:fightLibMission i:userGuildProgress d:LastSpaDate d:shopFinallyGottenTime i:UseOffer i:dailyScore i:dailyWinCount i:dailyGameCount b:DailyLeagueFirst i:DailyLeagueLastScore i:weeklyScore i:weeklyGameCount i:weeklyRanking i:spdTexpExp i:attTexpExp i:defTexpExp i:hpTexpExp i:lukTexpExp i:magicAtkTexpExp i:magicDefTexpExp i:texpTaskCount i:texpCount i:magicTexpCount d:texpTaskDate b:isOldPlayerHasValidEquitAtLogin i:badLuckNumber i:luckyNum d:lastLuckyNumDate i:lastLuckNum b:isOld b:isOld2 i:CardSoul i:GetSoulCount i:uesedFinishTime i:totemId i:necklaceExp i:necklaceCastLevel i:accumulativeLoginDays i:accumulativeAwardDays i:discard1 i:discard2 i:discard3 i:isFirstDivorce s:PveEpicPermission i:MountsType i:PetsID i:horseRaceCanRaceTime b:isAttest s:ImagePath i:SubID s:SubName b:IsShow b:discard4 d:createPlayerDate i:vipDiscount d:vipDiscountValidity i:ddtkingDiscount d:ddtkingDiscountValidity i:stive i:manual_Level i:pro_Agile i:pro_Armor i:pro_Attack i:pro_Damage i:pro_Defense i:pro_HP i:pro_Lucky i:pro_MagicAttack i:pro_MagicResistance i:pro_Stamina i:teamID s:teamName s:teamTag i:teamGrade i:teamWinTime i:teamTotalTime i:teamDivision i:teamScore i:teamDuty i:teamPersonalScore i:freeInvitedUsedCnt i:critTexpExp i:sunderArmorTexpExp i:critDmgTexpExp i:speedTexpExp i:uniqueSkillTexpExp i:dmgTexpExp i:armorDefTexpExp i:nsTexpCount i:avoidInjury i:mustResist i:toughness i:Guard i:fireResist i:waterResist i:windResist i:soilResist i:lightResist i:darkResist i:thirdTexpCount s:hasMagicIDStr";

        public static GSPacketIn Upgrade(GSPacketIn original, int giftToken)
        {
            if (original.Code != 1 && original.Code != 38) return original;
            string oldSchema = original.Code == 1 ? LegacySchema :
                "i:Money i:medal i:Score i:Gold i:GiftToken i:badLuckNumber i:petScore i:hardCurrency i:damageScores i:myHonor i:jampsCurrency";
            string newSchema = original.Code == 1 ? ClientSchema :
                "i:Money i:DDTMoney i:BandMoney i:Score i:Gold i:badLuckNumber i:damageScores i:discard i:myHonor i:hardCurrency i:jampsCurrency i:jampsBattleCount";
            var values = new Dictionary<string, byte[]>();
            int offset = GSPacketIn.HDR_SIZE;
            foreach (string field in oldSchema.Split(' '))
            {
                int size = Size(field[0], original.Buffer, offset, original.Length);
                if (offset + size > original.Length) throw new InvalidDataException("Truncated legacy login response: " + field);
                byte[] bytes = new byte[size];
                System.Buffer.BlockCopy(original.Buffer, offset, bytes, 0, size);
                values.Add(field.Substring(2), bytes);
                offset += size;
            }
            if (offset != original.Length) throw new InvalidDataException("Unexpected legacy login response length.");
            // The legacy backend has GiftToken but no bound-premium balance. DDTMoney is the
            // legacy gift currency; never display inventory medals as spendable money.
            values["DDTMoney"] = original.Code == 38 ? values["GiftToken"] : IntBytes(giftToken);
            values["BandMoney"] = IntBytes(0);
            if (original.Code == 1)
            {
                byte[] displayPower = new byte[8];
                System.Buffer.BlockCopy(values["FightPower"], 0, displayPower, 4, 4);
                values["FightPower_Show"] = displayPower;
            }
            var result = new GSPacketIn(original.Code, original.ClientID);
            result.Parameter1 = original.Parameter1;
            result.Parameter2 = original.Parameter2;
            foreach (string field in newSchema.Split(' '))
            {
                byte[] bytes;
                if (values.TryGetValue(field.Substring(2), out bytes)) result.Write(bytes);
                else if (field[0] == 's') result.WriteString("");
                else if (field[0] == 'd') result.WriteDateTime(new DateTime(2000, 1, 1));
                else result.Write(new byte[Size(field[0], null, 0, 0)]);
            }
            return result;
        }

        private static byte[] IntBytes(int value)
        {
            return new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
        }

        public static GSPacketIn CreateKingBlessState(int playerId)
        {
            // No active subscription is implemented by this backend. The client still
            // requires an initialized expiry date before constructing its hall toolbar.
            var packet = new GSPacketIn(142, playerId);
            packet.WriteInt(0);
            packet.WriteDateTime(new DateTime(2000, 1, 1));
            return packet;
        }

        private static int Size(char type, byte[] buffer, int offset, int length)
        {
            if (type == 'i') return 4;
            if (type == 'b') return 1;
            if (type == 'd') return 7;
            if (type == 'l') return 8;
            if (type == 's' && offset + 2 <= length)
                return 2 + ((buffer[offset] << 8) | buffer[offset + 1]);
            throw new InvalidDataException("Invalid legacy response field.");
        }
    }
}
