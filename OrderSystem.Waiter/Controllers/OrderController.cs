using HotelDAO.Models;
using OrderSystem.Utility;
using Protocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using Utility;
using YummyOnlineDAO.Identity;

namespace OrderSystem.Waiter.Controllers {
	[Authorize(Roles = nameof(Schema.ReadWaiterData))]
	[HotelAvailable]
	public class OrderController : BaseWaiterController {
		// GET: Order

		public ActionResult Index() {
			return View();
		}

		public ActionResult _ViewCart() {
			return View();
		}
		public ActionResult _ViewPayment() {
			return View();
		}
		public ActionResult _ViewCurrent() {
			return View();
		}

		public async Task<JsonResult> GetMenuInfos() {
			string connStr = CurrHotel.ConnectionString;
			var tMenuClasses = new HotelManager(connStr).GetFormatedMenuClasses();
			var tMenus = new HotelManager(connStr).GetFormatedMenus();
			var tMenuOnSales = new HotelManager(connStr).GetFormatedMenuOnSales();
			var tMenuSetMeals = new HotelManager(connStr).GetFormatedMenuSetMeals();
			var tHotel = new HotelManager(connStr).GetHotelConfig();
			var tTimeDiscounts = new HotelManager(connStr).GetTimeDiscounts();
			var tVipDiscounts = new HotelManager(connStr).GetVipDiscounts();

			var tPayKind = new HotelManager(connStr).GetOtherPayKind();
			var tDesks = new HotelManager(connStr).GetDesks();

			var result = new {
				MenuClasses = await tMenuClasses,
				Menus = await tMenus,
				MenuOnSales = await tMenuOnSales,
				MenuSetMeals = await tMenuSetMeals,
				DiscountMethods = new {
					TimeDiscounts = await tTimeDiscounts,
					VipDiscounts = await tVipDiscounts
				},
				Hotel = DynamicsCombination.CombineDynamics(await tHotel, new {
					CurrHotel.Name,
					CurrHotel.Address,
					CurrHotel.Tel,
					CurrHotel.OpenTime,
					CurrHotel.CloseTime
				}),
				Desks = await tDesks,
				PayKind = await tPayKind,
			};
			return Json(result);
		}

		public async Task<JsonResult> GetHotelInfos() {
			string connStr = CurrHotel.ConnectionString;

			var tAreas = new HotelManager(connStr).GetAreas();
			var tPayKinds = new HotelManager(connStr).GetFormatedPayKinds(new List<PayKindType> {
				PayKindType.Points, PayKindType.Offline, PayKindType.Online, PayKindType.Cash ,PayKindType.RandomPreference});
			var tRemarks = new HotelManager(connStr).GetRemarks();
			var tStaffs = new HotelManager(connStr).GetStaffs();
			var tSellOutMenus = new HotelManager(connStr).GetFormatedMenus(MenuStatus.SellOut);

			var result = new {
				Areas = await tAreas,
				PayKinds = await tPayKinds,
				Remarks = await tRemarks,
				Staffs = await tStaffs,
				SellOutMenus = await tSellOutMenus
			};
			return Json(result);
		}
		public async Task<JsonResult> GetHotelConfig() {
			return Json(DynamicsCombination.CombineDynamics(await HotelManager.GetHotelConfig(), new {
				CurrHotel.Name,
				CurrHotel.Address,
				CurrHotel.Tel,
				CurrHotel.OpenTime,
				CurrHotel.CloseTime
			}));
		}

		public async Task<JsonResult> GetCurrentDines(string deskId) {
			return Json(await HotelManager.GetHistoryDines(User.Identity.GetUserId(), deskId));
		}
		public async Task<JsonResult> GetHistoryDines() {
			return Json(await HotelManager.GetHistoryDines(User.Identity.GetUserId()));
		}
		public async Task<JsonResult> GetAllHistoryDines() {
			return Json(await HotelManager.GetHistoryDines());
		}

		public async Task<JsonResult> GetDineById(string dineId) {
			return Json(await HotelManager.GetFormatedDineById(dineId));
		}

		public async Task<JsonResult> ShiftDines() {
			await HotelManager.ShiftDines();
			return Json(new JsonSuccess());
		}
		public async Task<JsonResult> ToggleMenuStatus(string menuId, MenuStatus status) {
			if(!await HotelManager.ToggleMenuStatus(menuId, status)) {
				return Json(new JsonError());
			}
			return Json(new JsonSuccess());
		}

		public async Task<JsonResult> AddShiftsInfo(Models.Shift shift) {
			await HotelManager.AddShifts(shift);
			await HotelManager.RecordLog(Log.LogLevel.Info, $"Add Shifts Info", HttpPost.GetPostData(Request));
			return Json(new JsonSuccess());
		}
	}
}