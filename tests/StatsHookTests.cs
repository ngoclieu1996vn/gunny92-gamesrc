using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Game.Base.Packets;
using Game.Server;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Packets;
using SqlDataProvider.Data;
using Mono.Cecil;
class StatsHookTests
{
 sealed class Client:GameClient{public GSPacketIn Captured;public Client():base(null,new byte[8192],new byte[8192]){}public override void SendTCP(GSPacketIn p){Captured=p;}}
 static void E(object a,object b,string label){if(!a.Equals(b))throw new Exception(label+": "+a+" != "+b);}
 static void Set(object o,string name,object value){o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);}
 static int Main(string[] args){try{
  var info=new PlayerInfo{ID=228,hp=76543,Texp=new TexpInfo(),explorerManualInfo=new ExplorerManualInfo{MagicAttack=321,MagicResistance=654}};
  var client=new Client();var lib=new AbstractPacketLib(client);
  var response=lib.SendUpdatePublicPlayer(info,null,null);
  E(Object.ReferenceEquals(response,client.Captured),true,"broadcast return must be upgraded too");
  response.Offset=20;for(int i=0;i<12;i++)response.ReadInt();
  E(response.ReadInt(),321,"real patched public writer MA");E(response.ReadInt(),654,"real patched public writer MR");E(response.ReadInt(),76543,"real patched public writer HP");
  var player=(GamePlayer)FormatterServices.GetUninitializedObject(typeof(GamePlayer));Set(player,"m_character",info);
  var properties=(PlayerProperty)FormatterServices.GetUninitializedObject(typeof(PlayerProperty));
  properties.Current=new Dictionary<string,Dictionary<string,int>>{{"MagicAttack",new Dictionary<string,int>{{"Texp",25}}},{"MagicDefence",new Dictionary<string,int>{{"Horse",75}}}};
  Set(player,"m_playerProp",properties);
  E(MagicPowerV92.Apply(player,1000,1000),11853,"enabled AIProbe nonzero magic fixture");
  E(MagicPowerV92.Apply(player,1000,1000),11853,"no cumulative addition across recomputes");
  client.Player=player;info.totemId=10012;
  PlayerPropertyResponseV92.InitializeTotem(client,info);
  E((int)client.Captured.Code,619,"initialize page-grade state before self properties");client.Captured.Offset=20;
  E(client.Captured.ReadInt(),0,"no unsupported page grades");E(client.Captured.DataLeft,0,"exact Totem state framing");E(info.totemId,10012,"base Totem progress retained");
  var captured=client.Captured;PlayerPropertyResponseV92.InitializeTotem(client,new PlayerInfo{ID=227});
  E(Object.ReferenceEquals(captured,client.Captured),true,"viewing another player must not reset self state");
  info.ID=227;E(MagicPowerV92.Apply(player,1000,1000),1000,"other live accounts unchanged");E(info.hp,76543,"HP not mutated");
  using(var assembly=AssemblyDefinition.ReadAssembly(args[0])){
   var update=assembly.MainModule.GetType("Game.Server.GameObjects.GamePlayer").Methods.Single(m=>m.Name=="UpdateFightPower");
   var calls=update.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
   E(calls.Count(m=>m.Name=="GetBaseAttack"),1,"base damage evaluated once");
   E(calls.Count(m=>m.DeclaringType.Name=="MagicPowerV92"&&m.Name=="Apply"),1,"power hook installed once");
   var property=assembly.MainModule.GetType("Game.Base.Packets.AbstractPacketLib").Methods.Single(m=>m.Name=="SendUpdatePlayerProperty");
   E(property.Body.Instructions.Count(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).DeclaringType.Name=="PlayerPropertyResponseV92"&&((MethodReference)i.Operand).Name=="Create"),1,"property hook installed once");
   E(property.Body.Instructions.Count(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="InitializeTotem"),1,"Totem initialization hook installed once");
  }
  Console.WriteLine("PASS: actual patched public writer keeps HP/MA/MR separate for send+broadcast; AIProbe-only power uses real sources once, without HP mutations.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
