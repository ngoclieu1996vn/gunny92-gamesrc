using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.Rooms;
using Game.Server.Battle;
using Game.Server.RingStation;
using Game.Logic;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
namespace Game.Server.Packets
{
    public static class MatchmakingV92
    {
        public static GSPacketIn UpgradeRoomResponse(GSPacketIn packet,BattleServer server,int roomId)
        {
            if(packet.Code==91 && packet.Length>20 && (packet.Buffer[20]==99 || packet.Buffer[20]==6 || packet.Buffer[20]==2))
            {
                BaseRoom room=server==null?null:server.FindRoom(roomId);
                if(room==null || (room.RoomType!=eRoomType.Match && room.RoomType!=eRoomType.EliteGameScore && room.RoomType!=eRoomType.EliteGameChampion))return packet;
                if(packet.Buffer[20]==2)return BattleResponseV92.UpgradeShoot(packet);
                return packet.Buffer[20]==99?BattleResponseV92.UpgradeStart(packet):BattleResponseV92.UpgradeTurn(packet);
            }
            return BattleResponseV92.UpgradeCreate(packet);
        }
        private static readonly ConditionalWeakTable<GamePlayer, object> choices=new ConditionalWeakTable<GamePlayer, object>();
        private static readonly ConditionalWeakTable<BaseRoom, MatchAttempt> attempts=new ConditionalWeakTable<BaseRoom, MatchAttempt>();
        private static int sequence;
        private sealed class MatchAttempt { public int Id; }
        public static void SetBotPreference(GamePlayer player,bool bot)
        { lock(choices) { choices.Remove(player); choices.Add(player,bot); } }
        public static bool WantsBot(BaseRoom room)
        {
            object choice;
            if(room==null || room.Host==null || room.RoomType!=eRoomType.Match || room.isCrosszone || room.GameStyle!=0) return false;
            lock(choices) return choices.TryGetValue(room.Host,out choice) && (bool)choice;
        }
        public static GSPacketIn CreateRival(bool bot)
        { var p=new GSPacketIn(94); p.WriteByte(36); p.WriteBoolean(bot); return p; }
        public static bool TryHandle(GamePlayer player,GSPacketIn packet)
        {
            int offset=packet.Offset;
            if(packet.DataLeft<4) return false;
            int command=packet.ReadInt();
            if(command!=36) { packet.Offset=offset; return false; }
            if(player==null || packet.DataLeft!=1) return true;
            byte value=packet.ReadByte(); if(value>1) return true;
            BaseRoom room=player.CurrentRoom;
            if(room==null || room.Host!=player || room.IsPlaying || room.Game!=null) return true;
            SetBotPreference(player,value!=0);
            var response=CreateRival(WantsBot(room));
            player.SendTCP(response); room.SendToAll(response,player);
            return true;
        }
        // Replaces the legacy low-level automatic bot creation. The explicit choice
        // is evaluated once, immediately before the human room is sent to Fight.
        public static int ReserveNpc(GamePlayer player,int roomType,int gameType)
        { return RingStationConfiguration.NextRoomId(); }
        public static bool CanStart(BaseRoom room)
        {
            if(room.IsPlaying || room.Game!=null) return false;
            if(WantsBot(room) && room.PlayerCount!=1)
            {
                room.Host.SendMessage("Chế độ Bot hiện hỗ trợ phòng một người. Hãy chọn Người để ghép tổ đội.");
                room.SendCancelPickUp(); return false;
            }
            return room.CanStart();
        }
        public static void PrepareMatch(BaseRoom room)
        {
            lock(attempts) { attempts.Remove(room); attempts.Add(room,new MatchAttempt { Id=Interlocked.Increment(ref sequence) }); }
            if(room.RoomType!=eRoomType.Match) return;
            room.StartWithNpc=WantsBot(room);
            if(room.StartWithNpc)
            {
                room.PickUpNpcId=RingStationMgr.GetAutoBot(room.Host,(int)room.RoomType,(int)room.GameType);
            }
        }
        public static void SendRemoveRoom(FightServerConnector connector,BaseRoom room)
        {
            MatchAttempt attempt;
            lock(attempts) {
                if(!attempts.TryGetValue(room,out attempt)) { attempt=new MatchAttempt { Id=Interlocked.Increment(ref sequence) }; attempts.Add(room,attempt); }
            }
            var p=CreateCancelRequest(room.RoomId,attempt.Id);
            if(room.Game!=null || room.PlayerCount==0) p.ClientID=1; // ordinary cleanup, including active combat
            connector.SendTCP(p);
        }
        public static GSPacketIn TagRoomRequest(GSPacketIn packet,BaseRoom room)
        {
            MatchAttempt attempt;
            lock(attempts) { if(attempts.TryGetValue(room,out attempt)) packet.Parameter2=attempt.Id; }
            return packet;
        }
        public static GSPacketIn CreateCancelRequest(int roomId,int attempt)
        { var p=new GSPacketIn(65); p.Parameter1=roomId; p.Parameter2=attempt; return p; }
        public static bool AcceptCancellation(BaseRoom room,int attemptId)
        {
            MatchAttempt current;
            lock(attempts) {
                if(!attempts.TryGetValue(room,out current)) return attemptId==0;
                return attemptId!=0 && current.Id==attemptId;
            }
        }
        public static void OnRoomRemoved(BattleServer server,GSPacketIn packet)
        {
            BaseRoom room=server.FindRoom(packet.ClientID);
            if(room!=null && AcceptCancellation(room,packet.Parameter1)) server.RemoveRoomImp(packet.ClientID);
        }
        public static bool Cancel(GamePlayer player)
        {
            if(player!=null && player.CurrentRoom!=null) RoomMgr.AddAction(new CancelAction(player,player.CurrentRoom));
            return true;
        }
        private sealed class CancelAction : IAction
        {
            private readonly GamePlayer player; private readonly BaseRoom room;
            public CancelAction(GamePlayer p,BaseRoom r) { player=p;room=r; }
            public void Execute()
            {
                if(player.CurrentRoom!=room || room.Game!=null) return;
                if(room.BattleServer!=null && room.BattleServer.RemoveRoom(room))
                {
                    room.UpdatePlayerState(player,(byte)(room.Host==player ? 2 : 0),true);
                    return;
                }
                room.BattleServer=null; room.IsPlaying=false;
                room.SendCancelPickUp();
                RoomMgr.WaitingRoom.SendUpdateCurrentRoom(room);
            }
        }
    }
}
