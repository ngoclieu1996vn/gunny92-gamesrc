using System;
using System.Reflection;
using Game.Base.Packets;
class PublicStatsTests
{
 const string Legacy="i:GP i:Offer i:RichesOffer i:RichesRob i:WinCount i:TotalCount i:EscapeCount i:Attack i:Defence i:Agility i:Luck i:hp i:Hide s:Style s:Colors s:Skin b:IsShowConsortia i:ConsortiaID s:ConsortiaName i:badgeID i:discard1 i:discard2 i:Nimbus s:PvePermission s:fightLibMission i:FightPower i:apprenticeshipState i:masterID s:masterOrApprentices i:graduatesCount s:honourOfMaster i:AchievementPoint s:honor d:LastSpaDate i:charmGP i:charmLevel d:shopFinallyGottenTime i:UseOffer i:dailyScore i:dailyWinCount i:dailyGameCount i:weeklyScore i:weeklyGameCount i:spdTexpExp i:attTexpExp i:defTexpExp i:hpTexpExp i:lukTexpExp i:texpTaskCount i:texpCount d:texpTaskDate i:dungeonFlagCount i:evolutionGrade i:evolutionExp";
 static void E(object actual,object expected,string label){if(!actual.Equals(expected))throw new Exception(label+": "+actual+" != "+expected);}
 static GSPacketIn Original(){var p=new GSPacketIn(67,228);p.Parameter1=19;p.Parameter2=23;int n=100;
 foreach(string field in Legacy.Split(' ')){char t=field[0];string name=field.Substring(2);if(t=='i')p.WriteInt(name=="hp"?76543:name=="dungeonFlagCount"?0:++n);else if(t=='s')p.WriteString("Việt-"+name);else if(t=='b')p.WriteBoolean(true);else p.WriteDateTime(new DateTime(2026,10,23,10,20,30));}p.Offset=20;return p;}
 static int Main(string[] args){try{
 var type=Assembly.LoadFrom(args[0]).GetType("Game.Server.Packets.PublicStatsV92");if(type==null)throw new Exception("Public stats compatibility adapter missing: old HP is read as MagicAttack by current client.");
 var original=Original();var p=(GSPacketIn)type.GetMethod("Upgrade").Invoke(null,new object[]{original,321,654,888,77});p.Offset=20;
 E(p.ClientID,228,"header player");E(p.Parameter1,19,"header parameter");E(original.Offset,20,"original cursor");
 E(p.ReadInt(),101,"GP");E(p.ReadInt(),0,"new king grade");
 for(int i=102;i<=111;i++)E(p.ReadInt(),i,"legacy property");
 E(p.ReadInt(),321,"MagicAttack must not read HP");E(p.ReadInt(),654,"MagicDefence must not read Hide");E(p.ReadInt(),76543,"HP retained exactly");
 E(p.ReadInt(),112,"Hide remains aligned");E(p.ReadString(),"Việt-Style","Unicode style");E(p.ReadString(),"Việt-Colors","colors");E(p.ReadString(),"Việt-Skin","skin");E(p.ReadBoolean(),true,"flag");
 // Parse the actual client contract remainder, including 64-bit display power and trailers.
 var tail="i:ConsortiaID s:ConsortiaName i:badgeID i:discard1 i:discard2 i:Nimbus s:PvePermission s:fightLibMission i:FightPower l:FightPower_Show i:apprenticeshipState i:masterID s:masterOrApprentices i:graduatesCount s:honourOfMaster i:AchievementPoint s:honor i:honorId d:LastSpaDate i:charmGP i:charmLevel d:shopFinallyGottenTime i:UseOffer i:dailyScore i:dailyWinCount i:dailyGameCount i:weeklyScore i:weeklyGameCount i:leagueMoney i:spdTexpExp i:attTexpExp i:defTexpExp i:hpTexpExp i:lukTexpExp i:magicAtkTexpExp i:magicDefTexpExp i:texpTaskCount i:texpCount i:magicTexpCount d:texpTaskDate i:dungeonFlagCount s:PveEpicPermission i:evolutionGrade i:evolutionExp i:desertEnterCount i:BattleCount i:VipIntegral i:RingExp i:guardCoreID i:guardCoreGrade i:teamID s:teamName s:teamTag";
 foreach(string f in tail.Split(' ')){char t=f[0];string name=f.Substring(2);if(t=='i'){int v=p.ReadInt();if(name=="RingExp")E(v,888,name);if(name=="honorId")E(v,77,name);}else if(t=='l'){p.ReadInt();p.ReadInt();}else if(t=='s')p.ReadString();else p.ReadDateTime();}
 for(int i=0;i<34;i++)E(p.ReadInt(),0,"unsupported trailer");
 E(p.DataLeft,0,"actual client reads to exact packet end");
 var shortPacket=new GSPacketIn(67,228);shortPacket.Write(original.Buffer,20,original.Length-21);bool rejected=false;try{type.GetMethod("Upgrade").Invoke(null,new object[]{shortPacket,0,0,0,0});}catch(TargetInvocationException){rejected=true;}E(rejected,true,"truncated legacy packet rejected");
 Console.WriteLine("PASS: public stats keep HP and magic separate, preserve headers/UTF8, satisfy current client schema and reject truncation.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
