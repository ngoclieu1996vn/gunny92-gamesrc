using Game.Base.Packets;
namespace Game.Server.Packets
{
    // CAPTCHA disabled at the owner's request. Account login is unchanged.
    public static class CaptchaV92
    {
        public static int Dismiss(GameClient client,GSPacketIn ignored)
        {
            if(client==null || client.Player==null)return 0;
            var player=client.Player;var info=player.PlayerCharacter;
            player.FirstLoginCheckCode=false;
            info.CheckCode="";info.CheckError=0;info.CheckCount=0;info.CheckTimer=0;info.gameUpToCaptcha=0;
            var close=new GSPacketIn(200,info.ID);close.WriteByte(1);close.WriteBoolean(false);
            client.SendTCP(close);return 0;
        }
    }
}
