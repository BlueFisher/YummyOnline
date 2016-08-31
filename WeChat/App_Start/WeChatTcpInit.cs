using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using YummyOnlineTcpClient;
//此类用于接收订单消息
namespace WeChat
{
    public class WeChatTcpInit
    {
        private static TcpClient client;
        public async static Task Initialize(Action callBackWhenConnected, Action<string,object> CallBackWhenMessageReceived)
        {
            YummyOnlineDAO.Models.SystemConfig config = await new YummyOnlineManager().GetSystemConfig();
            string guid = "E6C2FCCC-0EF6-4109-9AA3-40D72B0556BA";
            client = new TcpClient(System.Net.IPAddress.Parse(config.TcpServerIp), config.TcpServerPort, new Protocol.NewDineInformClientConnectProtocol(guid));

            client.CallBackWhenConnected = () => {
                callBackWhenConnected();
            };

            client.CallBackWhenMessageReceived = (t,p) => {
                CallBackWhenMessageReceived( t ,p );
            };

            client.Start();
        }
      
    }
}