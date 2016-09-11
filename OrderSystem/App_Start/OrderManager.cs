using HotelDAO.Models;
using OrderSystem.Models;
using Protocol;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using YummyOnlineDAO.Identity;
using YummyOnlineDAO.Models;

namespace OrderSystem {
	public class OrderManager : HotelManager {
		private class MenuExtensionWithGift {
			public MenuExtension MenuExtension { get; set; }
			public bool IsGift { get; set; }
		}
		public OrderManager(string connString) : base(connString) { }

		public async Task<FunctionResult> CreateDine(Cart cart, CartAddition addition) {
			// 主要支付方式判断
			DinePaidDetail mainPaidDetail = new DinePaidDetail {
				PayKind = await ctx.PayKinds.FirstOrDefaultAsync(p => p.Id == cart.PayKindId),
				Price = 0,
			};
			if(mainPaidDetail.PayKind == null) {
				return new FunctionResult(false, "未找到该支付方式", $"No PayKind {cart.PayKindId}");
			}
			if(addition.From == DineFrom.CustomerBrowser && !mainPaidDetail.PayKind.Usable) {
				return new FunctionResult(false, $"{mainPaidDetail.PayKind.Name}不可用", $"PayKind Disabled {mainPaidDetail.PayKind.Id}");
			}

			// 桌号判断
			Desk Desk = await ctx.Desks.Include(p => p.Area).FirstOrDefaultAsync(p => p.Id == cart.DeskId);
			if(Desk == null) {
				return new FunctionResult(false, "未找到当前桌号", $"No Desk {cart.DeskId}");
			}
			if(!Desk.Usable) {
				return new FunctionResult(false, $"{Desk.Name}不可用", $"Desk Disabled {Desk.Id}");
			}

			Dine dine = new Dine {
				Type = addition.DineType,
				HeadCount = cart.HeadCount,
				IsOnline = mainPaidDetail.PayKind.Type == PayKindType.Online,
				IsPaid = false,
				Desk = Desk,

				UserId = addition.UserId,
				WaiterId = addition.WaiterId,

				DineMenus = new List<DineMenu>(),
				Remarks = new List<Remark>(),
				DinePaidDetails = new List<DinePaidDetail>(),

				BeginTime = addition.BeginTime ?? DateTime.Now,
				From = addition.From
			};

			// 订单备注
			foreach(int remarkId in cart.Remarks) {
				Remark remark = ctx.Remarks.FirstOrDefault(p => p.Id == remarkId);
				if(remark == null) {
					return new FunctionResult(false, "未找到备注信息", $"No Remark {remarkId}");
				}

				dine.Price += remark.Price;
				dine.OriPrice += remark.Price;
				dine.Remarks.Add(remark);
			}

			// 是否有自定义折扣方案
			if(addition.Discount.HasValue) {
				dine.Discount = addition.Discount.Value;
				dine.DiscountName = addition.DiscountName;
				dine.DiscountType = DiscountType.Custom;
			}
			else {
				await handleDiscount(mainPaidDetail.PayKind, dine);
			}

			List<MenuExtensionWithGift> menuExtensionWithGifts = new List<MenuExtensionWithGift>();
			foreach(MenuExtension menuExtension in addition.GiftMenus) {
				menuExtensionWithGifts.Add(new MenuExtensionWithGift {
					MenuExtension = menuExtension,
					IsGift = true
				});
			}
			foreach(MenuExtension menuExtension in cart.OrderedMenus) {
				menuExtensionWithGifts.Add(new MenuExtensionWithGift {
					MenuExtension = menuExtension,
					IsGift = false
				});
			}
			// 处理每个点过的菜品
			FunctionResult result = await handleDineMenu(menuExtensionWithGifts, dine);
			if(!result.Succeeded) {
				return result;
			}
			// 处理最后生成的价格并比较前端传输的价格数据
			result = await handleDinePrice(dine, cart.Price);
			if(!result.Succeeded) {
				return result;
			}

			mainPaidDetail.Price = dine.Price;

			// 如果是线上支付并且使用了积分抵扣
			if(mainPaidDetail.PayKind.Type == PayKindType.Online && cart.PriceInPoints.HasValue) {
				result = await handlePoints(cart.PriceInPoints.Value, mainPaidDetail, dine);
				if(!result.Succeeded) {
					return result;
				}
			}

			// 随机立减
			if(mainPaidDetail.PayKind.Type == PayKindType.Online) {
				HotelConfig hotelConfig = await ctx.HotelConfigs.FirstOrDefaultAsync();
				// 如果饭店支持随机立减
				if(hotelConfig.NeedRandomPreference) {
					await handleRandomPreference(hotelConfig.Id, dine, mainPaidDetail);
				}
			}

			// 如果是线上支付，则添加DinePaidDetail信息，否则不添加，交给收银系统处理
			if(mainPaidDetail.PayKind.Type == PayKindType.Online) {
				dine.DinePaidDetails.Add(mainPaidDetail);
			}


			// 订单发票
			if(cart.Invoice != null) {
				dine.Invoices.Add(new Invoice {
					Price = dine.Price,
					Title = cart.Invoice
				});
			}

			// 外卖处理
			if(dine.Desk.Area.Type == AreaType.TakeOut) {
				dine.Type = DineType.ToGo;
				ctx.TakeOuts.Add(new TakeOut {
					Dine = dine,
					Address = cart.TakeOut.Address,
					Name = cart.TakeOut.Name,
					PhoneNumber = cart.TakeOut.PhoneNumber
				});
				if(dine.UserId != null) {
					YummyOnlineContext yummyOnlineCtx = new YummyOnlineContext();
					var userAddress = await yummyOnlineCtx.UserAddresses.FirstOrDefaultAsync(p => p.UserId == dine.UserId && p.Address == cart.TakeOut.Address);
					if(userAddress == null) {
						yummyOnlineCtx.UserAddresses.Add(new UserAddress {
							UserId = dine.UserId,
							Address = cart.TakeOut.Address
						});
					}
					await yummyOnlineCtx.SaveChangesAsync();
				}
			}

			ctx.Dines.Add(dine);

			await ctx.SaveChangesAsync();

			return new FunctionResult(true, dine);
		}

		public async Task<FunctionResult> AddMenus(string dineId, List<MenuExtension> orderedMenus, decimal price) {
			Dine dine = await ctx.Dines.Include(p => p.DineMenus).FirstOrDefaultAsync(p => p.Id == dineId);
			if(dine == null) {
				return new FunctionResult(false, "订单号不存在", $"No DineId {dineId}");
			}

			List<MenuExtensionWithGift> menuExtensionWithGifts = new List<MenuExtensionWithGift>();
			foreach(MenuExtension menuExtension in orderedMenus) {
				menuExtensionWithGifts.Add(new MenuExtensionWithGift {
					MenuExtension = menuExtension,
					IsGift = false
				});
			}

			// 处理每个点过的菜品
			FunctionResult result = await handleDineMenu(menuExtensionWithGifts, dine);
			if(!result.Succeeded) {
				return result;
			}
			List<DineMenu> addedDineMenus = result.Data as List<DineMenu>;
			// 处理最后生成的价格并比较前端传输的价格数据
			//result = await handleDinePrice(dine, price);
			//if(!result.Succeeded) {
			//	return result;
			//}

			await ctx.SaveChangesAsync();

			return new FunctionResult(true, addedDineMenus);
		}

		/// <summary>
		/// 整单打折处理
		/// </summary>
		/// <param name="payKind"></param>
		/// <param name="dine"></param>
		/// <returns></returns>
		private async Task handleDiscount(PayKind payKind, Dine dine) {
			double minDiscount = 1;
			string minDiscountName = null;
			DiscountType minDiscountType = DiscountType.None;

			if(payKind.Discount < minDiscount) {
				minDiscount = payKind.Discount;
				minDiscountName = payKind.Name + "折扣";
				minDiscountType = DiscountType.PayKind;
			}

			DayOfWeek week = DateTime.Now.DayOfWeek;
			List<TimeDiscount> timeDicsounts = await ctx.TimeDiscounts.Where(p => p.Week == week).ToListAsync();
			TimeSpan now = DateTime.Now.TimeOfDay;
			foreach(TimeDiscount timeDiscount in timeDicsounts) {
				if(now >= timeDiscount.From && now <= timeDiscount.To) {
					if(timeDiscount.Discount < minDiscount) {
						minDiscount = timeDiscount.Discount;
						minDiscountName = timeDiscount.Name;
						minDiscountType = DiscountType.Time;
					}
					break;
				}
			}

			Customer customer = await ctx.Customers.FirstOrDefaultAsync(p => p.Id == dine.UserId);
			if(customer != null) {
				VipDiscount vipDiscounts = await ctx.VipDiscounts.FirstOrDefaultAsync(p => p.Level.Id == customer.VipLevelId);
				if(vipDiscounts != null) {
					if(vipDiscounts.Discount < minDiscount) {
						minDiscount = vipDiscounts.Discount;
						minDiscountName = vipDiscounts.Name;
						minDiscountType = DiscountType.Vip;
					}
				}
			}

			dine.Discount = minDiscount;
			dine.DiscountName = minDiscountName;
			dine.DiscountType = minDiscountType;
		}

		/// <summary>
		/// 处理每个菜品
		/// </summary>
		private async Task<FunctionResult> handleDineMenu(List<MenuExtensionWithGift> menuExtensionWithGifts, Dine dine) {
			List<DineMenu> addedDineMenus = new List<DineMenu>();

			foreach(MenuExtensionWithGift menuExtensionWithGift in menuExtensionWithGifts) {
				MenuExtension menuExtension = menuExtensionWithGift.MenuExtension;
				Menu menu = await ctx.Menus
					.Include(p => p.MenuPrice)
					.FirstOrDefaultAsync(p => p.Id == menuExtension.Id);
				// 菜品判断
				if(menu == null) {
					return new FunctionResult(false, "未找到菜品", $"No Menu {menuExtension.Id}");
				}
				if(!menu.Usable) {
					return new FunctionResult(false, $"{menu.Name} 不可用", $"Menu Disabled {menu.Id}: {menu.Name}");
				}
				if(dine.From == DineFrom.CustomerBrowser && menu.Status == MenuStatus.SellOut) {
					return new FunctionResult(false, $"{menu.Name} 已售完", $"Menu SellOut {menu.Id}: {menu.Name}");
				}

				DineMenu dineMenu = new DineMenu {
					Count = menuExtension.Ordered,
					OriPrice = menu.MenuPrice.Price,
					Price = menuExtensionWithGift.IsGift ? 0 : menu.MenuPrice.Price,
					RemarkPrice = 0,

					Menu = menu,
					Remarks = new List<Remark>(),
					Type = menuExtensionWithGift.IsGift ? DineMenuType.Gift : DineMenuType.None
				};

				if(!menuExtensionWithGift.IsGift) {
					// 是否排除在总单打折之外
					bool excludePayDiscount = menu.MenuPrice.ExcludePayDiscount;

					// 是否打折
					if(menu.MenuPrice.Discount < 1) {
						excludePayDiscount = true;
						dineMenu.Price = menu.MenuPrice.Price * (decimal)menu.MenuPrice.Discount;
						dineMenu.Type = DineMenuType.MenuDiscount;
					}
					// 是否为特价菜
					DayOfWeek week = DateTime.Now.DayOfWeek;
					MenuOnSale menuOnSales = await ctx.MenuOnSales.FirstOrDefaultAsync(p => p.Id == menu.Id && p.OnSaleWeek == week);
					if(menuOnSales != null) {
						excludePayDiscount = true;
						dineMenu.Price = menuOnSales.Price;
						dineMenu.Type = DineMenuType.OnSale;
					}
					// 是否为套餐
					var menuSetMeals = await ctx.MenuSetMeals.FirstOrDefaultAsync(p => p.MenuSetId == menu.Id && p.Menu.IsSetMeal);
					if(menuSetMeals != null) {
						excludePayDiscount = true;
						dineMenu.Type = DineMenuType.SetMeal;
					}

					if(!excludePayDiscount) {
						dineMenu.Price = menu.MenuPrice.Price * (decimal)dine.Discount;
						switch(dine.DiscountType) {
							case DiscountType.PayKind:
								dineMenu.Type = DineMenuType.PayKindDiscount;
								break;
							case DiscountType.Vip:
								dineMenu.Type = DineMenuType.VipDiscount;
								break;
							case DiscountType.Time:
								dineMenu.Type = DineMenuType.TimeDiscount;
								break;
							case DiscountType.Custom:
								dineMenu.Type = DineMenuType.CustomDiscount;
								break;
						}
					}
				}

				// 菜品备注处理
				foreach(int remarkId in menuExtension.Remarks) {
					Remark remark = await ctx.Remarks.FirstOrDefaultAsync(p => p.Id == remarkId);

					if(remark == null) {
						return new FunctionResult(false, "未找到备注信息", $"No Remark {remarkId}");
					}

					if(!menuExtensionWithGift.IsGift) {
						dineMenu.RemarkPrice += remark.Price;
					}

					dineMenu.Remarks.Add(remark);
				}

				dine.Price += dineMenu.Price * dineMenu.Count + dineMenu.RemarkPrice;
				dine.OriPrice += dineMenu.OriPrice * dineMenu.Count + dineMenu.RemarkPrice;

				addedDineMenus.Add(dineMenu);
				dine.DineMenus.Add(dineMenu);
			}

			return new FunctionResult(true, addedDineMenus);
		}

		/// <summary>
		/// 处理订单的价格并验证是否与前端的价格相同
		/// </summary>
		private async Task<FunctionResult> handleDinePrice(Dine dine, decimal? cartPrice) {
			HotelConfig hotelConfig = await ctx.HotelConfigs.FirstOrDefaultAsync();
			int trim = 100;
			switch(hotelConfig.TrimZero) {
				case TrimZero.Jiao:
					trim = 10;
					break;
				case TrimZero.Yuan:
					trim = 1;
					break;
			}

			dine.Price = Math.Floor(dine.Price * trim) / trim;
			cartPrice = cartPrice ?? 0;
			cartPrice = Math.Floor(cartPrice.Value * trim) / trim;

			foreach(DineMenu dineMenu in dine.DineMenus) {
				var menuOnSale = await ctx.MenuOnSales.FirstOrDefaultAsync(p => p.Id == dineMenu.Menu.Id && p.OnSaleWeek == dine.BeginTime.DayOfWeek);
				if(menuOnSale == null)
					continue;

				if(dine.From == DineFrom.CustomerBrowser && menuOnSale.MinPrice > dine.OriPrice - dineMenu.OriPrice) {
					return new FunctionResult(false, $"{dineMenu.Menu.Name} 不满最低消费￥{menuOnSale.MinPrice}",
						$"MenuOnSale Price Error, MenuId: {dineMenu.Menu.Id}, Dine OriPrice: {dine.OriPrice}");
				}
			}

			// 检测前端计算的金额与后台计算的金额是否相同，如果前端金额为null则检测
			if(dine.From != DineFrom.CustomerBrowser && Math.Abs(dine.Price - cartPrice.Value) > 0.01m) {
				return new FunctionResult(false, "金额有误",
					$"Price Error, Cart Price: {cartPrice.Value}, Cal Price: {dine.Price}");
			}

			return new FunctionResult();
		}

		/// <summary>
		/// 积分处理
		/// </summary>
		private async Task<FunctionResult> handlePoints(decimal priceInPoints, DinePaidDetail mainPaidDetail, Dine dine) {
			if(priceInPoints == 0) {
				return new FunctionResult();
			}

			HotelConfig hotelConfig = await ctx.HotelConfigs.FirstOrDefaultAsync();

			DinePaidDetail pointsPaidDetail = new DinePaidDetail {
				PayKind = await ctx.PayKinds.FirstOrDefaultAsync(p => p.Type == PayKindType.Points),
				Price = priceInPoints
			};

			Customer customer = await ctx.Customers.FirstOrDefaultAsync(p => p.Id == dine.UserId);
			int customerPoints = customer == null ? 0 : customer.Points;

			if(pointsPaidDetail.Price > customerPoints / hotelConfig.PointsRatio) {
				return new FunctionResult(false, "积分不足",
					$"Points Error, Cart PointsPrice: {pointsPaidDetail.Price}, Real Points Price {customerPoints / hotelConfig.PointsRatio}");
			}
			if(pointsPaidDetail.Price > dine.Price) {
				pointsPaidDetail.Price = dine.Price;
			}

			mainPaidDetail.Price -= pointsPaidDetail.Price;

			dine.DinePaidDetails.Add(pointsPaidDetail);

			return new FunctionResult();
		}

		/// <summary>
		/// 随机立减处理
		/// </summary>
		/// <returns></returns>
		private async Task handleRandomPreference(int hotelId, Dine dine, DinePaidDetail mainPaidDetail) {
			PayKind randomPreferencePayKind = await ctx.PayKinds.FirstOrDefaultAsync(p => p.Type == PayKindType.RandomPreference && p.Usable == true);
			if(randomPreferencePayKind != null) {
				int top = (int)Math.Ceiling(mainPaidDetail.Price / 50);

				Random random = new Random(DateTime.Now.Millisecond);
				decimal randomPrice = (decimal)random.NextDouble();
				randomPrice = randomPrice + top - 1;
				randomPrice = Math.Floor(randomPrice * 10) / 10;

				if(randomPrice == 0) {
					randomPrice = 0.1m;
				}

				// 如果随机立减超过应付金额则全额支付
				if(randomPrice > mainPaidDetail.Price) {
					randomPrice = mainPaidDetail.Price;
				}

				if(randomPrice != 0) {
					dine.DinePaidDetails.Add(new DinePaidDetail {
						PayKind = randomPreferencePayKind,
						Price = randomPrice
					});
					mainPaidDetail.Price -= randomPrice;
				}
			}
		}

		public async Task OfflinePayCompleted(string dineId) {
			Dine dine = await ctx.Dines.FirstOrDefaultAsync(p => p.Id == dineId);
			dine.IsPaid = true;

			await changeCustomerPoints(dine);

			await ctx.SaveChangesAsync();
		}
		public async Task<bool> OfflinePayCompleted(WaiterPaidDetails paidDetails) {
			Dine dine = await ctx.Dines.FirstOrDefaultAsync(p => p.Id == paidDetails.DineId);
			List<DinePaidDetail> exisedPaidDetails = await ctx.DinePaidDetails.Where(p => p.Dine.Id == paidDetails.DineId).ToListAsync();

			decimal price = 0;
			foreach(var p in exisedPaidDetails) {
				price += p.Price;
			}
			foreach(var p in paidDetails.PaidDetails) {
				price += p.Price;
				ctx.DinePaidDetails.Add(new DinePaidDetail {
					DineId = paidDetails.DineId,
					PayKindId = p.PayKindId,
					Price = p.Price,
					RecordId = p.RecordId
				});
			}
			if(Math.Abs(dine.Price - price) > 0.01m) {
				return false;
			}
			dine.IsPaid = true;

			await changeCustomerPoints(dine);

			await ctx.SaveChangesAsync();

			return true;
		}

		public async Task OnlinePayCompleted(string dineId, string recordId) {
			Dine dine = await ctx.Dines.FirstOrDefaultAsync(p => p.Id == dineId);
			dine.IsPaid = true;

			DinePaidDetail mainPaidDetail = await ctx.DinePaidDetails.FirstOrDefaultAsync(p => p.Dine.Id == dineId && p.PayKind.Type == PayKindType.Online);
			mainPaidDetail.RecordId = recordId;

			await changeCustomerPoints(dine);

			await ctx.SaveChangesAsync();
		}
		private async Task changeCustomerPoints(Dine dine) {
			// 用户总平台消费金额
			var yummyonlineCtx = new YummyOnlineContext();
			User user = await yummyonlineCtx.Users.FirstOrDefaultAsync(p => p.Id == dine.UserId);
			if(user == null) {
				return;
			}
			user.Price += dine.Price;
			await yummyonlineCtx.SaveChangesAsync();

			Customer customer = await ctx.Customers.FirstOrDefaultAsync(p => p.Id == dine.UserId);
			// 如果用户不存在或者是匿名用户
			if(customer == null || await new UserManager().IsInRoleAsync(dine.UserId, Role.Nemo)) {
				return;
			}
			// 如果使用的积分支付
			DinePaidDetail pointsPaidDetail = await ctx.DinePaidDetails.FirstOrDefaultAsync(p => p.Dine.Id == dine.Id && p.PayKind.Type == PayKindType.Points);
			if(pointsPaidDetail != null) {
				HotelConfig config = await ctx.HotelConfigs.FirstOrDefaultAsync();
				customer.Points -= Convert.ToInt32((double)pointsPaidDetail.Price / config.PointsRatio);
			}
			// 用户点过的菜品增加积分
			List<DineMenu> dineMenus = await ctx.DineMenus.Include(p => p.Menu.MenuPrice).Where(p => p.Dine.Id == dine.Id).ToListAsync();
			dineMenus?.ForEach(m => {
				customer.Points += m.Menu.MenuPrice.Points * m.Count;
			});
		}

		public async Task PrintCompleted(string dineId) {
			Dine dine = await ctx.Dines.FirstOrDefaultAsync(p => p.Id == dineId);
			dine.Status = DineStatus.Printed;
			await ctx.SaveChangesAsync();
		}
	}
}