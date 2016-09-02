using HotelDAO.Models;
using Newtonsoft.Json;
using OrderSystem.Models;
using Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Utility;
using YummyOnlineDAO.Identity;
using YummyOnlineDAO.Models;

namespace OrderSystem.Controllers {
	public partial class PaymentController : BaseOrderSystemController {

		private OrderManager _orderManager;
		public OrderManager OrderManager {
			get {
				if(_orderManager == null) {
					_orderManager = new OrderManager(CurrHotel.ConnectionString);
				}
				return _orderManager;
			}
		}

		/// <summary>
		/// 支付完成页面，无关数据记录
		/// </summary>
		public ActionResult Complete(bool? succeeded, string dineId) {
			ViewBag.Succeeded = succeeded == null ? true : succeeded;
			ViewBag.DineId = dineId;
			return View();
		}

		/// <summary>
		/// 普通用户支付
		/// </summary>
		[RequireHotel]
		[HotelAvailable]
		public async Task<JsonResult> Pay(Cart cart) {
			CartAddition addition = new CartAddition();

			// 新建或获取用户Id
			User user = await createOrGetUser(User.Identity.GetUserId(), "OrderSystem");
			if(user == null) {
				return Json(new JsonError("创建匿名用户失败"));
			}
			SigninManager.Signin(user, true);
			addition.UserId = user.Id;

			// 创建新订单
			FunctionResult result = await OrderManager.CreateDine(cart, addition);
			if(!result.Succeeded) {
				if(await UserManager.IsInRoleAsync(user.Id, Role.Nemo)) {
					await UserManager.DeleteAsync(user);
					await YummyOnlineManager.RecordLog(YummyOnlineDAO.Models.Log.LogProgram.Identity, YummyOnlineDAO.Models.Log.LogLevel.Warning, $"Anonymous User Deleted {user.Id}, Via OrderSystem");
				}
				await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Error, $"{result.Detail}, Host:{Request.UserHostAddress}", HttpPost.GetPostData(Request));
				return Json(new JsonError(result.Message));
			}

			Dine dine = ((Dine)result.Data);

			await newDineInform(dine, "OrderSystem");

			PayKind payKind = await HotelManager.GetPayKindById(cart.PayKindId);
			string redirectUrl = $"{payKind.CompleteUrl}?Succeeded={true}&DineId={dine.Id}";

			if(payKind.Type == PayKindType.Online) {
				DinePaidDetail mainPaidDetail = await HotelManager.GetDineOnlinePaidDetail(dine.Id);

				// 如果实际需要支付的价格等于0则直接显示支付完成界面
				if(mainPaidDetail.Price == 0) {
					await onlinePayCompleted(dine.Id, null);
				}
				else {
					redirectUrl = await getOnlineRedirectUrl(dine.Id);
				}
			}
			else {
				HotelConfig config = await HotelManager.GetHotelConfig();
				if(config.IsPrintReciptAfterPayingOffline) {
					await requestPrintDine(dine.Id, new List<PrintType> { PrintType.Recipt });
				}
			}

			return Json(new JsonSuccess(redirectUrl));
		}

		/// <summary>
		/// 收银员台支付
		/// </summary>
		public async Task<ActionResult> ManagerPay(Cart cart, ManagerCartAddition cartAddition) {
			SystemConfig system = await YummyOnlineManager.GetSystemConfig();
			if(system.Token != cartAddition.Token) {
				return Json(new JsonError("身份验证失败"));
			}

			var hotel = await YummyOnlineManager.GetHotelById(cartAddition.HotelId);
			CurrHotel = new CurrHotelInfo(hotel.Id, hotel.ConnectionString);

			if(!hotel.Usable)
				return RedirectToAction("HotelUnavailable", "Error");

			cart.PayKindId = await new HotelManager(CurrHotel.ConnectionString).GetOtherPayKindId();
			CartAddition addition = new CartAddition {
				WaiterId = cartAddition.WaiterId,
				DineType = cartAddition.DineType,
				Discount = cartAddition.Discount,
				DiscountName = cartAddition.DiscountName,
				GiftMenus = cartAddition.GiftMenus,
				From = DineFrom.Manager
			};

			User user = await UserManager.FindByIdAsync(cartAddition.UserId);
			addition.UserId = user?.Id;

			// 创建新订单
			FunctionResult result = await OrderManager.CreateDine(cart, addition);
			if(!result.Succeeded) {
				if(await UserManager.IsInRoleAsync(user.Id, Role.Nemo)) {
					await UserManager.DeleteAsync(user);
					await YummyOnlineManager.RecordLog(YummyOnlineDAO.Models.Log.LogProgram.Identity, YummyOnlineDAO.Models.Log.LogLevel.Warning, $"Anonymous User Deleted {user.Id}, Via Manager");
				}
				await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Error, $"{result.Detail}, Host:{Request.UserHostAddress}", HttpPost.GetPostData(Request));
				return Json(new JsonError(result.Message));
			}

			Dine dine = ((Dine)result.Data);

			await newDineInform(dine, "Manager");

			return Json(new JsonSuccess { Data = dine.Id });
		}


		/// <summary>
		/// 重新支付
		/// </summary>
		public async Task<JsonResult> PayAgain(string dineId) {
			return Json(new JsonSuccess(await getOnlineRedirectUrl(dineId)));
		}

		/// <summary>
		/// 支付完成异步通知
		/// </summary>
		public async Task<JsonResult> OnlineNotify(string encryptedInfo) {
			string decryptedInfo = DesCryptography.DesDecrypt(encryptedInfo);
			NetworkNotifyViewModels model = null;

			try {
				model = JsonConvert.DeserializeObject<NetworkNotifyViewModels>(decryptedInfo);
			}
			catch {
				return Json(new JsonError());
			}

			CurrHotel = new CurrHotelInfo(await YummyOnlineManager.GetHotelById(model.HotelId));

			await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Info, $"Notified DineId: {model.DineId}");
			await onlinePayCompleted(model.DineId, model.RecordId);

			return Json(new JsonSuccess());
		}

		/// <summary>
		/// 打印完成
		/// </summary>
		public async Task<JsonResult> PrintCompleted(int hotelId, string dineId) {
			CurrHotel = new CurrHotelInfo(await YummyOnlineManager.GetHotelById(hotelId));

			if(dineId == "00000000000000") {
				await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Success, $"Print Test Dine Completed");
				return Json(new JsonSuccess());
			}

			await OrderManager.PrintCompleted(dineId);
			await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Success, $"PrintCompleted DineId: {dineId}");
			return Json(new JsonSuccess());
		}
	}


	// PaymentController辅助函数
	public partial class PaymentController {
		/// <summary>
		/// 创建或获取用户
		/// </summary>
		/// <param name="userId">用户id</param>
		/// <returns>当前或新建的用户</returns>
		private async Task<User> createOrGetUser(string userId, string via) {
			User user = await UserManager.FindByIdAsync(userId);
			if(user == null) {
				user = await UserManager.CreateVoidUserAsync();
				if(user == null) {
					return null;
				}
				await YummyOnlineManager.RecordLog(YummyOnlineDAO.Models.Log.LogProgram.Identity, YummyOnlineDAO.Models.Log.LogLevel.Success, $"Anonymous User Created {user.Id}, Via {via}");
				await UserManager.AddToRoleAsync(user.Id, Role.Nemo);
			}
			return user;
		}
		private async Task newDineInform(Dine dine, string via) {
			await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Success, $"Dine recorded DineId: {dine.Id}, IsOnline: {dine.IsOnline}, Via {via}, Host: {Request.UserHostAddress}",
				HttpPost.GetPostData(Request));
			NewDineInformTcpClient.SendNewDineInfrom(CurrHotel.Id, dine.Id, false);
		}
		/// <summary>
		/// 请求打印订单
		/// </summary>
		/// <param name="dineId">订单号</param>
		private async Task requestPrintDine(string dineId, List<PrintType> printTypes) {
			HotelConfig config = await HotelManager.GetHotelConfig();
			if(config.HasAutoPrinter) {
				NewDineInformTcpClient.SendRequestPrintDine(CurrHotel.Id, dineId, printTypes);
			}
		}

		/// <summary>
		/// 获取在线支付的跳转地址
		/// </summary>
		/// <param name="dineId">订单号</param>
		/// <returns>跳转地址</returns>
		private async Task<string> getOnlineRedirectUrl(string dineId) {
			HotelConfig hotelConfig = await HotelManager.GetHotelConfig();
			DinePaidDetail dinePaidDetail = await HotelManager.GetDineOnlinePaidDetail(dineId);
			if(dinePaidDetail == null)
				return null;

			StringBuilder redirectUrl = new StringBuilder();
			redirectUrl.Append($"{dinePaidDetail.PayKind.RedirectUrl}?");
			string priceCrypted = DesCryptography.DesEncrypt(dinePaidDetail.Price.ToString());
			redirectUrl.Append($"HotelId={hotelConfig.Id}&DineId={dineId}&Price={Server.UrlEncode(priceCrypted)}&");
			redirectUrl.Append($"NotifyUrl={Server.UrlEncode(dinePaidDetail.PayKind.NotifyUrl)}&");
			redirectUrl.Append($"CompleteUrl={Server.UrlEncode(dinePaidDetail.PayKind.CompleteUrl)}");
			return redirectUrl.ToString();
		}
		/// <summary>
		/// 在线支付完成
		/// </summary>
		/// <param name="dineId">订单号</param>
		/// <param name="recordId">附加信息</param>
		/// <returns></returns>
		private async Task onlinePayCompleted(string dineId, string recordId) {
			bool isPaid = await HotelManager.IsDinePaid(dineId);
			if(isPaid) {
				await HotelManager.RecordLog(HotelDAO.Models.Log.LogLevel.Warning, $"DineId: {dineId} Has Been Paid");
				return;
			}
			await OrderManager.OnlinePayCompleted(dineId, recordId);
			NewDineInformTcpClient.SendNewDineInfrom(CurrHotel.Id, dineId, true);

			HotelConfig config = await HotelManager.GetHotelConfig();

			List<PrintType> printTypes = new List<PrintType> { PrintType.Recipt, PrintType.ServeOrder, PrintType.KitchenOrder };
			
			for(int i = 0; i < config.PrintingReciptTimes - 1; i++) {
				printTypes.Add(PrintType.Recipt);
			}

			await requestPrintDine(dineId, printTypes);
		}
	}
}