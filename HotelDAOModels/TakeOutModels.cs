using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelDAO.Models {
	/// <summary> 
	/// 订单外卖，一对一表
	/// </summary>
	public class TakeOut {
		[Key, ForeignKey(nameof(Dine))]
		public string DineId { get; set; }
		public Dine Dine { get; set; }

		[MaxLength(128)]
		public string Address { get; set; }
		[MaxLength(11)]
		public string PhoneNumber { get; set; }
		[MaxLength(20)]
		public string Name { get; set; }

		public string RecordId { get; set; }
	}
}
