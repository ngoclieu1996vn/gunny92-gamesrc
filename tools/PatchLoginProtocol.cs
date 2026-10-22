using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Apply the tested reader to an existing server assembly without rebuilding unrelated legacy code.
public static class PatchLoginProtocol
{
    public static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Expected: server.dll reader.dll output.dll");
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(args[0])));
        using (var target = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }))
        using (var reader = AssemblyDefinition.ReadAssembly(args[1]))
        {
            var module = target.MainModule;
            var handler = module.GetType("Game.Server.Packets.Client.UserLoginHandler");
            if (handler.Methods.Any(method => method.Name == "PrepareLoginPacketV92"))
                throw new InvalidOperationException("Login protocol patch is already installed.");
            var source = reader.MainModule.GetType("Game.Server.Packets.Client.LoginPacketReader").Methods.Single(method => method.Name == "Prepare");
            var copied = new MethodDefinition("PrepareLoginPacketV92", MethodAttributes.Private | MethodAttributes.Static,
                module.TypeSystem.Void);
            foreach (var parameter in source.Parameters)
                copied.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, module.ImportReference(parameter.ParameterType)));
            copied.Body.InitLocals = source.Body.InitLocals;
            foreach (var variable in source.Body.Variables)
                copied.Body.Variables.Add(new VariableDefinition(module.ImportReference(variable.VariableType)));
            var instructions = new Dictionary<Instruction, Instruction>();
            foreach (var instruction in source.Body.Instructions)
            {
                var clone = Instruction.Create(OpCodes.Nop);
                clone.OpCode = instruction.OpCode;
                instructions.Add(instruction, clone);
                copied.Body.Instructions.Add(clone);
            }
            foreach (var instruction in source.Body.Instructions)
            {
                object operand = instruction.Operand;
                if (operand is Instruction) operand = instructions[(Instruction)operand];
                else if (operand is Instruction[]) operand = ((Instruction[])operand).Select(value => instructions[value]).ToArray();
                else if (operand is VariableDefinition) operand = copied.Body.Variables[((VariableDefinition)operand).Index];
                else if (operand is ParameterDefinition) operand = copied.Parameters[((ParameterDefinition)operand).Index];
                else if (operand is MethodReference) operand = module.ImportReference((MethodReference)operand);
                else if (operand is FieldReference) operand = module.ImportReference((FieldReference)operand);
                else if (operand is TypeReference) operand = module.ImportReference((TypeReference)operand);
                instructions[instruction].Operand = operand;
            }
            if (source.Body.ExceptionHandlers.Count != 0) throw new InvalidOperationException("Reader must not contain exception handlers.");
            handler.Methods.Add(copied);
            var methodBody = handler.Methods.Single(method => method.Name == "HandlePacket").Body;
            var firstRead = methodBody.Instructions.First(instruction => instruction.Operand is MethodReference &&
                ((MethodReference)instruction.Operand).Name == "ReadInt");
            var start = firstRead.Previous;
            if (start.OpCode != OpCodes.Ldarg_2) throw new InvalidOperationException("Unexpected login handler layout.");
            var il = methodBody.GetILProcessor();
            var rsaGetter = module.GetType("Game.Server.Managers.WorldMgr").Methods.Single(method => method.Name == "get_RsaCryptor");
            var keySize = module.ImportReference(typeof(AsymmetricAlgorithm).GetProperty("KeySize").GetGetMethod());
            var prefix = new[] {
                Instruction.Create(OpCodes.Ldarg_2), Instruction.Create(OpCodes.Call, rsaGetter),
                Instruction.Create(OpCodes.Callvirt, keySize), Instruction.Create(OpCodes.Ldc_I4_8),
                Instruction.Create(OpCodes.Div), Instruction.Create(OpCodes.Call, copied) };
            foreach (var instruction in methodBody.Instructions)
                if (Object.ReferenceEquals(instruction.Operand, start)) instruction.Operand = prefix[0];
            foreach (var instruction in prefix)
                il.InsertBefore(start, instruction);
            var responseSource = reader.MainModule.GetType("Game.Server.Packets.LoginResponseV92");
            var methods = CopyStaticType(module, responseSource);
            var library = module.GetType("Game.Base.Packets.AbstractPacketLib");
            if (library == null) library = module.Types.Single(type => type.Name == "AbstractPacketLib");
            var privateInfo = library.Methods.Single(value => value.Name == "SendUpdatePrivateInfo");
            var giftGetter = (MethodReference)privateInfo.Body.Instructions.First(value => value.Operand is MethodReference && ((MethodReference)value.Operand).Name == "get_GiftToken").Operand;
            var login = library.Methods.Single(value => value.Name == "SendLoginSuccess");
            var clientField = (FieldReference)login.Body.Instructions.First(value => value.OpCode == OpCodes.Ldfld).Operand;
            var playerGetter = (MethodReference)login.Body.Instructions.First(value => value.Operand is MethodReference && ((MethodReference)value.Operand).Name == "get_Player").Operand;
            var characterGetter = (MethodReference)login.Body.Instructions.First(value => value.Operand is MethodReference && ((MethodReference)value.Operand).Name == "get_PlayerCharacter").Operand;
            foreach (var sendMethod in new[] { login, privateInfo })
            {
                var send = sendMethod.Body.Instructions.Single(value => value.Operand is MethodReference && ((MethodReference)value.Operand).Name == "SendTCP");
                var editor = sendMethod.Body.GetILProcessor();
                if (sendMethod == login)
                {
                    var packetLoad = send.Previous;
                    var sendStart = packetLoad.Previous;
                    if (sendStart.OpCode != OpCodes.Ldarg_0 || packetLoad.OpCode != OpCodes.Ldloc_1)
                        throw new InvalidOperationException("Unexpected login response local layout.");
                    var packetType = module.ImportReference(responseSource.Methods.Single(value => value.Name == "Upgrade").ReturnType);
                    var idGetter = new MethodReference("get_ClientID", module.TypeSystem.Int32, packetType) { HasThis = true };
                    editor.InsertBefore(sendStart, Instruction.Create(OpCodes.Ldarg_0));
                    editor.InsertBefore(sendStart, Instruction.Create(OpCodes.Ldloc_1));
                    editor.InsertBefore(sendStart, Instruction.Create(OpCodes.Callvirt, idGetter));
                    editor.InsertBefore(sendStart, Instruction.Create(OpCodes.Call, methods[responseSource.Methods.Single(value => value.Name == "CreateKingBlessState")]));
                    editor.InsertBefore(sendStart, Instruction.Create(OpCodes.Call, (MethodReference)send.Operand));
                    editor.InsertBefore(send, Instruction.Create(OpCodes.Ldarg_0));
                    editor.InsertBefore(send, Instruction.Create(OpCodes.Ldfld, clientField));
                    editor.InsertBefore(send, Instruction.Create(OpCodes.Callvirt, playerGetter));
                    editor.InsertBefore(send, Instruction.Create(OpCodes.Callvirt, characterGetter));
                }
                else editor.InsertBefore(send, Instruction.Create(OpCodes.Ldarg_1));
                editor.InsertBefore(send, Instruction.Create(OpCodes.Callvirt, giftGetter));
                editor.InsertBefore(send, Instruction.Create(OpCodes.Call, methods[responseSource.Methods.Single(value => value.Name == "Upgrade")]));
            }
            var hallSource = reader.MainModule.GetType("Game.Server.Packets.HallSceneV92");
            var hallMethods = CopyStaticType(module, hallSource);
            var processor = module.GetType("Game.Base.Packets.PacketProcessor");
            var handle = processor.Methods.Single(value => value.Name == "HandlePacket");
            var beginning = handle.Body.Instructions[0];
            var processorIl = handle.Body.GetILProcessor();
            var hook = new[] {
                Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldfld, processor.Fields.Single(value => value.Name == "m_client")),
                Instruction.Create(OpCodes.Ldarg_1),
                Instruction.Create(OpCodes.Call, hallMethods[hallSource.Methods.Single(value => value.Name == "TryHandle")]),
                Instruction.Create(OpCodes.Brfalse, beginning), Instruction.Create(OpCodes.Ret) };
            foreach (var instruction in hook) processorIl.InsertBefore(beginning, instruction);
            target.Write(args[2]);
            Console.WriteLine("Patched login framing; original RSA validation and account authentication retained.");
        }
        return 0;
    }
    private static Dictionary<MethodDefinition, MethodDefinition> CopyStaticType(ModuleDefinition module, TypeDefinition responseSource)
    {
            var responseType = new TypeDefinition(responseSource.Namespace, responseSource.Name,
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                module.TypeSystem.Object);
            module.Types.Add(responseType);
            var methods = new Dictionary<MethodDefinition, MethodDefinition>();
            foreach (var item in responseSource.Methods)
            {
                var clone = new MethodDefinition(item.Name, item.Attributes, module.ImportReference(item.ReturnType));
                foreach (var parameter in item.Parameters)
                    clone.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, module.ImportReference(parameter.ParameterType)));
                responseType.Methods.Add(clone);
                methods.Add(item, clone);
            }
            foreach (var pair in methods)
            {
                var from = pair.Key;
                var to = pair.Value;
                to.Body.InitLocals = from.Body.InitLocals;
                foreach (var variable in from.Body.Variables)
                    to.Body.Variables.Add(new VariableDefinition(module.ImportReference(variable.VariableType)));
                var map = new Dictionary<Instruction, Instruction>();
                foreach (var item in from.Body.Instructions)
                {
                    var clone = Instruction.Create(OpCodes.Nop); clone.OpCode = item.OpCode;
                    map.Add(item, clone); to.Body.Instructions.Add(clone);
                }
                foreach (var item in from.Body.Instructions)
                {
                    object operand = item.Operand;
                    if (operand is Instruction) operand = map[(Instruction)operand];
                    else if (operand is Instruction[]) operand = ((Instruction[])operand).Select(value => map[value]).ToArray();
                    else if (operand is VariableDefinition) operand = to.Body.Variables[((VariableDefinition)operand).Index];
                    else if (operand is ParameterDefinition) operand = to.Parameters[((ParameterDefinition)operand).Index];
                    else if (operand is MethodReference)
                    {
                        var reference = (MethodReference)operand;
                        operand = reference.DeclaringType.FullName == responseSource.FullName
                            ? methods[responseSource.Methods.Single(value => value.Name == reference.Name)]
                            : module.ImportReference(reference);
                    }
                    else if (operand is FieldReference) operand = module.ImportReference((FieldReference)operand);
                    else if (operand is TypeReference) operand = module.ImportReference((TypeReference)operand);
                    map[item].Operand = operand;
                }
                foreach (var region in from.Body.ExceptionHandlers)
                {
                    to.Body.ExceptionHandlers.Add(new ExceptionHandler(region.HandlerType) {
                        TryStart = map[region.TryStart], TryEnd = region.TryEnd == null ? null : map[region.TryEnd],
                        HandlerStart = map[region.HandlerStart], HandlerEnd = region.HandlerEnd == null ? null : map[region.HandlerEnd],
                        FilterStart = region.FilterStart == null ? null : map[region.FilterStart],
                        CatchType = region.CatchType == null ? null : module.ImportReference(region.CatchType)
                    });
                }
            }
        return methods;
    }

}
