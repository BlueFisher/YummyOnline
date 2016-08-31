
using Protocol;
using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using YummyOnlineDAO;
using YummyOnlineDAO.Models;
using HotelDAO;
using HotelDAO.Models;
using Senparc.Weixin.MP.Containers;
using Senparc.Weixin.MP.AdvancedAPIs.TemplateMessage;

namespace WeChat
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            //增加接受订单消息tcpClient

            var _ = WeChatTcpInit.Initialize(async () =>
            {

                await new YummyOnlineManager().RecordLog(YummyOnlineDAO.Models.Log.LogProgram.WeChat, YummyOnlineDAO.Models.Log.LogLevel.Success, "WeChat TcpServer Connected");
            }, async (t, p) =>
            {
                //此为收到的订单
                if (t != Protocol.TcpProtocolType.NewDineInform)
                    return;
                NewDineInformProtocol protocal = (NewDineInformProtocol)p;
                //1.查询订单
                //2.发送模板消息
                await queryDine(protocal);
            });
        }

        protected async Task queryDine(NewDineInformProtocol p)
        {

            YummyOnlineManager manager = new YummyOnlineManager();
            string connStr = (await manager.GetHotelById(p.HotelId)).ConnectionString;
            HotelManager hotelManager = new HotelManager(connStr);
            var dine = await hotelManager.GetFormatedDineById(p.DineId);
            string openId = dine.WeChatOpenId;
            decimal price = dine.Price;
            DateTime time = dine.BeginTime;
            sendWeChatMsg(openId, price, time);
        }

        protected async void sendWeChatMsg(string openId, decimal price, DateTime beginTime)
        {
            var accessToken = AccessTokenContainer.GetAccessToken(AppConfig.AppId);
            if (accessToken == null)
            {

                accessToken = AccessTokenContainer.TryGetAccessToken(AppConfig.AppId, AppConfig.AppSecret);
            }

            var data = new
            {

                pay = new
                {
                    value = price.ToString() + "元",
                    color = "#173177"
                },
                address = new
                {
                    value = "店小二点餐",
                    color = "#173177"
                },
                time = new
                {
                    value = beginTime.ToString(),
                    color = "#173177"
                }
            };

            SendTemplateMessageResult result = Senparc.Weixin.MP.AdvancedAPIs.TemplateApi.SendTemplateMessage(accessToken, openId,
                AppConfig.TemplateIdForResponse, AppConfig.TemplateResponseUrl, data);
            if (result.errmsg != null)
            {

                await new YummyOnlineManager().RecordLog(YummyOnlineDAO.Models.Log.LogProgram.WeChat, YummyOnlineDAO.Models.Log.LogLevel.Success, result.errmsg);

            }
        }


    }
}
