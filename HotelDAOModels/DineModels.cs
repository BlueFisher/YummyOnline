using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelDAO.Models {
	public enum DineStatus {
		/// <summary>
		/// 用户刚下单，未作处理
		/// </summary>
		Untreated = 0,
		/// <summary>
		/// 已打印
		/// </summary>
		Printed = 1,
		/// <summary>
		/// 上菜完毕，订单完成
		/// </summary>
		Finished = 2,
		/// <summary>
		/// 已完成交接班
		/// </summary>
		Shifted = 3
	}
	public enum DineType {
		/// <summary>
		/// 堂吃
		/// </summary>
		ToStay = 0,
		/// <summary>
		/// 外卖
		/// </summary>
		ToGo = 1
	}
	/// <summary>
	/// 整单打折类型
	/// </summary>
	public enum DiscountType {
		None = 0,
		/// <summary>
		/// 支付打折
		/// </summary>
		PayKind = 1,
		/// <summary>
		/// 分时段打折
		/// </summary>
		Time = 2,
		/// <summary>
		/// 会员打折
		/// </summary>
		Vip = 3,
		/// <summary>
		/// 自定义折扣
		/// </summary>
		Custom = 4
	}
	public enum DineFrom {
		CustomerBrowser = 0,
		WaiterBrowser = 1,
		WaiterPad = 2,
		WaiterApp = 3,
		Manager = 4
	}

	/// <summary>
	/// 订单
	/// </summary>
	public class Dine {
		[Key, MaxLength(14)]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public string Id { get; set; }
		/// <summary>
		/// 订单状态
		/// </summary>
		public DineStatus Status { get; set; }
		/// <summary>
		/// 订单类型
		/// </summary>
		public DineType Type { get; set; }
		public DineFrom From { get; set; }
		/// <summary>
		/// 人数
		/// </summary>
		public int HeadCount { get; set; }

		/// <summary>
		/// 总价
		/// </summary>
		public decimal Price { get; set; }
		public decimal OriPrice { get; set; }
		public decimal Change { get; set; }
		public double Discount { get; set; }
		[MaxLength(25)]
		public string DiscountName { get; set; }
		public DiscountType DiscountType { get; set; }

		/// <summary>
		/// 是否已经打印发票
		/// </summary>
		public bool IsInvoiced { get; set; }

		public DateTime BeginTime { get; set; }
		/// <summary>
		///  是否为线上支付
		/// </summary>
		public bool IsOnline { get; set; }

		public bool IsPaid { get; set; }
		/// <summary>
		/// 收银员编号
		/// </summary>
		[MaxLength(8)]
		public string ClerkId { get; set; }
		public Staff Clerk { get; set; }
		/// <summary>
		/// 服务员编号
		/// </summary>
		[MaxLength(8)]
		public string WaiterId { get; set; }
		public Staff Waiter { get; set; }
		/// <summary>
		/// 用户编号
		/// </summary>
		[MaxLength(10)]
		public string UserId { get; set; }

		public string DeskId { get; set; }
		public Desk Desk { get; set; }

		public string WeChatOpenId { get; set; }

		public ICollection<Remark> Remarks { get; set; }
		public ICollection<DineMenu> DineMenus { get; set; }
		public ICollection<DinePaidDetail> DinePaidDetails { get; set; }
		public ICollection<Invoice> Invoices { get; set; }
		public TakeOut TakeOut { get; set; }
	}

	public enum DineMenuType {
		None = 0,
		OnSale = 1,
		MenuDiscount = 2,
		PayKindDiscount = 3,
		TimeDiscount = 4,
		VipDiscount = 5,
		CustomDiscount = 6,
		SetMeal = 7,
		Gift = 8
	}
	public enum DineMenuStatus {
		/// <summary>
		/// 普通
		/// </summary>
		Normal = 0,
		/// <summary>
		/// 退回
		/// </summary>
		Returned = 1
	}
	/// <summary>
	/// 订单菜品详情，一对多表
	/// </summary>
	public class DineMenu {
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		public string DineId { get; set; }
		public Dine Dine { get; set; }

		public string MenuId { get; set; }
		public Menu Menu { get; set; }

		public DineMenuType Type { get; set; } = DineMenuType.None;
		public DineMenuStatus Status { get; set; } = DineMenuStatus.Normal;
		public int Count { get; set; }
		public decimal OriPrice { get; set; }
		public decimal Price { get; set; }
		public decimal RemarkPrice { get; set; }

		[ForeignKey(nameof(ReturnedWaiter))]
		public string ReturnedWaiterId { get; set; }
		public Staff ReturnedWaiter { get; set; }
		public string ReturnedReason { get; set; }

		public ICollection<Remark> Remarks { get; set; }
	}

	/// <summary>
	/// 支付明细表
	/// </summary>
	public class DinePaidDetail {
		[Key, Column(Order = 0)]
		public string DineId { get; set; }
		public Dine Dine { get; set; }

		[Key, Column(Order = 1)]
		public int PayKindId { get; set; }
		public PayKind PayKind { get; set; }

		public decimal Price { get; set; }
		/// <summary>
		/// 支付身份记录信息
		/// </summary>
		public string RecordId { get; set; }
	}

	public class ReturnedReason {
		[Key]
		public int Id { get; set; }
		public string Description { get; set; }
	}
}