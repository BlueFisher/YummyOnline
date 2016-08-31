using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OrderSystem.Waiter.Models {
	public class Shift {
		public decimal OriPrice { get; set; }
		public decimal Price { get; set; }
		public decimal ToStayPrice { get; set; }
		public decimal ToGoPrice { get; set; }
		public decimal PreferencePrice { get; set; }
		public decimal GiftPrice { get; set; }
		public decimal ReturnedPrice { get; set; }
		public decimal AveragePrice { get; set; }
		public int DeskCount { get; set; }
		public int CustomerCount { get; set; }

		public List<PayKindShiftDetail> PayKindShifts { get; set; } = new List<PayKindShiftDetail>();
		public List<MenuClassShiftDetail> MenuClassShifts { get; set; } = new List<MenuClassShiftDetail>();
	}

	public class PayKindShiftDetail {
		public int PayKindId { get; set; }
		public decimal ReceivablePrice { get; set; }
		public decimal RealPrice { get; set; }
	}
	public class MenuClassShiftDetail {
		public string MenuClassId { get; set; }
		public decimal Price { get; set; }
	}
}