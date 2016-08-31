using AsynchronousTcp;
using Newtonsoft.Json;
using Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using YummyOnlineDAO;
using YummyOnlineDAO.Models;

namespace YummyOnlineTcpServer {
	public partial class TcpServer {
		private TcpManager tcp;
		private string ip;
		private int port;
		private Action<string, Log.LogLevel> logDelegate;

		private WaitingForVerificationClients waitingForVerificationClients;
		private SystemClient systemClient;
		private NewDineInformClients newDineInformClients;
		private PrinterClients printerClients;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="logDelegate">记录日志回调函数, 参数1: 日志信息, 参数2: 日志等级</param>
		/// <param name="clientsStatusDelegate">socket客户端状态变化回调函数</param>
		public TcpServer(string ip, int port, Action<string, Log.LogLevel> logDelegate) {
			tcp = new TcpManager();
			this.ip = ip;
			this.port = port;
			this.logDelegate = logDelegate;

			tcp.MessageReceivedEvent += Tcp_MessageReceivedEvent;
			tcp.ErrorEvent += Tcp_ErrorEvent;

		}
		public async Task Initialize() {
			YummyOnlineManager manag er = new YummyOnlineManager();

			waitingForVerificationClients = new WaitingForVerificationClients(log, send);
			systemClient = new SystemClient(log, send, GetTcpServerStatus);
			newDineInformClients = new NewDineInformClients(log, send, await manager.GetGuids());
			printerClients = new PrinterClients(log, send, await manager.GetHotels());

			log($"Binding {ip}:{port}", Log.LogLevel.Info);

			tcp.StartListening(IPAddress.Parse(ip), port, client => {
				TcpClientInfo clientInfo = new TcpClientInfo(client);
				waitingForVerificationClients.Add(clientInfo);

				log($"{clientInfo.OriginalRemotePoint} Connected, Waiting for verification", Log.LogLevel.Info);
			});

			System.Timers.Timer timer = new System.Timers.Timer(10 * 1000);
			timer.Elapsed += (e, o) => {
				// 30秒之内已连接但是未发送身份信息的socket断开
				waitingForVerificationClients.HandleTimeOut();

				//60秒之内没有接收到心跳包的socket断开, 或发送心跳包失败的socket断开
				systemClient.HandleTimeOut();
				newDineInformClients.HandleTimeOut();
				printerClients.HandleTimeOut();
			};
			timer.Start();
		}

		private void Tcp_MessageReceivedEvent(TcpClient client, string content) {
			try {
				BaseTcpProtocol baseProtocol = JsonConvert.DeserializeObject<BaseTcpProtocol>(content);
				TcpClientInfo clientInfo = new TcpClientInfo(client);

				switch(baseProtocol.Type) {
					case TcpProtocolType.HeartBeat:
						systemClient.HeartBeat(clientInfo);
						newDineInformClients.HeartBeat(clientInfo);
						printerClients.HeartBeat(clientInfo);
						break;

					case TcpProtocolType.SystemConnect:
						systemClient.ClientConnected(clientInfo, waitingForVerificationClients);
						break;
					case TcpProtocolType.SystemCommand:
						systemClient.SystemCommand(clientInfo, JsonConvert.DeserializeObject<SystemCommandProtocol>(content),
							newDineInformClients);
						break;

					case TcpProtocolType.NewDineInformClientConnect:
						newDineInformClients.ClientConnected(clientInfo,
							JsonConvert.DeserializeObject<NewDineInformClientConnectProtocol>(content),
							waitingForVerificationClients);
						break;
					case TcpProtocolType.NewDineInform:
						newDineInformClients.NewDineInform(clientInfo, JsonConvert.DeserializeObject<NewDineInformProtocol>(content));
						break;

					case TcpProtocolType.PrintDineClientConnect:
						printerClients.ClientConnected(clientInfo,
							 JsonConvert.DeserializeObject<PrintDineClientConnectProtocol>(content),
							 waitingForVerificationClients);
						break;

					case TcpProtocolType.RequestPrintDine:
						printerClients.RequestPrintDine(clientInfo,
							JsonConvert.DeserializeObject<RequestPrintDineProtocol>(content),
							newDineInformClients.GetSender(clientInfo));
						break;
					case TcpProtocolType.RequestPrintShifts:
						printerClients.RequestPrintShifts(clientInfo,
							JsonConvert.DeserializeObject<RequestPrintShiftsProtocol>(content),
							newDineInformClients.GetSender(clientInfo));
						break;
				}
			}
			catch(Exception e) {
				log($"{client.Client.RemoteEndPoint} Receive Error: {e.Message}, Data: {content}", Log.LogLevel.Error);
				client.Close();
			}
		}

		private void Tcp_ErrorEvent(TcpClient client, Exception e) {
			waitingForVerificationClients.HandleError(client, e);
			systemClient.HandleError(client, e);
			newDineInformClients.HandleError(client, e);
			printerClients.HandleError(client, e);
		}

		private void log(string log, Log.LogLevel level) {
			logDelegate(log, level);
		}

		private void send(TcpClient client, object protocol) {
			var _ = tcp.Send(client, JsonConvert.SerializeObject(protocol, new JsonSerializerSettings {
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore
			}), null);
		}

		public TcpServerStatusProtocol GetTcpServerStatus() {
			TcpServerStatusProtocol protocol = new TcpServerStatusProtocol();

			foreach(var p in waitingForVerificationClients.Clients) {
				protocol.WaitingForVerificationClients.Add(getClientStatus(p));
			}
			protocol.SystemClient = getClientStatus(systemClient.ClientInfo);
			foreach(var pair in newDineInformClients.Clients) {
				protocol.NewDineInformClients.Add(new NewDineInformClientStatus {
					Guid = pair.Key.Guid,
					Description = pair.Key.Description,
					Status = getClientStatus(pair.Value)
				});
			}
			foreach(var pair in printerClients.Clients) {
				protocol.PrinterClients.Add(new PrinterClientStatus {
					HotelId = pair.Key,
					WaitedCount = printerClients.WaitedQueue[pair.Key].Count,
					Status = getClientStatus(pair.Value)
				});
			}

			return protocol;
		}

		private ClientStatus getClientStatus(TcpClientInfo info) {
			ClientStatus status = new ClientStatus();
			if(info != null) {
				status.IsConnected = true;
				status.ConnectedTime = info.ConnectedTime;
				status.IpAddress = info.OriginalRemotePoint.Address.ToString();
				status.Port = info.OriginalRemotePoint.Port;
				status.HeartAlive = info.HeartAlive;
			}
			else {
				status.IsConnected = false;
			}
			return status;
		}
	}
}
