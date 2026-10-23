using System;
using System.Collections.Generic;
using System.Reflection;
using Fighting.Server;
using Fighting.Server.Rooms;
using Game.Base.Packets;
using System.Runtime.CompilerServices;
namespace Fighting.Server.Rooms
{
    public static class MatchCancellationV92
    {
        public static Game.Logic.Phy.Maps.Map CloneBattleMap(int mapId)
        {
            var map=Game.Logic.MapMgr.CloneMap(mapId);
            Console.WriteLine("MATCH_MAP id={0} loaded={1}",mapId,map!=null);
            return map;
        }
        public static Fighting.Server.Games.BattleGame RecordBattleStart(Fighting.Server.Games.BattleGame game)
        {
            Console.WriteLine("MATCH_CREATE result={0} state={1}",game==null ? "null" : game.Id.ToString(),game==null ? "none" : game.GameState.ToString());
            return game;
        }
        private static readonly FieldInfo roomsField=typeof(ServerClient).GetField("m_rooms",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly ConditionalWeakTable<ProxyRoom,object> generations=new ConditionalWeakTable<ProxyRoom,object>();
        private static Dictionary<int,ProxyRoom> Rooms(ServerClient client)
        { return (Dictionary<int,ProxyRoom>)roomsField.GetValue(client); }
        public static void Cancel(ServerClient client,GSPacketIn request)
        {
            ProxyRoom room=null; var rooms=Rooms(client);
            lock(rooms) rooms.TryGetValue(request.Parameter1,out room);
            if(room!=null && request.Parameter2!=0 && Generation(room)!=request.Parameter2) return;
            if(room!=null && (request.Parameter2==0 || request.ClientID==1))
            { ProxyRoomMgr.RemoveRoom(room); return; }
            ProxyRoomMgr.AddAction(new CancelAction(client,room,request.Parameter1,request.Parameter2));
        }
        public static void TagRoom(ProxyRoom room,int generation)
        { lock(generations) { generations.Remove(room); generations.Add(room,generation); } }
        public static int Generation(ProxyRoom room)
        { object generation; lock(generations) return generations.TryGetValue(room,out generation) ? (int)generation : 0; }
        public static void RemoveRoom(ServerClient client,int roomId,ProxyRoom room)
        {
            var rooms=Rooms(client); bool removed=false;
            lock(rooms) {
                ProxyRoom current;
                if(rooms.TryGetValue(roomId,out current) && Object.ReferenceEquals(current,room)) removed=rooms.Remove(roomId);
            }
            if(removed) client.SendTCP(CreateAcknowledgement(roomId,Generation(room)));
        }
        public static bool CanCancel(ProxyRoom room)
        { return room==null || (!room.IsPlaying && room.Game==null); }
        public static GSPacketIn CreateAcknowledgement(int roomId,int attempt)
        { var p=new GSPacketIn(65,roomId); p.Parameter1=attempt; return p; }
        private sealed class CancelAction : IAction
        {
            private readonly ServerClient client; private readonly ProxyRoom room; private readonly int roomId,attempt;
            public CancelAction(ServerClient c,ProxyRoom r,int id,int token) { client=c;room=r;roomId=id;attempt=token; }
            public void Execute()
            {
                // Matchmaking and cancellation run on this same manager thread.
                // A combat that won the race must keep both its proxy and mapping.
                if(!CanCancel(room)) return;
                var rooms=Rooms(client);
                lock(rooms)
                {
                    ProxyRoom current;
                    if(rooms.TryGetValue(roomId,out current))
                    {
                        if(!Object.ReferenceEquals(current,room)) return;
                        ProxyRoomMgr.RemoveRoomUnsafe(room.RoomId);
                        rooms.Remove(roomId);
                    }
                }
                client.SendTCP(CreateAcknowledgement(roomId,attempt));
            }
        }
    }
}
