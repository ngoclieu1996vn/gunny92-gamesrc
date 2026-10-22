using System;
using System.Diagnostics;
using System.Web;

namespace Tank.Request
{
    // Current client sends fire-and-forget onboarding analytics, not quest progress.
    public sealed class NoviceNodeDataV92 : IHttpHandler
    {
        public bool IsReusable { get { return true; } }
        public void ProcessRequest(HttpContext context)
        {
            int node;
            string user = context.Request["userName"];
            context.Response.ContentType = "text/xml";
            if (!Int32.TryParse(context.Request["nodeID"], out node) || node < 0 ||
                String.IsNullOrEmpty(user) || user.Length > 200)
            {
                context.Response.StatusCode = 400;
                context.Response.Write("<Result value=\"false\" message=\"Invalid node event\" />");
                return;
            }
            // Do not trust this unauthenticated analytics event to change player state.
            Trace.TraceInformation("Novice client node: {0}", node);
            context.Response.Write("<Result value=\"true\" message=\"\" />");
        }
    }
}
