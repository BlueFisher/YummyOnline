using HotelDAO.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OrderSystem.Models {
	public class MenuExtension {
		public string Id { get; set; }
		public int Ordered { get; set; }
		public List<int> Remarks { get; set; } = new List<int>();
	}
	public class CartTakeOut {
		public string Name { get; set; }
		public string Address { get; set; }
		public string PhoneNumber { get; set; }
	}
	public class Cart {
		public int HeadCount { get; set; }
		/// <summary>
		/// 实际支付价格
		/// </summary>
		public decimal? Price { get; set; }
		/// <summary>
		/// 积分抵扣价格
		/// </summary>
		public decimal? PriceInPoints { get; set; }

		public string Invoice { get; set; }
		public int PayKindId { get; set; }
		public string DeskId { get; set; }

		public List<MenuExtension> OrderedMenus { get; set; } = new List<MenuExtension>();
		public List<int> Remarks { get; set; } = new List<int>();

		public CartTakeOut TakeOut { get; set; }
	}
	public class CartAddition {
		public string UserId { get; set; }
		public string WaiterId { get; set; }
		public DineType DineType { get; set; }
		public double? Discount { get; set; }
		public string DiscountName { get; set; }
		public DateTime? BeginTime { get; set; }
		public DineFrom From { get; set; }
		public List<MenuExtension> GiftMenus { get; set; } = new List<MenuExtension>();
	}


	public class WaiterCartAddition {
		public string UserId { get; set; }
		public double? Discount { get; set; }
		public string DiscountName { get; set; }
		public DateTime? BeginTime { get; set; }
		public DineFrom From { get; set; }
	}

	public class ManagerCartAddition {
		public string Token { get; set; }
		public int HotelId { get; set; }
		public DineType DineType { get; set; } = DineType.ToStay;
		public string WaiterId { get; set; }
		public string UserId { get; set; }
		public double? Discount { get; set; }
		public string DiscountName { get; set; }
		public List<MenuExtension> GiftMenus { get; set; } = new List<MenuExtension>();
	}


	public class WaiterPaidDetails {
		public class PaidDetail {
			public int PayKindId { get; set; }
			public decimal Price { get; set; }
			public string RecordId { get; set; }
		}

		public string DineId { get; set; }
		public List<PaidDetail> PaidDetails { get; set; } = new List<PaidDetail>();
	}
}