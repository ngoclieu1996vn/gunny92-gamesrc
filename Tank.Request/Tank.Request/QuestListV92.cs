using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using Bussiness;
using Road.Flash;
using SqlDataProvider.Data;

namespace Tank.Request
{
    // Keep the existing quest database and publish the metadata expected by the new client.
    public class QuestListV92 : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            if (!csFunction.ValidAdminIP(context.Request.UserHostAddress))
            {
                context.Response.StatusCode = 403;
                context.Response.Write("IP is not valid!");
                return;
            }
            try
            {
                XElement result;
                using (var produce = new ProduceBussiness())
                using (var service = new ServiceBussiness())
                {
                    result = CreateResult(produce.GetALlQuest(), produce.GetAllQuestCondiction(),
                        produce.GetAllQuestGoods(), service.GetServerConfig());
                }
                // CreateResult must finish successfully before replacing the published cache.
                string status = csFunction.CreateCompressXml(context, result, "QuestList", true);
                if (status.IndexOf("Fail", StringComparison.OrdinalIgnoreCase) >= 0)
                    context.Response.StatusCode = 500;
                context.Response.Write(status);
            }
            catch (Exception error)
            {
                context.Trace.Warn("QuestListV92", "Quest cache was not regenerated.", error);
                context.Response.StatusCode = 500;
                context.Response.Write("QuestList generation failed; check server logs.");
            }
        }

        public static XElement CreateResult(QuestInfo[] quests, QuestConditionInfo[] conditions,
            QuestAwardInfo[] awards, IDictionary<string, string> settings)
        {
            if (quests == null || quests.Length == 0 || conditions == null || awards == null)
                throw new InvalidOperationException("Quest database data is unavailable.");
            var result = new XElement("Result", new XAttribute("value", true),
                new XAttribute("message", "Success!"));
            foreach (var quest in quests)
            {
                var item = FlashUtils.CreateQuestInfo(quest);
                item.SetAttributeValue("RewardBindMoney", quest.RewardGiftToken);
                foreach (var condition in conditions.Where(value => value.QuestID == quest.ID))
                    item.Add(FlashUtils.CreateQuestCondiction(condition));
                foreach (var award in awards.Where(value => value.QuestID == quest.ID))
                {
                    var reward = FlashUtils.CreateQuestGoods(award);
                    // This backend uses a fixed reward count; keep it consistent at every client level.
                    for (int level = 1; level <= 5; level++)
                        reward.SetAttributeValue("RewardItemCount" + level, award.RewardItemCount);
                    item.Add(reward);
                }
                result.Add(item);
            }
            var rate = new XElement("Rate");
            foreach (string name in new[] { "BindMoneyRate", "ExpRate", "GoldRate", "ExploitRate", "CanOneKeyFinishTime" })
            {
                string value;
                if (settings == null || !settings.TryGetValue(name, out value) || String.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Missing quest rate setting: " + name);
                rate.SetAttributeValue(name, value);
            }
            result.Add(rate);
            return result;
        }
    }
}
