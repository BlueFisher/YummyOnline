using Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Utility;
using YummyOnline.Utility;
using YummyOnlineDAO.Models;
using YummyOnline.Models;

namespace YummyOnline.Controllers {
	[Authorize(Roles = nameof(Role.Admin))]
	public class HotelController : BaseController {
		// GET: Hotel
		public ActionResult Index() {
			return View();
		}
		public ActionResult _ViewHotel() {
			return View();
		}
		public ActionResult _ViewDine() {
			return View();
		}
		public ActionResult _ViewArticle() {
			return View();
		}

		public async Task<JsonResult> GetHotelNames() {
			List<Hotel> hotels = await YummyOnlineManager.GetHotels();
			return Json(hotels.Select(p => new {
				p.Id,
				p.Name,
			}));
		}
		public async Task<JsonResult> GetHotels() {
			List<Hotel> hotels = await YummyOnlineManager.GetHotels();
			hotels.AddRange(await YummyOnlineManager.GetHotelReadyForConfirms());

			if(User.IsInRole(nameof(Role.SuperAdmin))) {
				return Json(hotels.Select(p => new {
					p.Id,
					p.Name,
					p.ConnectionString,
					p.AdminConnectionString,
					p.CssThemePath,
					p.OrderSystemStyle,
					p.CreateDate,
					p.Tel,
					p.Address,
					p.OpenTime,
					p.CloseTime,
					p.Usable
				}));
			}
			return Json(hotels.Select(p => new {
				p.Id,
				p.Name,
				p.ConnectionString,
				p.CssThemePath,
				p.OrderSystemStyle,
				p.CreateDate,
				p.Tel,
				p.Address,
				p.OpenTime,
				p.CloseTime,
				p.Usable
			}));
		}
		[Authorize(Roles = nameof(Role.SuperAdmin))]
		public async Task<JsonResult> UpdateHotel(Hotel hotel) {
			await YummyOnlineManager.UpdateHotel(hotel);
			await YummyOnlineManager.RecordLog(Log.LogProgram.System, Log.LogLevel.Warning, $"Hotel {hotel.Id} Updated");
			return Json(new JsonSuccess());
		}
		[Authorize(Roles = nameof(Role.SuperAdmin))]
		public async Task<JsonResult> CreateHotel(int hotelId, string databaseName) {
			// 创建空数据库
			//OriginSql originSql = new OriginSql(new YummyOnlineContext().Database.Connection.ConnectionString);
			//var result = await originSql.CreateDatabase(databaseName);
			//if(!result.Succeeded) {
			//	return Json(new JsonError(result.Message));
			//}
			// 总数据库记录连接字符串等信息
			await YummyOnlineManager.CreateHotel(hotelId, databaseName);

			// 新数据库写入所有表格等架构
			Hotel newHotel = await YummyOnlineManager.GetHotelById(hotelId);
			//originSql = new OriginSql(newHotel.AdminConnectionString);
			//result = await originSql.InitializeHotel();
			//if(!result.Succeeded) {
			//	return Json(new JsonError(result.Message));
			//}

			// 新数据库初始化
			string staffId = await YummyOnlineManager.GetHotelAdminId(hotelId);
			HotelManager hotelManager = new HotelManager(newHotel.AdminConnectionString);
			await hotelManager.InitializeHotel(hotelId, staffId);

			await YummyOnlineManager.RecordLog(Log.LogProgram.System, Log.LogLevel.Success, $"Hotel {hotelId} Created");

			return Json(new JsonSuccess());
		}

		public async Task<JsonResult> GetAllDineIds(int hotelId, DateTime? dateTime) {
			dateTime = dateTime ?? DateTime.Now;

			string connStr = await YummyOnlineManager.GetHotelConnectionStringById(hotelId);

			return Json(await new HotelManager(connStr).GetAllDineIds(dateTime.Value));
		}
		public async Task<JsonResult> GetDines(int hotelId, List<string> dineIds) {
			string connStr = await YummyOnlineManager.GetHotelConnectionStringById(hotelId);

			List<dynamic> dines = new List<dynamic>();
			foreach(string dineId in dineIds) {
				var dine = await new HotelManager(connStr).GetFormatedDineById(dineId);
				if(dine != null)
					dines.Add(dine);
			}

			return Json(dines);
		}

		[Authorize(Roles = nameof(Role.SuperAdmin))]
		public async Task<JsonResult> WeixinNotify(int hotelId, string dineId) {
			NetworkNotifyViewModels model = new NetworkNotifyViewModels {
				HotelId = hotelId,
				DineId = dineId,
				RecordId = $"SystemTestNotify{DateTime.Now.ToString("yyyyMMddHHmmssffff")}"
			};
			string notifyInfo = Newtonsoft.Json.JsonConvert.SerializeObject(model);
			string encryptedInfo = DesCryptography.DesEncrypt(notifyInfo);

			string url = (await YummyOnlineManager.GetSystemConfig()).OrderSystemUrl;
			string result = await HttpPost.PostAsync($"{url}/Payment/OnlineNotify", new {
				EncryptedInfo = encryptedInfo
			});

			if(result == null) {
				return Json(new JsonError());
			}

			await YummyOnlineManager.RecordLog(Log.LogProgram.System, Log.LogLevel.Success, $"Weixin Hotel {hotelId}, DineId {dineId} Notified");

			return Json(new JsonSuccess());
		}

		public async Task<JsonResult> GetArticles(int? hotelId) {
			return Json(await YummyOnlineManager.GetArticles(hotelId));
		}
		public async Task<JsonResult> AddArticle(ArticleViewModel model) {
			await YummyOnlineManager.AddArticle(model, User.Identity.Name);
			await YummyOnlineManager.RecordLog(Log.LogProgram.System, Log.LogLevel.Success, $"Article Hotel {model.HotelId}, Title {model.Title} Created");
			return Json(new JsonSuccess());
		}
		public async Task<JsonResult> RemoveArticle(int id) {
			await YummyOnlineManager.RemoveArticle(id);
			await YummyOnlineManager.RecordLog(Log.LogProgram.System, Log.LogLevel.Success, $"Article {id} Removed");
			return Json(new JsonSuccess());
		}
	}
}