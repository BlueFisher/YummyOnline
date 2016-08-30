using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using Utility;

namespace OrderSystem.Controllers {
	using HotelDAO.Models;
	using YummyOnlineDAO.Identity;

	[RequireHotel]
	[HotelAvailable]
	public class OrderController : BaseOrderSystemController {
		// GET: Order
		public ActionResult Index() {
			return View();
		}

		public async Task<ActionResult> _ViewMenu() {
			ViewBag.OrderSystemStyle = (await YummyOnlineManager.GetHotelById(CurrHotel.Id)).OrderSystemStyle;
			return View();
		}
		public ActionResult _ViewCart() {
			return View();
		}
		public ActionResult _ViewPayment() {
			return View();
		}
		public ActionResult _ViewHistory() {
			return View();
		}

		public JsonResult GetCurrentDesk() {
			return Json(Session["CurrentDesk"]);
		}

		public async Task<JsonResult> GetMenuInfos() {
			var t1 = new HotelManager(CurrHotel.ConnectionString).GetFormatedMenuClasses();
			var t2 = new HotelManager(CurrHotel.ConnectionString).GetFormatedMenus();
			var t3 = new HotelManager(CurrHotel.ConnectionString).GetFormatedMenuOnSales();
			var t4 = new HotelManager(CurrHotel.ConnectionString).GetFormatedMenuSetMeals();
			var t5 = new HotelManager(CurrHotel.ConnectionString).GetFormatedPayKinds(new List<PayKindType> { PayKindType.Online, PayKindType.Other });
			var t6 = new HotelManager(CurrHotel.ConnectionString).GetHotelConfig();
			var t7 = new HotelManager(CurrHotel.ConnectionString).GetTimeDiscounts();
			var t8 = new HotelManager(CurrHotel.ConnectionString).GetVipDiscounts();
			var currHotel = await new YummyOnlineManager().GetHotelById(CurrHotel.Id);

			var result = new {
				MenuClasses = await t1,
				Menus = await t2,
				MenuOnSales = await t3,
				MenuSetMeals = await t4,
				PayKinds = await t5,
				DiscountMethods = new {
					TimeDiscounts = await t7,
					VipDiscounts = await t8
				},
				Hotel = DynamicsCombination.CombineDynamics(await t6, new {
					currHotel.Name,
					currHotel.Address,
					currHotel.Tel,
					currHotel.OpenTime,
					currHotel.CloseTime
				})
			};
			return Json(result);
		}

		public async Task<JsonResult> GetHistoryDines() {
			return Json(await HotelManager.GetFormatedHistoryDines(User.Identity.GetUserId()));
		}

		public async Task<JsonResult> GetUserAddresses() {
			return Json(await HotelManager.GetUserAddresses(User.Identity.GetUserId()));
		}
	}
}

namespace OrderSystem.Controllers {
	using Protocol.PrintingProtocol;

	public class OrderForPrintingController : BaseOrderSystemController {
		public async Task<JsonResult> GetDineForPrinting(int hotelId, string dineId, List<int> dineMenuIds) {
			string connStr = await YummyOnlineManager.GetHotelConnectionStringById(hotelId);

			var tHotel = new YummyOnlineManager().GetHotelForPrintingById(hotelId);

			Task<Dine> tDine = null;
			if(dineId == "00000000000000") {
				tDine = generateTestDine();
			}
			else {
				tDine = new HotelManager(connStr).GetDineForPrintingById(dineId, dineMenuIds);
			}

			var tPrinterFormat = new HotelManager(connStr).GetPrinterFormatForPrinting();
			var tUser = new YummyOnlineManager().GetUserForPrintingById((await tDine).UserId);

			return Json(new DineForPrinting {
				Hotel = await tHotel,
				Dine = await tDine,
				User = await tUser,
				PrinterFormat = await tPrinterFormat
			});
		}

		private async Task<Dine> generateTestDine() {
			return await Task.Run(() => {
				var Dine = new Dine {
					Id = "00000000000000",
					Status = HotelDAO.Models.DineStatus.Untreated,
					Type = HotelDAO.Models.DineType.ToStay,
					HeadCount = 10,
					Price = 123.56m,
					OriPrice = 987.65m,
					Change = 123.456m,
					Discount = 0.5,
					DiscountName = "测试折扣名",
					DiscountType = HotelDAO.Models.DiscountType.PayKind,
					BeginTime = DateTime.Now,
					IsPaid = true,
					IsOnline = true,
					UserId = "0000000000",
					Waiter = new Staff {
						Id = "00000000",
						Name = "测试服务员名"
					},
					Clerk = new Staff {
						Id = "00000000",
						Name = "测试收银员名"
					},
					Remarks = new List<Remark> {
							new Remark {Id = 1,Name = "测试备注1",Price = 12.34m },
							new Remark {Id = 2,Name = "测试备注2",Price = 56.78m },
							new Remark {Id = 3,Name = "测试备注3",Price = 90.12m },
							new Remark {Id = 4,Name = "测试备注4",Price = 34.56m }
						},
					Desk = new Desk {
						Id = "000",
						QrCode = "111",
						Name = "测试桌名",
						Description = "测试桌子备注信息",
						AreaType = HotelDAO.Models.AreaType.Normal,
						ReciptPrinter = new Printer {
							Id = 0,
							Name = "Microsoft XPS Document Writer",
							IpAddress = "127.0.0.1",
							Usable = true
						},
						ServePrinter = new Printer {
							Id = 1,
							Name = "Microsoft XPS Document Writer",
							IpAddress = "127.0.0.2",
							Usable = true
						}
					},
					DineMenus = new List<DineMenu> {
							new DineMenu {
								Status = HotelDAO.Models.DineMenuStatus.Normal,
								Count = 10,
								OriPrice = 56.78m,
								Price = 12.34m,
								RemarkPrice = 12.34m,
								Remarks = new List<Remark> {
											new Remark {Id = 0,Name = "测试备注1",Price = 12.34m },
											new Remark {Id = 1,Name = "测试备注2",Price = 56.78m },
											new Remark {Id = 2,Name = "测试备注3",Price = 90.12m },
											new Remark {Id = 3,Name = "测试备注4",Price = 34.56m }
										},
								Menu = new Menu {
									Id = $"00000",
									Code = "test",
									Name = $"测试套餐",
									NameAbbr = $"测试0",
									Unit = "份",
									IsSetMeal = true,
									SetMealMenus = new List<SetMealMenu> {
										new SetMealMenu {
											Id = "10000",
											Name = "测试套餐菜品1",
											Count = 10
										},
										new SetMealMenu {
											Id = "10001",
											Name = "测试套餐菜品2",
											Count = 10
										},
										new SetMealMenu {
											Id = "10002",
											Name = "测试套餐菜品3",
											Count = 10
										}
									},
									Printer = new Printer {
										Id = 2,
										Name = "Microsoft XPS Document Writer",
										IpAddress = "127.0.0.1",
										Usable = true
									},
								}
							}
						},
					DinePaidDetails = new List<DinePaidDetail> {
							new DinePaidDetail {
								Price = 12.34m,
								RecordId = "1234567890abcdeABCDE",
								PayKind = new PayKind {
									Id = 0,
									Name = "测试支付1",
									Type = HotelDAO.Models.PayKindType.Online
								}
							},
							new DinePaidDetail {
								Price = 12.34m,
								RecordId = "1234567890abcdeABCDE",
								PayKind = new PayKind {
									Id = 0,
									Name = "测试支付2",
									Type = HotelDAO.Models.PayKindType.Offline
								}
							},
							new DinePaidDetail {
								Price = 12.34m,
								RecordId = "1234567890abcdeABCDE",
								PayKind = new PayKind {
									Id = 0,
									Name = "测试支付3",
									Type = HotelDAO.Models.PayKindType.Points
								}
							}
						}
				};

				for(int i = 1; i <= 5; i++) {
					Dine.DineMenus.Add(new DineMenu {
						Status = HotelDAO.Models.DineMenuStatus.Normal,
						Count = 10,
						OriPrice = 56.78m,
						Price = 12.34m,
						RemarkPrice = 12.34m,
						Remarks = new List<Remark> {
									new Remark {Id = 0,Name = "测试备注1",Price = 12.34m },
									new Remark {Id = 1,Name = "测试备注2",Price = 56.78m },
									new Remark {Id = 2,Name = "测试备注3",Price = 90.12m },
									new Remark {Id = 3,Name = "测试备注4",Price = 34.56m }
								},
						Menu = new Menu {
							Id = $"0000{i}",
							Code = "test",
							Name = $"测试菜品名{i}",
							NameAbbr = $"测试{i}",
							Unit = "份",
							IsSetMeal = false,
							Printer = new Printer {
								Id = 2,
								Name = "Microsoft XPS Document Writer",
								IpAddress = "127.0.0.1",
								Usable = true
							},
							DepartmentName = $"测试部门名{i}"
						}
					});
				}
				return Dine;
			});
		}

		public async Task<JsonResult> GetShiftsForPrinting(int hotelId, List<int> ids, DateTime dateTime) {
			string connStr = await YummyOnlineManager.GetHotelConnectionStringById(hotelId);
			var manager = new HotelManager(connStr);

			Task<List<Shift>> tShifts = null;
			Task<List<PayKindShift>> tPayKindShifts = null;
			Task<List<MenuClassShift>> tMenuClassShifts = null;

			if(ids == null || ids.Count == 0) {
				tShifts = generateTestShift();
				tPayKindShifts = generateTestPayKindShift();
				tMenuClassShifts = generateTestMenuClassShift();
			}
			else {
				tShifts = new HotelManager(connStr).GetShiftsForPrinting(ids, dateTime);
				tPayKindShifts = new HotelManager(connStr).GetPayKindShiftsForPrinting(ids, dateTime);
				tMenuClassShifts = new HotelManager(connStr).GetMenuClassShiftsForPrinting(ids, dateTime);
			}

			var tPrinter = new HotelManager(connStr).GetShiftPrinter();
			var tPrinterFormat = new HotelManager(connStr).GetPrinterFormatForPrinting();


			return Json(new ShiftForPrinting {
				Shifts = await tShifts,
				PayKindShifts = await tPayKindShifts,
				MenuClassShifts = await tMenuClassShifts,
				Printer = await tPrinter,
				PrinterFormat = await tPrinterFormat
			});
		}

		private Task<List<Shift>> generateTestShift() {
			return Task.Run(() => {
				List<Shift> shift = new List<Shift>();

				shift.Add(new Shift {
					Id = -1,
					DateTime = DateTime.Now,
					AveragePrice = 12.34m,
					CustomerCount = 8,
					DeskCount = 9,
					GiftPrice = 12.34m,
					OriPrice = 12.34m,
					PreferencePrice = 12.34m,
					Price = 12.34m,
					ReturnedPrice = 12.34m,
					ToGoPrice = 12.34m,
					ToStayPrice = 12.34m,
				});
				shift.Add(new Shift {
					Id = 0,
					DateTime = DateTime.Now,
					AveragePrice = 12.34m,
					CustomerCount = 8,
					DeskCount = 9,
					GiftPrice = 12.34m,
					OriPrice = 12.34m,
					PreferencePrice = 12.34m,
					Price = 12.34m,
					ReturnedPrice = 12.34m,
					ToGoPrice = 12.34m,
					ToStayPrice = 12.34m,
				});

				return shift;
			});
		}
		private Task<List<PayKindShift>> generateTestPayKindShift() {
			return Task.Run(() => {
				List<PayKindShift> shift = new List<PayKindShift>();

				shift.Add(new PayKindShift {
					Id = -1,
					DateTime = DateTime.Now,
					PayKindShiftDetails = new List<PayKindShiftDetail> {
						new PayKindShiftDetail {
							PayKind = "测试支付1",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
						new PayKindShiftDetail {
							PayKind = "测试支付2",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
						new PayKindShiftDetail {
							PayKind = "测试支付3",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
						new PayKindShiftDetail {
							PayKind = "测试支付4",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
					}
				});
				shift.Add(new PayKindShift {
					Id = 0,
					DateTime = DateTime.Now,
					PayKindShiftDetails = new List<PayKindShiftDetail> {
						new PayKindShiftDetail {
							PayKind = "测试支付1",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
						new PayKindShiftDetail {
							PayKind = "测试支付2",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
						new PayKindShiftDetail {
							PayKind = "测试支付3",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
						new PayKindShiftDetail {
							PayKind = "测试支付4",
							RealPrice = 12.34m,
							ReceivablePrice= 56.78m
						},
					}
				});

				return shift;
			});
		}
		private Task<List<MenuClassShift>> generateTestMenuClassShift() {
			return Task.Run(() => {
				List<MenuClassShift> shift = new List<MenuClassShift>();

				shift.Add(new MenuClassShift {
					Id = -1,
					DateTime = DateTime.Now,
					MenuClassShiftDetails = new List<MenuClassShiftDetail> {
						new MenuClassShiftDetail {
							MenuClass = "测试分类1",
							Price=12.34m
						},
						new MenuClassShiftDetail {
							MenuClass = "测试分类2",
							Price=12.34m
						},
						new MenuClassShiftDetail {
							MenuClass = "测试分类3",
							Price=12.34m
						},
						new MenuClassShiftDetail {
							MenuClass = "测试分类4",
							Price=12.34m
						},
					}
				});
				shift.Add(new MenuClassShift {
					Id = 0,
					DateTime = DateTime.Now,
					MenuClassShiftDetails = new List<MenuClassShiftDetail> {
						new MenuClassShiftDetail {
							MenuClass = "测试分类1",
							Price=12.34m
						},
						new MenuClassShiftDetail {
							MenuClass = "测试分类2",
							Price=12.34m
						},
						new MenuClassShiftDetail {
							MenuClass = "测试分类3",
							Price=12.34m
						},
						new MenuClassShiftDetail {
							MenuClass = "测试分类4",
							Price=12.34m
						},
					}
				});

				return shift;
			});
		}

		public async Task<JsonResult> GetPrintersForPrinting(int hotelId) {
			string connStr = await YummyOnlineManager.GetHotelConnectionStringById(hotelId);
			var manager = new HotelManager(connStr);

			return Json(new PrintersForPrinting {
				Printers = await manager.GetPrinters()
			});
		}
	}
}