using System;
using System.Runtime.Serialization;
using Fighting.Server.Rooms;
using Game.Base.Packets;
class MatchCancellationTests
{
    static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
    static int Main()
    {
        var room=(ProxyRoom)FormatterServices.GetUninitializedObject(typeof(ProxyRoom));
        Assert(MatchCancellationV92.CanCancel(null),"Absent mapping still needs an acknowledgement.");
        Assert(MatchCancellationV92.CanCancel(room),"Waiting room must be cancellable.");
        room.IsPlaying=true;
        Assert(!MatchCancellationV92.CanCancel(room),"A combat that won the race must survive cancellation.");
        MatchCancellationV92.TagRoom(room,19);
        Assert(MatchCancellationV92.Generation(room)==19,"Remote room lifetime must retain the incoming generation.");
        var p=MatchCancellationV92.CreateAcknowledgement(77,19);
        Assert(p.Code==65 && p.ClientID==77 && p.Parameter1==19,"Acknowledgement must retain the match generation.");
        Console.WriteLine("PASS: absent/waiting rooms acknowledge cancellation; active combat is retained; generation is preserved.");
        return 0;
    }
}
