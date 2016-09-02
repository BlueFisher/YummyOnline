using HotelDAO.Models;
using Newtonsoft.Json;
using Protocol;
using Protocol.PrintingProtocol;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using Utility;

namespace AutoPrinter {
	public partial class MainWindow {
		/// <summary>
		/// 打印订单及菜品信息
		/// </summary>
		async Task printDine(string dineId, List<int> dineMenuIds, List<PrintType> printTypes) {
			DineForPrinting dp = null;
			try {
				int tryTimes = 1;
				while(dp == null) {
					dp = await getDineForPrinting(dineId, dineMenuIds);

					if(dp == null) {
						if(tryTimes < 5) {
							serverLog($"获取订单信息{dineId}失败，开始第{tryTimes}次尝试", LogLevel.Error);
						}
						else {
							serverLog($"获取订单信息{dineId}失败，已达尝试次数上限，请检查网络设置", LogLevel.Error);
							return;
						}

						tryTimes++;
					}
				}

				serverLog($"发送打印命令 单号: {dineId}", LogLevel.Info);

				if(Config.IsIPPrinter) {
					DinePrinter dinePrinter = new DinePrinter();
					await dinePrinter.Print(dp, printTypes, dineMenuIds.Count == 0);
				}
				else {
					DineDriverPrinter dineDriverPrinter = new DineDriverPrinter();
					dineDriverPrinter.Print(dp, printTypes, dineMenuIds.Count == 0);
				}

				serverLog($"发送命令成功 单号: {dineId}", LogLevel.Success);
				printCompleted(dineId);
			}
			catch(Exception e) {
				serverLog($"无法打印 单号: {dineId}, 错误信息: {e.Message}", LogLevel.Error);
				remoteLog(Log.LogLevel.Error, $"DineId: {dineId}, {e.Message}", $"Data: {JsonConvert.SerializeObject(dp)}, Error: {e}");
			}
		}
		/// <summary>
		/// 打印远程测试
		/// </summary>
		async Task printRemoteTest(List<PrintType> printTypes, string ipOrName) {
			DineForPrinting dp = null;
			ShiftForPrinting sp = null;
			try {
				serverLog($"开始测试, 打印机: {ipOrName}", LogLevel.Info);
				dp = await getDineForPrinting("00000000000000");
				if(dp == null) {
					serverLog("获取订单信息失败，请检查网络设置", LogLevel.Error);
					return;
				}
				dp.Dine.Desk.ReciptPrinter.IpAddress = ipOrName;
				dp.Dine.Desk.ReciptPrinter.Name = ipOrName;
				dp.Dine.Desk.ServePrinter.IpAddress = ipOrName;
				dp.Dine.Desk.ServePrinter.Name = ipOrName;
				foreach(var dineMenu in dp.Dine.DineMenus) {
					dineMenu.Menu.Printer.IpAddress = ipOrName;
					dineMenu.Menu.Printer.Name = ipOrName;
				}

				serverLog($"发送测试单命令", LogLevel.Info);

				if(Config.IsIPPrinter) {
					DinePrinter dinePrinter = new DinePrinter();
					await dinePrinter.Print(dp, printTypes, true);
				}
				else {
					DineDriverPrinter dineDriverPrinter = new DineDriverPrinter();
					dineDriverPrinter.Print(dp, printTypes, true);
				}

				serverLog($"发送测试单命令成功", LogLevel.Success);
				printCompleted("00000000000000");


				sp = await getShiftsForPrinting(null, DateTime.Now);
				if(sp == null) {
					serverLog("获取交接班信息失败，请检查网络设置", LogLevel.Error);
					return;
				}

				serverLog($"发送打印测试交接班命令", LogLevel.Info);

				if(Config.IsIPPrinter) {
					ShiftPrinter shiftPrinter = new ShiftPrinter();
					await shiftPrinter.Print(sp);
				}
				else {
					ShiftDriverPrinter shiftDriverPrinter = new ShiftDriverPrinter();
					shiftDriverPrinter.Printer(sp);
				}

				serverLog($"发送打印测试交接班命令成功", LogLevel.Success);
			}
			catch(Exception e) {
				serverLog($"无法打印测试交接班, 错误信息: {e}", LogLevel.Error);
				remoteLog(Log.LogLevel.Error, $"Online Test, {e.Message}", $"Data: {JsonConvert.SerializeObject(dp)}, Error: {e}");
			}
		}
		/// <summary>
		/// 打印交接班
		/// </summary>
		async Task printShifts(List<int> ids, DateTime dateTime) {
			if(ids == null) {
				ids = new List<int>();
			}
			StringBuilder idStr = new StringBuilder();
			foreach(int id in ids) {
				idStr.Append($"{id} ");
			}

			ShiftForPrinting sp = null;

			try {
				int tryTimes = 1;
				while(sp == null) {
					sp = await getShiftsForPrinting(ids, dateTime);

					if(sp == null) {
						if(tryTimes < 5) {
							serverLog($"获取交接班信息失败，开始第{tryTimes}次尝试", LogLevel.Error);
						}
						else {
							serverLog("获取交接班信息失败，已达尝试次数上限，请检查网络设置", LogLevel.Error);
							return;
						}

						tryTimes++;
					}
				}

				serverLog($"发送打印命令 交接班", LogLevel.Info);

				if(Config.IsIPPrinter) {
					ShiftPrinter shiftPrinter = new ShiftPrinter();
					await shiftPrinter.Print(sp);
				}
				else {
					ShiftDriverPrinter shiftDriverPrinter = new ShiftDriverPrinter();
					shiftDriverPrinter.Printer(sp);
				}

				serverLog($"发送命令成功 交接班", LogLevel.Success);

				remoteLog(Log.LogLevel.Success, $"PrintShifts Completed, Ids: {idStr}, DateTime: {dateTime.ToString("yyyy-MM-dd")}");
			}
			catch(Exception e) {
				serverLog($"无法打印 交接班, 错误信息: {e.Message}", LogLevel.Error);
				remoteLog(Log.LogLevel.Error, $"Shifts Ids: {idStr}, DateTime: {dateTime.ToString("yyyy-MM-dd")}, {e.Message}", $"Data: {JsonConvert.SerializeObject(sp)}, Error: {e}");
			}
		}

		async Task<DineForPrinting> getDineForPrinting(string dineId, List<int> dineMenuIds = null) {
			object postData = new {
				HotelId = Config.HotelId,
				DineId = dineId,
				DineMenuIds = dineMenuIds
			};
			string response = await HttpPost.PostAsync(Config.RemoteGetDineForPrintingUrl, postData);
			if(response == null)
				return null;

			DineForPrinting protocol = JsonConvert.DeserializeObject<DineForPrinting>(response);
			if(protocol.Hotel == null)
				return null;
			return protocol;
		}
		async Task<ShiftForPrinting> getShiftsForPrinting(List<int> ids, DateTime dateTime) {
			object postData = new {
				HotelId = Config.HotelId,
				Ids = ids,
				DateTime = dateTime
			};
			string response = await HttpPost.PostAsync(Config.RemoteGetShiftsForPrintingUrl, postData);
			if(response == null)
				return null;

			ShiftForPrinting protocol = JsonConvert.DeserializeObject<ShiftForPrinting>(response);

			return protocol;
		}
		async Task<PrintersForPrinting> getPrintersForPrinting() {
			string response = await HttpPost.PostAsync(Config.RemoteGetPrintersForPrintingUrl, new {
				HotelId = Config.HotelId,
			});
			if(response == null)
				return null;

			PrintersForPrinting protocol = JsonConvert.DeserializeObject<PrintersForPrinting>(response);

			return protocol;
		}

		void printCompleted(string dineId) {
			object postData = new {
				HotelId = Config.HotelId,
				DineId = dineId
			};
			var _ = HttpPost.PostAsync(Config.RemotePrintCompletedUrl, postData);
		}

		void serverLog(string message, LogLevel level) {
			listViewLog.Dispatcher.Invoke(() => {
				listViewLog.Items.Add(new ServerLog {
					DateTime = DateTime.Now,
					Message = message,
					Level = level
				});

				listViewLog.SelectedIndex = listViewLog.Items.Count - 1;
				listViewLog.ScrollIntoView(listViewLog.SelectedItem);
			});
		}
		void ipPrinterLog(IPAddress ip, int? hashCode, string message, LogLevel level) {
			if(message != null) {
				listViewIpPrinter.Dispatcher.Invoke(() => {

					listViewIpPrinter.Items.Add(new IPPrinterLog {
						DateTime = DateTime.Now,
						IP = ip,
						Message = message,
						HashCode = hashCode,
						Level = level
					});

					listViewIpPrinter.SelectedIndex = listViewIpPrinter.Items.Count - 1;
					listViewIpPrinter.ScrollIntoView(listViewIpPrinter.SelectedItem);
				});
			}

			listViewIpPrinterStatus.Dispatcher.Invoke(() => {
				listViewIpPrinterStatus.Items.Clear();
				var map = IPPrinter.GetInstance().IPClientBmpMap;
				foreach(IPAddress ipKey in map.Keys) {
					string status = "已断开";
					LogLevel printerLevel = LogLevel.Error;
					if(map[ipKey].IsConnecting) {
						status = "正在连接";
						printerLevel = LogLevel.Info;
					}
					else if(map[ipKey].TcpClientInfo != null) {
						status = "已连接";
						printerLevel = LogLevel.Success;
					}
					listViewIpPrinterStatus.Items.Add(new IPPrinterStatus {
						IP = ipKey,
						Message = status,
						WaitedCount = map[ipKey].Queue.Count,
						IdleTime = map[ipKey].TcpClientInfo?.IdleTime ?? 0,
						Level = printerLevel
					});
				}
			});
		}

		void remoteLog(Log.LogLevel level, string message, string detail = null) {
			object postData = new {
				HotelId = Config.HotelId,
				Level = level,
				Message = message,
				Detail = detail
			};
			var _ = HttpPost.PostAsync(Config.RemoteLogUrl, postData);
		}
	}
}
