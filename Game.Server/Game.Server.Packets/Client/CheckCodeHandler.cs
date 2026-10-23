using Bussiness;
using Game.Base.Packets;
using Game.Server.Managers;
using System;

namespace Game.Server.Packets.Client
{
    [PacketHandler((int)ePackageType.CHECK_CODE, "验证码")]
    public class CheckCodeHandler : IPacketHandler
    {
        //修改:  XiaoJian
        //时间:  2020-11-7
        //描述:  验证码<测试完成>   
        //状态： 正在使用

        public int HandlePacket(GameClient client, GSPacketIn pkg)
        {
            return Game.Server.Packets.CaptchaV92.Dismiss(client, pkg);
        }
    }
}
