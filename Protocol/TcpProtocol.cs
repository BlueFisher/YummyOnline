﻿using System;
using System.Collections.Generic;

namespace Protocol {
	public static class TcpProtocolType {
		/// <summary>
		/// 心跳包
		/// </summary>
		public const string HeartBeat = "{F3E101EA-F55D-40DD-9747-BF0DB29C98AF}";
		/// <summary>
		/// 总控系统连接
		/// </summary>
		public const string SystemConnect = "{9DA2754A-B3E5-408A-A8C5-524AEA61D2B3}";
		/// <summary>
		/// 总控系统发送指令
		/// </summary>
		public const string SystemCommand = "{8B0B5D6B-963B-4164-AB4D-9A37042057DC}";
		/// <summary>
		/// TcpServer状态通知
		/// </summary>
		public const string TcpServerStatusInform = "{DCF7434E-2A3E-4296-BED7-EFB528EA317A}";
		/// <summary>
		/// 需要及时收到新订单消息的客户端连接
		/// </summary>
		public const string NewDineInformClientConnect = "{053A168C-D4B8-409A-A058-7E2208B57CDA}";
		/// <summary>
		/// 新订单消息
		/// </summary>
		public const string NewDineInform = "{6309155D-B9D9-4417-B1BF-C985F2EA6630}";
		/// <summary>
		/// 饭店打印服务器连接
		/// </summary>
		public const string PrintDineClientConnect = "{2617871B-88A7-43A4-8E2F-23C319B61750}";
		/// <summary>
		/// 请求饭店打印
		/// </summary>
		public const string RequestPrintDine = "{FCAC99D2-1807-4FD0-8B5C-71D00B91A927}";
		/// <summary>
		/// 饭店打印
		/// </summary>
		public const string PrintDine = "{8A39D55F-CC16-4798-990E-062A9260496C}";
		/// <summary>
		/// 请求饭店打印交接班信息
		/// </summary>
		public const string RequestPrintShifts = "{4E6D44F1-9FD6-4DAD-BAE6-545577701149}";
		/// <summary>
		/// 饭店打印交接班信息
		/// </summary>
		public const string PrintShifts = "{EBFC25BD-2F2A-4ED2-8DEF-3206C113913A}";
	}
	public class BaseTcpProtocol {
		public BaseTcpProtocol(string type) {
			Type = type;
		}
		public string Type { get; set; }
	}

	public class HeartBeatProtocol : BaseTcpProtocol {
		public HeartBeatProtocol() : base(TcpProtocolType.HeartBeat) { }
	}

	public class SystemConnectProtocol : BaseTcpProtocol {
		public SystemConnectProtocol() : base(TcpProtocolType.SystemConnect) { }
	}
	public class NewDineInformClientConnectProtocol : BaseTcpProtocol {
		public NewDineInformClientConnectProtocol() : base(TcpProtocolType.NewDineInformClientConnect) { }
		public NewDineInformClientConnectProtocol(string guidStr)
			: base(TcpProtocolType.NewDineInformClientConnect) {
			Guid = new Guid(guidStr);
		}
		public Guid Guid { get; set; }
	}
	public class PrintDineClientConnectProtocol : BaseTcpProtocol {
		public PrintDineClientConnectProtocol() : base(TcpProtocolType.PrintDineClientConnect) { }
		public PrintDineClientConnectProtocol(int hotelId)
			: base(TcpProtocolType.PrintDineClientConnect) {
			HotelId = hotelId;
		}
		public int HotelId { get; set; }
	}

	public enum SystemCommandType {
		RefreshNewDineClients = 0,
		RequestTcpServerStatus = 1
	}
	public class SystemCommandProtocol : BaseTcpProtocol {
		public SystemCommandProtocol() : base(TcpProtocolType.SystemCommand) { }
		public SystemCommandProtocol(SystemCommandType commandType)
			: base(TcpProtocolType.SystemCommand) {
			CommandType = commandType;
		}
		public SystemCommandType CommandType { get; set; }
	}
	public class TcpServerStatusInformProtocol : BaseTcpProtocol {
		public TcpServerStatusInformProtocol() : base(TcpProtocolType.TcpServerStatusInform) { }
		public TcpServerStatusInformProtocol(TcpServerStatusProtocol status)
			: base(TcpProtocolType.TcpServerStatusInform) {
			Status = status;
		}
		public TcpServerStatusProtocol Status { get; set; }
	}

	public class NewDineInformProtocol : BaseTcpProtocol {
		public NewDineInformProtocol() : base(TcpProtocolType.NewDineInform) { }
		public NewDineInformProtocol(int hotelId, string dineId, bool isPaid)
			: base(TcpProtocolType.NewDineInform) {
			HotelId = hotelId;
			DineId = dineId;
			IsPaid = isPaid;
		}
		public int HotelId { get; set; }
		public string DineId { get; set; }
		public bool IsPaid { get; set; }
	}

	public enum PrintType {
		/// <summary>
		/// 收银条
		/// </summary>
		Recipt = 0,
		/// <summary>
		/// 厨房单
		/// </summary>
		KitchenOrder = 1,
		/// <summary>
		/// 传菜单
		/// </summary>
		ServeOrder = 2
	}

	public class PrintDineProtocol : BaseTcpProtocol {
		public PrintDineProtocol() : base(TcpProtocolType.PrintDine) { }
		public PrintDineProtocol(string dineId, List<int> dineMenuIds, List<PrintType> printTypes)
			: base(TcpProtocolType.PrintDine) {
			DineId = dineId;
			DineMenuIds = dineMenuIds;
			PrintTypes = printTypes;
		}
		public string DineId { get; set; }
		public List<int> DineMenuIds { get; set; } = new List<int>();
		public List<PrintType> PrintTypes { get; set; } = new List<PrintType>();
	}
	public class RequestPrintDineProtocol : PrintDineProtocol {
		public RequestPrintDineProtocol() : base() { }
		public RequestPrintDineProtocol(int hotelId, string dineId, List<int> dineMenuIds, List<PrintType> printTypes)
			: base(dineId, dineMenuIds, printTypes) {
			Type = TcpProtocolType.RequestPrintDine;
			HotelId = hotelId;
		}
		public int HotelId { get; set; }
	}

	public class PrintShiftsProtocol : BaseTcpProtocol {
		public PrintShiftsProtocol() : base(TcpProtocolType.PrintShifts) { }
		public PrintShiftsProtocol(List<int> ids, DateTime dateTime)
			: base(TcpProtocolType.PrintShifts) {
			Ids = ids;
			DateTime = dateTime;
		}
		public List<int> Ids { get; set; }
		public DateTime DateTime { get; set; }
	}
	public class RequestPrintShiftsProtocol : PrintShiftsProtocol {
		public RequestPrintShiftsProtocol() : base() { }
		public RequestPrintShiftsProtocol(int hotelId, List<int> ids, DateTime dateTime)
			: base(ids, dateTime) {
			Type = TcpProtocolType.RequestPrintShifts;
			HotelId = hotelId;
		}
		public int HotelId { get; set; }
	}
}
