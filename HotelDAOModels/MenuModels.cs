using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelDAO.Models {
	/// <summary>
	/// 菜品分类
	/// </summary>
	public class MenuClass {
		[Key, MaxLength(6)]
		public string Id { get; set; }
		[MaxLength(20), Required]
		public string Name { get; set; }
		[MaxLength(50)]
		public string Description { get; set; }
		public int? Level { get; set; }
		/// <summary>
		/// 是否在点菜界面上显示
		/// </summary>
		public bool IsShow { get; set; }
		public bool Usable { get; set; }

		public bool IsLeaf { get; set; }

		public string ParentMenuClassId { get; set; }
		public MenuClass ParentMenuClass { get; set; }

		public ICollection<Menu> Menus { get; set; }
	}


	public enum MenuStatus {
		/// <summary>
		/// 正常
		/// </summary>
		Normal = 0,
		/// <summary>
		/// 已售完
		/// </summary>
		SellOut = 1
	}
	/// <summary>
	/// 菜品总表
	/// </summary>
	public class Menu {
		[Key, MaxLength(6)]
		public string Id { get; set; }

		public MenuStatus Status { get; set; }

		[MaxLength(10)]
		public string Code { get; set; }

		[MaxLength(30), Required]
		public string Name { get; set; }
		[MaxLength(30)]
		public string EnglishName { get; set; }

		[MaxLength(15), Required]
		public string NameAbbr { get; set; }

		public string PicturePath { get; set; }

		/// <summary>
		/// 是否在菜单中始终显示（不管有没有点菜）
		/// </summary>
		/// 
		public bool IsFixed { get; set; }
		public int SupplyDate { get; set; }

		/// <summary>
		/// 单位
		/// </summary>
		[MaxLength(3)]
		public string Unit { get; set; }

		/// <summary>
		/// 最少可点数量
		/// </summary>
		public int MinOrderCount { get; set; }

		/// <summary>
		/// 点过菜的人数
		/// </summary>
		public int Ordered { get; set; }

		public int SourDegree { get; set; }
		public int SweetDegree { get; set; }
		public int SaltyDegree { get; set; }
		public int SpicyDegree { get; set; }

		public bool Usable { get; set; }
		public bool IsSetMeal { get; set; }

		public int DepartmentId { get; set; }
		public Department Department { get; set; }

		public MenuPrice MenuPrice { get; set; }
		public ICollection<MenuClass> Classes { get; set; }
		public ICollection<Remark> Remarks { get; set; }
	}
	/// <summary>
	/// 菜品价格积分相关，一对一表
	/// </summary>
	public class MenuPrice {
		[Key, ForeignKey(nameof(Menu))]
		public string Id { get; set; }
		public Menu Menu { get; set; }

		/// <summary>
		/// 是否算在支付方式打折中
		/// </summary>
		public bool ExcludePayDiscount { get; set; } = false;
		public decimal Price { get; set; }
		public double Discount { get; set; }
		/// <summary>
		/// 积分
		/// </summary>
		public int Points { get; set; }
	}

	/// <summary>
	/// 打折菜品表
	/// </summary>
	public class MenuOnSale {
		[Key, Column(Order = 0)]
		public string Id { get; set; }
		public Menu Menu { get; set; }

		[Key, Column(Order = 1)]
		public DayOfWeek OnSaleWeek { get; set; }

		public decimal Price { get; set; }
		public decimal MinPrice { get; set; }
	}
	/// <summary>
	/// 套餐菜品表
	/// </summary>
	public class MenuSetMeal {
		// 该类使用了Fluent API来删除级联
		// 套餐菜品
		[Key, Column(Order = 0)]
		public string MenuSetId { get; set; }
		public Menu MenuSet { get; set; }

		// 套餐菜品对应的单个菜品
		[Key, Column(Order = 1)]
		public string MenuId { get; set; }
		public Menu Menu { get; set; }

		public int Count { get; set; }
	}
}