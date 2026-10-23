using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PatchMatchmaking
{
    static MethodReference Import(ModuleDefinition target,AssemblyDefinition helper,string type,string name)
    { return target.ImportReference(helper.MainModule.GetType(type).Methods.Single(m=>m.Name==name)); }
    static void Body(MethodDefinition method,params Instruction[] instructions)
    { method.Body=new MethodBody(method); foreach(var i in instructions)method.Body.Instructions.Add(i); }
    static void ExpandBranches(MethodDefinition method)
    {
        foreach(var i in method.Body.Instructions)
            if(i.OpCode.OperandType==OperandType.ShortInlineBrTarget)
            {
                string name=i.OpCode.Name.Substring(0,i.OpCode.Name.Length-2);
                i.OpCode=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).Single(o=>o.Name==name);
            }
    }
    static int Main(string[] args)
    {
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(args[0]));
        using(var road=AssemblyDefinition.ReadAssembly(args[0],new ReaderParameters{AssemblyResolver=resolver}))
        using(var helper=AssemblyDefinition.ReadAssembly(args[1]))
        using(var fight=AssemblyDefinition.ReadAssembly(args[2],new ReaderParameters{AssemblyResolver=resolver}))
        using(var fightHelper=AssemblyDefinition.ReadAssembly(args[3]))
        {
            var m=road.MainModule; const string h="Game.Server.Packets.MatchmakingV92";
            if(m.AssemblyReferences.Any(a=>a.Name=="Game.Matchmaking.V92"))throw new Exception("Already patched Road input.");
            var packetLib=m.GetType("Game.Base.Packets.AbstractPacketLib");
            var publicStats=packetLib.Methods.Single(x=>x.Name=="SendUpdatePublicPlayer");
            var publicSend=publicStats.Body.Instructions.Single(x=>x.Operand is MethodReference&&((MethodReference)x.Operand).Name=="SendTCP");
            var loadPacket=publicSend.Previous;var publicStart=loadPacket.Previous;
            if(publicStart.OpCode!=OpCodes.Ldarg_0)throw new Exception("Unexpected public stats send layout.");
            VariableDefinition packetVariable;
            if(loadPacket.OpCode==OpCodes.Ldloc_0)packetVariable=publicStats.Body.Variables[0];
            else if(loadPacket.OpCode==OpCodes.Ldloc_1)packetVariable=publicStats.Body.Variables[1];
            else if(loadPacket.Operand is VariableDefinition)packetVariable=(VariableDefinition)loadPacket.Operand;
            else throw new Exception("Public stats packet must be local.");
            var publicPrefix=new[]{Instruction.Create(OpCodes.Ldloc,packetVariable),Instruction.Create(OpCodes.Ldarg_1),
                Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.PublicStatsV92","UpgradePublic")),Instruction.Create(OpCodes.Stloc,packetVariable)};
            foreach(var instruction in publicStats.Body.Instructions)if(Object.ReferenceEquals(instruction.Operand,publicStart))instruction.Operand=publicPrefix[0];
            foreach(var instruction in publicPrefix)publicStats.Body.GetILProcessor().InsertBefore(publicStart,instruction);
            ExpandBranches(publicStats);
            var propertyWriter=packetLib.Methods.Single(x=>x.Name=="SendUpdatePlayerProperty");
            var originalPropertySend=(MethodReference)propertyWriter.Body.Instructions.Single(x=>x.Operand is MethodReference&&((MethodReference)x.Operand).Name=="SendTCP").Operand;
            var createProperty=m.ImportReference(helper.MainModule.GetType("Game.Server.Packets.PlayerPropertyResponseV92").Methods.Single(x=>x.Name=="Create"&&x.Parameters.Count==2));
            Body(propertyWriter,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,packetLib.Fields.Single(x=>x.Name=="m_gameClient")),Instruction.Create(OpCodes.Ldarg_1),
                Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.PlayerPropertyResponseV92","InitializeTotem")),
                Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,createProperty),
                Instruction.Create(OpCodes.Stloc_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldloc_0),Instruction.Create(OpCodes.Call,originalPropertySend),Instruction.Create(OpCodes.Ldloc_0),Instruction.Create(OpCodes.Ret));
            propertyWriter.Body.InitLocals=true;propertyWriter.Body.Variables.Add(new VariableDefinition(createProperty.ReturnType));
            var gamePlayer=m.GetType("Game.Server.GameObjects.GamePlayer");
            var power=gamePlayer.Methods.Single(x=>x.Name=="UpdateFightPower");
            var damageCall=power.Body.Instructions.Single(x=>x.Operand is MethodReference&&((MethodReference)x.Operand).Name=="GetBaseAttack");
            if(damageCall.Next.OpCode!=OpCodes.Stloc_2)throw new Exception("Unexpected base damage local.");
            var powerSet=power.Body.Instructions.Single(x=>x.Operand is MethodReference&&((MethodReference)x.Operand).Name=="set_FightPower");
            var powerStart=powerSet.Previous.Previous.Previous.Previous;
            if(powerStart.OpCode!=OpCodes.Ldarg_0)throw new Exception("Unexpected power assignment layout.");
            var powerField=gamePlayer.Fields.Single(x=>x.Name=="FightPower");
            var powerPrefix=new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,powerField),
                Instruction.Create(OpCodes.Ldloc,power.Body.Variables[2]),Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.MagicPowerV92","Apply")),Instruction.Create(OpCodes.Stfld,powerField)};
            foreach(var instruction in power.Body.Instructions)if(Object.ReferenceEquals(instruction.Operand,powerStart))instruction.Operand=powerPrefix[0];
            foreach(var instruction in powerPrefix)power.Body.GetILProcessor().InsertBefore(powerStart,instruction);
            ExpandBranches(power);
            Body(m.GetType("Game.Base.Packets.AbstractPacketLib").Methods.Single(x=>x.Name=="SendCheckCode"),Instruction.Create(OpCodes.Ret));
            Body(m.GetType("Game.Server.Packets.Client.CheckCodeHandler").Methods.Single(x=>x.Name=="HandlePacket"),
                Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),
                Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.CaptchaV92","Dismiss")),Instruction.Create(OpCodes.Ret));
            var inventory=m.GetType("Game.Base.Packets.AbstractPacketLib").Methods.Single(x=>x.Name=="SendUpdateInventorySlot");
            var inventorySend=inventory.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="SendTCP");
            inventory.Body.GetILProcessor().InsertBefore(inventorySend,Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.InventoryResponseV92","Upgrade")));
            ExpandBranches(inventory);
            var mailType=m.GetType("Game.Server.Packets.Client.MailGetAttachHandler");
            var mailHandler=mailType.Methods.Single(x=>x.Name=="HandlePacket");
            var mailLegacy=new MethodDefinition("HandleLegacyPacketV92",MethodAttributes.Private|MethodAttributes.HideBySig,mailHandler.ReturnType);
            foreach(var parameter in mailHandler.Parameters)mailLegacy.Parameters.Add(new ParameterDefinition(parameter.Name,parameter.Attributes,parameter.ParameterType));
            var oldMailBody=mailHandler.Body;mailLegacy.Body=new MethodBody(mailLegacy){InitLocals=oldMailBody.InitLocals,MaxStackSize=oldMailBody.MaxStackSize};
            foreach(var variable in oldMailBody.Variables)mailLegacy.Body.Variables.Add(variable);
            foreach(var instruction in oldMailBody.Instructions){if(instruction.Operand is ParameterDefinition)instruction.Operand=mailLegacy.Parameters[((ParameterDefinition)instruction.Operand).Index];mailLegacy.Body.Instructions.Add(instruction);}
            foreach(var region in oldMailBody.ExceptionHandlers)mailLegacy.Body.ExceptionHandlers.Add(region);
            mailType.Methods.Add(mailLegacy);
            var mailSend=mailLegacy.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="SendTCP");
            mailLegacy.Body.GetILProcessor().InsertBefore(mailSend,Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.MailAttachmentV92","UpgradeResponse")));
            ExpandBranches(mailLegacy);
            var mailDispatch=Import(m,helper,"Game.Server.Packets.MailAttachmentV92","Dispatch");
            var delegateCtor=new MethodReference(".ctor",m.TypeSystem.Void,mailDispatch.Parameters[2].ParameterType){HasThis=true};
            delegateCtor.Parameters.Add(new ParameterDefinition(m.TypeSystem.Object));delegateCtor.Parameters.Add(new ParameterDefinition(m.TypeSystem.IntPtr));
            Body(mailHandler,Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldftn,mailLegacy),Instruction.Create(OpCodes.Newobj,delegateCtor),Instruction.Create(OpCodes.Call,mailDispatch),Instruction.Create(OpCodes.Ret));
            var petProcessor=m.GetType("Game.Server.Pet.PetLogicProcessor").Methods.Single(x=>x.Name=="OnGameData");
            var petBeginning=petProcessor.Body.Instructions[0];
            foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,Import(m,helper,"Game.Server.Packets.PetFormsV92","TryHandle")),Instruction.Create(OpCodes.Brfalse,petBeginning),Instruction.Create(OpCodes.Ret)})petProcessor.Body.GetILProcessor().InsertBefore(petBeginning,i);
            ExpandBranches(petProcessor);
            var processor=m.GetType("Game.Server.GameRoom.GameRoomLogicProcessor").Methods.Single(x=>x.Name=="OnGameData");
            var beginning=processor.Body.Instructions[0]; var il=processor.Body.GetILProcessor();
            foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,Import(m,helper,h,"TryHandle")),Instruction.Create(OpCodes.Brfalse,beginning),Instruction.Create(OpCodes.Ret)})il.InsertBefore(beginning,i);
            var cancel=m.GetType("Game.Server.GameRoom.Handle.GamePickupCancel").Methods.Single(x=>x.Name=="CommandHandler");
            Body(cancel,Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Import(m,helper,h,"Cancel")),Instruction.Create(OpCodes.Ret));
            var start=m.GetType("Game.Server.Rooms.StartGameAction").Methods.Single(x=>x.Name=="Execute");
            var canStart=start.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="CanStart");
            canStart.OpCode=OpCodes.Call; canStart.Operand=Import(m,helper,h,"CanStart");
            var firstBot=start.Body.Instructions.First(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="GetAutoBot");
            firstBot.Operand=Import(m,helper,h,"ReserveNpc");
            var add=start.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="AddRoom" && ((MethodReference)x.Operand).DeclaringType.FullName=="Game.Server.Battle.BattleMgr");
            var dup=Instruction.Create(OpCodes.Dup);
            foreach(var i in start.Body.Instructions)if(Object.ReferenceEquals(i.Operand,add))i.Operand=dup;
            start.Body.GetILProcessor().InsertBefore(add,dup);
            start.Body.GetILProcessor().InsertBefore(add,Instruction.Create(OpCodes.Call,Import(m,helper,h,"PrepareMatch")));
            ExpandBranches(start);
            var connector=m.GetType("Game.Server.Battle.FightServerConnector");
            var relay=connector.Methods.Single(x=>x.Name=="HandleSendToRoom");
            var readInner=relay.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="ReadPacket");
            var relayAfter=readInner.Next;
            var clientIdAccessor=new MethodReference("get_ClientID",m.TypeSystem.Int32,((MethodReference)readInner.Operand).ReturnType){HasThis=true};
            foreach(var instruction in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,connector.Fields.Single(x=>x.Name=="m_server")),
                Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Callvirt,clientIdAccessor),
                Instruction.Create(OpCodes.Call,Import(m,helper,h,"UpgradeRoomResponse"))})relay.Body.GetILProcessor().InsertBefore(relayAfter,instruction);
            ExpandBranches(relay);
            var sendAdd=connector.Methods.Single(x=>x.Name=="SendAddRoom");
            var send=sendAdd.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="SendTCP");
            sendAdd.Body.GetILProcessor().InsertBefore(send,Instruction.Create(OpCodes.Ldarg_1));
            sendAdd.Body.GetILProcessor().InsertBefore(send,Instruction.Create(OpCodes.Call,Import(m,helper,h,"TagRoomRequest")));
            ExpandBranches(sendAdd);
            Body(connector.Methods.Single(x=>x.Name=="SendRemoveRoom"),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Import(m,helper,h,"SendRemoveRoom")),Instruction.Create(OpCodes.Ret));
            Body(connector.Methods.Single(x=>x.Name=="HandleRoomRemove"),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,connector.Fields.Single(x=>x.Name=="m_server")),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Import(m,helper,h,"OnRoomRemoved")),Instruction.Create(OpCodes.Ret));
            var action=m.GetType("Game.Server.Rooms.CancelPickupAction"); var execute=action.Methods.Single(x=>x.Name=="Execute");
            var roomField=action.Fields.Single(x=>x.Name=="m_room"); var serverField=action.Fields.Single(x=>x.Name=="m_server");
            var battleField=m.GetType("Game.Server.Rooms.BaseRoom").Fields.Single(x=>x.Name=="BattleServer");
            beginning=execute.Body.Instructions[0]; il=execute.Body.GetILProcessor();
            foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,roomField),Instruction.Create(OpCodes.Ldfld,battleField),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,serverField),Instruction.Create(OpCodes.Beq,beginning),Instruction.Create(OpCodes.Ret)})il.InsertBefore(beginning,i);
            var fm=fight.MainModule;
            if(fm.AssemblyReferences.Any(a=>a.Name=="Fight.Matchmaking.V92"))throw new Exception("Already patched Fight input.");
            var fightCancel=fm.GetType("Fighting.Server.ServerClient").Methods.Single(x=>x.Name=="HandleGameRoomCancel");
            Body(fightCancel,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Import(fm,fightHelper,"Fighting.Server.Rooms.MatchCancellationV92","Cancel")),Instruction.Create(OpCodes.Ret));
            var fightClient=fm.GetType("Fighting.Server.ServerClient");
            var battleStart=fm.GetType("Fighting.Server.Games.GameMgr").Methods.Single(x=>x.Name=="StartBattleGame");
            var cloneMap=battleStart.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="CloneMap");
            cloneMap.Operand=Import(fm,fightHelper,"Fighting.Server.Rooms.MatchCancellationV92","CloneBattleMap");
            var battleReturn=battleStart.Body.Instructions.Single(x=>x.OpCode==OpCodes.Ret);
            battleStart.Body.GetILProcessor().InsertBefore(battleReturn,Instruction.Create(OpCodes.Call,Import(fm,fightHelper,"Fighting.Server.Rooms.MatchCancellationV92","RecordBattleStart")));
            ExpandBranches(battleStart);
            Body(fightClient.Methods.Single(x=>x.Name=="RemoveRoom"),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,Import(fm,fightHelper,"Fighting.Server.Rooms.MatchCancellationV92","RemoveRoom")),Instruction.Create(OpCodes.Ret));
            var create=fightClient.Methods.Single(x=>x.Name=="HandleGameRoomCreate");
            var construct=create.Body.Instructions.Single(x=>x.OpCode==OpCodes.Newobj && ((MethodReference)x.Operand).DeclaringType.FullName=="Fighting.Server.Rooms.ProxyRoom");
            var after=construct.Next; il=create.Body.GetILProcessor();
            // Import the packet accessor from the target module, not this patcher's runtime.
            var packetType=new TypeReference("Game.Base.Packets","GSPacketIn",fm,create.Parameters[0].ParameterType.Scope);
            var accessor=new MethodReference("get_Parameter2",fm.TypeSystem.Int32,packetType){HasThis=true};
            foreach(var i in new[]{Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Callvirt,accessor),Instruction.Create(OpCodes.Call,Import(fm,fightHelper,"Fighting.Server.Rooms.MatchCancellationV92","TagRoom"))})il.InsertBefore(after,i);
            ExpandBranches(create);
            road.Write(args[4]); fight.Write(args[5]);
        }
        Console.WriteLine("Patched Road/Fight matchmaking, cancellation guards and generation acknowledgements.");return 0;
    }
}
