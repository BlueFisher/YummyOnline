using HotelDAO;
using OrderSystem.Models;
using OrderSystem.Utility;
using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using YummyOnlineDAO.Identity;
using YummyOnlineDAO.Models;
using Protocol;
using System.Collections.Generic;

namespace OrderSystem.Controllers {
	public abstract class BaseAccountController : BaseOrderSystemController {
		#region 用户注册
		public virtual async Task<JsonResult> SendSMS(string phoneNumber) {
			if(await UserManager.IsPhoneNumberDuplicated(phoneNumber)) {
				return Json(new JsonError("此号码已注册"));
			}
			FunctionResult result = generateSmsCodeAndSend(phoneNumber);
			if(!result.Succeeded) {
				return Json(new JsonError(result.Message));
			}
			Session["SmsCode"] = result.Data;

			return Json(new JsonSuccess());
		}
		public virtual async Task<JsonResult> Signup(SignupViewModel model) {
			if((string)Session["SmsCode"] != model.Code) {
				return Json(new JsonError("验证码不正确", "code"));
			}
			if(model.PhoneNumber == null) {
				return Json(new JsonError("手机号不能为空"));
			}
			if(model.Password == null) {
				return Json(new JsonError("密码不能为空"));
			}
			if(model.Password != model.PasswordAga) {
				return Json(new JsonError("密码不一致"));
			}
			User user;
			bool succeeded;
			if(await SigninManager.IsAuthenticated() && await UserManager.IsInRoleAsync(User.Identity.GetUserId(), Role.Nemo)) {
				user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
				user.PhoneNumber = model.PhoneNumber;
				user.UserName = model.PhoneNumber;
				user.PasswordHash = UserManager.GetMd5(model.Password);
				succeeded = await UserManager.UpdateAsync(user);
				await UserManager.RemoveFromRoleAsync(user.Id, Role.Nemo);

				await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Warning, $"User Update: {User.Identity.GetUserId()}");
			}
			else {
				user = new User {
					PhoneNumber = model.PhoneNumber,
					UserName = model.PhoneNumber
				};
				succeeded = await UserManager.CreateAsync(user, model.Password);
			}

			if(!succeeded) {
				return Json(new JsonError("注册失败"));
			}
			await UserManager.AddToRoleAsync(user.Id, Role.Customer);
			SigninManager.Signin(user, true);
			await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Success, $"User Signup: {user.Id} ({user.PhoneNumber})");
			return Json(new JsonSuccess());
		}
		#endregion

		#region 用户登录
		public virtual async Task<JsonResult> Signin(SigninViewModel model) {
			User user = await UserManager.FindByPhoneNumberAsync(model.PhoneNumber);
			if(user == null) {
				await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Warning, $"User Signin: {model.PhoneNumber} No PhoneNumber, Host: {Request.UserHostAddress}");
				return Json(new JsonError("手机未注册"));
			}
			if(!await UserManager.CheckPasswordAsync(user, model.Password)) {
				await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Warning, $"User Signin: {model.PhoneNumber} Password Error, Host: {Request.UserHostAddress}",
					$"Password: {model.Password}");
				return Json(new JsonError("密码不正确"));
			}
			if(User.Identity.IsAuthenticated) {
				User oldUser = await UserManager.FindByIdAsync(User.Identity.GetUserId());
				if(oldUser != null && await UserManager.IsInRoleAsync(oldUser.Id, Role.Nemo)) {
					// 原来为匿名用户, 每个饭店该匿名用户点过的订单转移到登录的用户帐号下
					List<Hotel> hotels = await YummyOnlineManager.GetHotels();
					foreach(Hotel h in hotels) {
						HotelManager hotelManager = new HotelManager(h.ConnectionString);
						await hotelManager.TransferDines(oldUser.Id, user.Id);
					}
					await UserManager.TransferUserPrice(user, oldUser);
					await UserManager.DeleteAsync(oldUser);

					await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Warning, $"User Transfer: {oldUser.Id} -> {user.Id}");
				}
			}
			SigninManager.Signin(user, true);
			await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Success, $"User Signin: {user.Id} ({user.PhoneNumber}), Host: {Request.UserHostAddress}");
			return Json(new JsonSuccess());
		}
		#endregion

		#region 登出
		public virtual JsonResult Signout() {
			SigninManager.Signout();
			return Json(new JsonSuccess());
		}
		#endregion

		#region 忘记密码
		public virtual async Task<ActionResult> SendForgetSMS(string phoneNumber) {
			if(!await UserManager.IsPhoneNumberDuplicated(phoneNumber)) {
				return Json(new JsonError("此号码未注册"));
			}
			FunctionResult result = generateSmsCodeAndSend(phoneNumber);
			if(!result.Succeeded) {
				return Json(new JsonError(result.Message));
			}
			Session["SMSForgetCode"] = result.Data;

			return Json(new JsonSuccess());
		}
		public virtual async Task<JsonResult> Forget(ForgetViewModel model) {
			if(Session["SMSForgetCode"] == null || Session["SMSForgetCode"].ToString() != model.Code) {
				return Json(new JsonError("验证码不正确", "code"));
			}
			await UserManager.ChangePasswordAsync(model.PhoneNumber, model.Password);

			await YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Warning, $"{model.PhoneNumber} {model.Password} Change Password");

			return Json(new JsonSuccess());
		}
		#endregion

		/// <summary>
		/// 生成短信验证码并且发送
		/// </summary>
		/// <param name="phoneNumber">手机号</param>
		/// <returns>短信验证码</returns>
		private FunctionResult generateSmsCodeAndSend(string phoneNumber) {
			DateTime? LastSmsDateTime = Session["LastSmsDateTime"] as DateTime?;
			if(LastSmsDateTime.HasValue && (DateTime.Now - LastSmsDateTime.Value).TotalSeconds < 50) {
				return new FunctionResult(false, "您还不能发送短信验证码");
			}

			Random rand = new Random(unchecked((int)DateTime.Now.Ticks));
			string code = "";
			for(int i = 0; i < 6; i++) {
				code += rand.Next(10);
			}
			Session["LastSmsDateTime"] = DateTime.Now;

#if DEBUG
			var _= YummyOnlineManager.RecordLog(Log.LogProgram.Identity, Log.LogLevel.Debug, phoneNumber + " ： " + code);
#else
			if(!Utility.SMSSender.Send(phoneNumber, code)) {
				return new FunctionResult(false, "发送失败");
			}
#endif
			return new FunctionResult {
				Succeeded = true,
				Data = code
			};
		}

		public async Task<JsonResult> IsAuthenticated() {
			if(!await SigninManager.IsAuthenticated()) {
				return Json(new JsonError());
			}
			return Json(new JsonSuccess(await getCustomerInfo()));
		}

		class CustomerInfo {
			public dynamic Hotel { get; set; }
			public int Points { get; set; }
			public dynamic VipLevel { get; set; }
			public int DinesCount { get; set; }
		}
		protected virtual async Task<dynamic> getCustomerInfo() {
			string userId = User.Identity.GetUserId();
			User user = await UserManager.FindByIdAsync(userId);
			if(user == null) {
				return null;
			}

			List<CustomerInfo> customerInfos = new List<CustomerInfo>();
			List<Hotel> hotels = await YummyOnlineManager.GetHotels();
			foreach(Hotel h in hotels) {
				HotelManager hotelManager = new HotelManager(h.ConnectionString);
				HotelDAO.Models.Customer customer = await hotelManager.GetCustomer(userId);
				if(customer == null)
					continue;

				customerInfos.Add(new CustomerInfo {
					Hotel = new {
						h.Id,
						h.Name
					},
					Points = customer.Points,
					VipLevel = customer.VipLevel == null ? null : new {
						customer.VipLevel.Id,
						customer.VipLevel.Name
					},
					DinesCount = await hotelManager.GetHistoryDinesCount(userId)
				});
			}

			return new {
				user.Id,
				user.Email,
				user.PhoneNumber,
				user.UserName,
				CustomerInfos = customerInfos
			};
		}
	}

	[RequireHotel]
	[HotelAvailable]
	public class AccountController : BaseAccountController {
		// GET: Account
		public ActionResult Index() {
			return View();
		}
		public ActionResult _ViewUser() {
			return View();
		}
		public ActionResult _ViewSignup() {
			return View();
		}
		public async Task<ActionResult> _ViewSignin() {
			ViewBag.NeedCodeImg = (await HotelManager.GetHotelConfig()).NeedCodeImg;
			return View();
		}
		public ActionResult _ViewForget() {
			return View();
		}

		public FileContentResult CodeImage() {
			string code = CodeImg.CreateRandomCode();
			Session["CodeImg"] = code.ToLower();
			return File(CodeImg.CreateCheckCodeImage(code), "image/jpeg");
		}
		public override async Task<JsonResult> Signin(SigninViewModel model) {
			bool needCodeImg = (await HotelManager.GetHotelConfig()).NeedCodeImg;
			if(needCodeImg && (Session["CodeImg"] == null || model.CodeImg.ToLower() != Session["CodeImg"].ToString())) {
				return Json(new JsonError("验证码不正确"));
			}
			return await base.Signin(model);
		}

		protected override async Task<dynamic> getCustomerInfo() {
			string userId = User.Identity.GetUserId();
			User user = await UserManager.FindByIdAsync(userId);
			if(user == null) {
				return null;
			}
			HotelDAO.Models.Customer customer = await HotelManager.GetOrCreateCustomer(userId);

			return new {
				user.Id,
				user.Email,
				user.PhoneNumber,
				user.UserName,
				customer.Points,
				customer.VipLevelId,
				DinesCount = await HotelManager.GetHistoryDinesCount(userId)
			};
		}
	}
}