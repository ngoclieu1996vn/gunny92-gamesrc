using System;
using System.IO;
using System.Collections.Generic;
using Game.Base.Packets;
using Game.Server.Managers;
using SqlDataProvider.Data;
namespace Game.Server.Packets
{
 public static class PublicStatsV92
 {
  const string Legacy="i:GP i:Offer i:RichesOffer i:RichesRob i:WinCount i:TotalCount i:EscapeCount i:Attack i:Defence i:Agility i:Luck i:hp i:Hide s:Style s:Colors s:Skin b:IsShowConsortia i:ConsortiaID s:ConsortiaName i:badgeID i:discard1 i:discard2 i:Nimbus s:PvePermission s:fightLibMission i:FightPower i:apprenticeshipState i:masterID s:masterOrApprentices i:graduatesCount s:honourOfMaster i:AchievementPoint s:honor d:LastSpaDate i:charmGP i:charmLevel d:shopFinallyGottenTime i:UseOffer i:dailyScore i:dailyWinCount i:dailyGameCount i:weeklyScore i:weeklyGameCount i:spdTexpExp i:attTexpExp i:defTexpExp i:hpTexpExp i:lukTexpExp i:texpTaskCount i:texpCount d:texpTaskDate i:dungeonFlagCount i:evolutionGrade i:evolutionExp";
  static readonly string Client=Legacy.Replace("i:GP ","i:GP i:ddtKingGrade ")
   .Replace("i:Luck ","i:Luck i:MagicAttack i:MagicDefence ")
   .Replace("i:FightPower ","i:FightPower l:FightPower_Show ")
   .Replace("s:honor ","s:honor i:honorId ")
   .Replace("i:weeklyGameCount ","i:weeklyGameCount i:leagueMoney ")
   .Replace("i:lukTexpExp ","i:lukTexpExp i:magicAtkTexpExp i:magicDefTexpExp ")
   .Replace("i:texpCount ","i:texpCount i:magicTexpCount ")
   .Replace("i:dungeonFlagCount ","i:dungeonFlagCount s:PveEpicPermission ")+
   " i:desertEnterCount i:BattleCount i:VipIntegral i:RingExp i:guardCoreID i:guardCoreGrade i:teamID s:teamName s:teamTag"+
   " i:teamGrade i:teamWinTime i:teamTotalTime i:teamDivision i:teamScore i:teamDuty i:teamPersonalScore i:magicWardrobeID i:critTexpExp i:sunderArmorTexpExp i:critDmgTexpExp i:speedTexpExp i:uniqueSkillTexpExp i:dmgTexpExp i:armorDefTexpExp i:nsTexpCount i:avoidInjury i:mustResist i:toughness i:Guard i:fireResist i:waterResist i:windResist i:soilResist i:lightResist i:darkResist i:thirdTexpCount i:elfTempId i:swornExp i:elfEquipType i:elfEquipQua i:outSideDRStage i:inherentDRStage i:soulDRStage";
  public static GSPacketIn UpgradePublic(GSPacketIn original,PlayerInfo info)
  {
   var player=WorldMgr.GetPlayerById(info.ID);
   var props=player==null?null:player.PlayerProp;
   return Upgrade(original,MagicPowerV92.TotalMagic(info,props,false),MagicPowerV92.TotalMagic(info,props,true),info.RingExp,info.honorId);
  }
  public static GSPacketIn Upgrade(GSPacketIn original,int magicAttack,int magicDefence,int ringExp,int honorId)
  {
   if(original.Code!=67)return original;
   var values=new Dictionary<string,byte[]>();int offset=20;
   foreach(string field in Legacy.Split(' ')){
    char t=field[0];int size=t=='i'?4:t=='d'?7:t=='b'?1:2;
    Need(original,offset,size);
    if(t=='s'){size=2+(original.Buffer[offset]<<8)+original.Buffer[offset+1];Need(original,offset,size);}
    var bytes=new byte[size];System.Buffer.BlockCopy(original.Buffer,offset,bytes,0,size);values.Add(field.Substring(2),bytes);offset+=size;
   }
   if(offset!=original.Length || BitConverter.ToInt32(values["dungeonFlagCount"],0)!=0)throw new InvalidDataException("Unexpected public stats trailer.");
   values["MagicAttack"]=IntBytes(magicAttack);values["MagicDefence"]=IntBytes(magicDefence);
   values["RingExp"]=IntBytes(ringExp);values["honorId"]=IntBytes(honorId);
   var display=new byte[8];if((values["FightPower"][0]&128)!=0)for(int i=0;i<4;i++)display[i]=255;
   System.Buffer.BlockCopy(values["FightPower"],0,display,4,4);values["FightPower_Show"]=display;
   var result=new GSPacketIn(67,original.ClientID);result.Parameter1=original.Parameter1;result.Parameter2=original.Parameter2;
   foreach(string field in Client.Split(' ')){
    byte[] bytes;if(values.TryGetValue(field.Substring(2),out bytes))result.Write(bytes);
    else if(field[0]=='s')result.WriteShort(0);else result.WriteInt(0);
   }
   return result;
  }
  static byte[] IntBytes(int value){return new[]{(byte)(value>>24),(byte)(value>>16),(byte)(value>>8),(byte)value};}
  static void Need(GSPacketIn p,int offset,int size){if(offset<20||size<0||offset>p.Length-size)throw new InvalidDataException("Truncated public stats.");}
 }
}
