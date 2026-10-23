using System;
using System.Configuration;
using System.Collections.Generic;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using SqlDataProvider.Data;
namespace Game.Server.Packets
{
    // User-supplied estimate, not the official Zing formula. Off unless explicitly opted in.
    public static class MagicPowerV92
    {
        public static bool IsEnabledForPlayer(int id,bool enabled,string ids)
        {
            if(!enabled || id<=0 || String.IsNullOrWhiteSpace(ids))return false;
            foreach(string token in ids.Split(',')){int allowed;if(Int32.TryParse(token.Trim(),out allowed)&&allowed==id)return true;}
            return false;
        }
        public static int Calculate(int baseline,double damage,long magicAttack,long magicDefence)
        {
            if(baseline<0 || Double.IsNaN(damage)||Double.IsInfinity(damage)||damage<800||damage>1300)return baseline;
            double magic=(double)Math.Max(0L,magicAttack)+(double)Math.Max(0L,magicDefence);
            double extra=Math.Floor((9.7662e-9*damage*damage*damage+0.33)*magic);
            return extra>=Int32.MaxValue-(double)baseline?Int32.MaxValue:baseline+(int)extra;
        }
        public static int Apply(GamePlayer player,int baseline,double damage)
        {
            if(player==null||player.PlayerCharacter==null)return baseline;
            bool enabled;
            if(!Boolean.TryParse(ConfigurationManager.AppSettings["MagicPowerV92.Enabled"],out enabled)||
               !IsEnabledForPlayer(player.PlayerCharacter.ID,enabled,ConfigurationManager.AppSettings["MagicPowerV92.PlayerIDs"]))return baseline;
            return Calculate(baseline,damage,TotalMagic(player.PlayerCharacter,player.PlayerProp,false),TotalMagic(player.PlayerCharacter,player.PlayerProp,true));
        }
        public static int TotalMagic(PlayerInfo info,PlayerProperty properties,bool defence)
        {
            long total=0;Dictionary<string,int> sources;
            if(properties!=null && properties.Current!=null && properties.Current.TryGetValue(defence?"MagicDefence":"MagicAttack",out sources) && sources!=null)
                foreach(int value in sources.Values)total+=value;
            // ExplorerManual is stored separately and is not in CreateBaseProp's source map.
            if(info!=null&&info.explorerManualInfo!=null)total+=defence?info.explorerManualInfo.MagicResistance:info.explorerManualInfo.MagicAttack;
            return (int)Math.Min(Int32.MaxValue,Math.Max(0L,total));
        }
    }
}
