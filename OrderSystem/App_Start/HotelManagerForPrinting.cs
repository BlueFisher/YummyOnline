using HotelDAO.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Threading.Tasks;

namespace OrderSystem {
	// 打印协议相关
	public partial class HotelManager {
		public async Task<Protocol.PrintingProtocol.PrinterFormat> GetPrinterFormatForPrinting() {
			return await ctx.PrinterFormats.Select(p => new Protocol.PrintingProtocol.PrinterFormat {
				PaperSize = p.PaperSize,
				Font = p.Font,
				ColorDepth = p.ColorDepth,
				ReciptBigFontSize = p.ReciptBigFontSize,
				ReciptFontSize = p.ReciptFontSize,
				ReciptSmallFontSize = p.ReciptSmallFontSize,
				ServeOrderFontSize = p.ServeOrderFontSize,
				ServeOrderSmallFontSize = p.ServeOrderSmallFontSize,
				KitchenOrderFontSize = p.KitchenOrderFontSize,
				KitchenOrderSmallFontSize = p.KitchenOrderSmallFontSize,
				ShiftBigFontSize = p.ShiftBigFontSize,
				ShiftFontSize = p.ShiftFontSize,
				ShiftSmallFontSize = p.ShiftSmallFontSize,
				PaddingRight = p.PaddingRight,
			}).FirstOrDefaultAsync();
		}
		public async Task<Protocol.PrintingProtocol.Dine> GetDineForPrintingById(string dineId, List<int> dineMenuIds = null) {
			var dine = await formatDineForPrinting(ctx.Dines
					.Where(p => p.Id == dineId))
					.FirstOrDefaultAsync();

			if(dineMenuIds != null && dineMenuIds.Count > 0) {
				dine.DineMenus.RemoveAll(p => !dineMenuIds.Contains(p.Id));
			}

			foreach(var dineMenu in dine.DineMenus) {
				if(dineMenu.Menu.IsSetMeal) {
					dineMenu.Menu.SetMealMenus = await ctx.MenuSetMeals
					.Where(p => p.MenuSetId == dineMenu.Menu.Id)
					.Select(p => new Protocol.PrintingProtocol.SetMealMenu {
						Id = p.Menu.Id,
						Name = p.Menu.Name,
						Count = p.Count
					}).ToListAsync();
				}
			}
			return dine;
		}

		private IQueryable<Protocol.PrintingProtocol.Dine> formatDineForPrinting(IQueryable<Dine> dine) {
			return dine.Select(p => new Protocol.PrintingProtocol.Dine {
				Id = p.Id,
				Status = p.Status,
				Type = p.Type,
				HeadCount = p.HeadCount,
				Price = p.Price,
				OriPrice = p.OriPrice,
				Change = p.Change,
				Discount = p.Discount,
				DiscountName = p.DiscountName,
				DiscountType = p.DiscountType,
				BeginTime = p.BeginTime,
				IsPaid = p.IsPaid,
				IsOnline = p.IsOnline,
				UserId = p.UserId,
				Waiter = new Protocol.PrintingProtocol.Staff {
					Id = p.Waiter.Id,
					Name = p.Waiter.Name
				},
				Clerk = new Protocol.PrintingProtocol.Staff {
					Id = p.Clerk.Id,
					Name = p.Clerk.Name
				},
				Remarks = p.Remarks.Select(pp => new Protocol.PrintingProtocol.Remark {
					Id = pp.Id,
					Name = pp.Name,
					Price = pp.Price
				}).ToList(),
				Desk = new Protocol.PrintingProtocol.Desk {
					Id = p.Desk.Id,
					QrCode = p.Desk.QrCode,
					Name = p.Desk.Name,
					Description = p.Desk.Description,
					AreaType = p.Desk.Area.Type,
					ReciptPrinter = new Protocol.PrintingProtocol.Printer {
						Id = p.Desk.Area.DepartmentRecipt.Printer.Id,
						Name = p.Desk.Area.DepartmentRecipt.Printer.Name,
						IpAddress = p.Desk.Area.DepartmentRecipt.Printer.IpAddress,
						Usable = p.Desk.Area.DepartmentRecipt.Printer.Usable
					},
					ServePrinter = new Protocol.PrintingProtocol.Printer {
						Id = p.Desk.Area.DepartmentServe.Printer.Id,
						Name = p.Desk.Area.DepartmentServe.Printer.Name,
						IpAddress = p.Desk.Area.DepartmentServe.Printer.IpAddress,
						Usable = p.Desk.Area.DepartmentServe.Printer.Usable
					},
					ReciptDepartmentName = p.Desk.Area.DepartmentRecipt.Name,
					ServeDepartmentName = p.Desk.Area.DepartmentServe.Name
				},
				DineMenus = p.DineMenus.Select(d => new Protocol.PrintingProtocol.DineMenu {
					Id = d.Id,
					Status = d.Status,
					Count = d.Count,
					OriPrice = d.OriPrice,
					Price = d.Price,
					RemarkPrice = d.RemarkPrice,
					Remarks = d.Remarks.Select(r => new Protocol.PrintingProtocol.Remark {
						Id = r.Id,
						Name = r.Name,
						Price = r.Price
					}).ToList(),
					Menu = new Protocol.PrintingProtocol.Menu {
						Id = d.Menu.Id,
						Code = d.Menu.Code,
						Name = d.Menu.Name,
						NameAbbr = d.Menu.NameAbbr,
						Unit = d.Menu.Unit,
						IsSetMeal = d.Menu.IsSetMeal,
						Printer = new Protocol.PrintingProtocol.Printer {
							Id = d.Menu.Department.Printer.Id,
							Name = d.Menu.Department.Printer.Name,
							IpAddress = d.Menu.Department.Printer.IpAddress,
							Usable = d.Menu.Department.Printer.Usable
						},
						DepartmentName = d.Menu.Department.Name
					},
					ReturnedWaiter = new Protocol.PrintingProtocol.Staff {
						Id = d.ReturnedWaiter.Id,
						Name = d.ReturnedWaiter.Name
					},
					ReturnedReason = d.ReturnedReason
				}).ToList(),
				DinePaidDetails = p.DinePaidDetails.Select(d => new Protocol.PrintingProtocol.DinePaidDetail {
					Price = d.Price,
					RecordId = d.RecordId,
					PayKind = new Protocol.PrintingProtocol.PayKind {
						Id = d.PayKind.Id,
						Name = d.PayKind.Name,
						Type = d.PayKind.Type
					}
				}).ToList(),
				TakeOut = new Protocol.PrintingProtocol.TakeOut {
					Address = p.TakeOut.Address,
					Name = p.TakeOut.Name,
					PhoneNumber = p.TakeOut.PhoneNumber,
					RecordId = p.TakeOut.RecordId
				}
			});
		}


		public async Task<Protocol.PrintingProtocol.Printer> GetShiftPrinter() {
			return await ctx.HotelConfigs.Select(p => p.ShiftPrinter == null ? null : new Protocol.PrintingProtocol.Printer {
				Id = p.ShiftPrinter.Id,
				Name = p.ShiftPrinter.Name,
				IpAddress = p.ShiftPrinter.IpAddress,
				Usable = p.ShiftPrinter.Usable
			}).FirstOrDefaultAsync();
		}

		public async Task<List<Protocol.PrintingProtocol.PayKindShift>> GetPayKindShiftsForPrinting(List<int> ids, DateTime dateTime) {
			List<Protocol.PrintingProtocol.PayKindShift> payKindShifts = await ctx.PayKindShifts
				.Where(p => ids.Contains(p.Id) && SqlFunctions.DateDiff("day", p.DateTime, dateTime) == 0)
				.GroupBy(p => p.Id).Select(p => new Protocol.PrintingProtocol.PayKindShift {
					Id = p.Key,
					DateTime = p.Select(a => a.DateTime).FirstOrDefault(),
					PayKindShiftDetails = p.Select(d => new Protocol.PrintingProtocol.PayKindShiftDetail {
						PayKind = d.PayKind.Name,
						RealPrice = d.RealPrice,
						ReceivablePrice = d.ReceivablePrice
					}).ToList()
				}).ToListAsync();

			return payKindShifts;
		}

		public async Task<List<Protocol.PrintingProtocol.Shift>> GetShiftsForPrinting(List<int> ids, DateTime dateTime) {
			List<Protocol.PrintingProtocol.Shift> shifts = await ctx.Shifts
				.Where(p => ids.Contains(p.Id) && SqlFunctions.DateDiff("day", p.DateTime, dateTime) == 0)
				.Select(p => new Protocol.PrintingProtocol.Shift {
					Id = p.Id,
					DateTime = p.DateTime,
					AveragePrice = p.AveragePrice,
					CustomerCount = p.CustomerCount,
					DeskCount = p.DeskCount,
					GiftPrice = p.GiftPrice,
					OriPrice = p.OriPrice,
					PreferencePrice = p.PreferencePrice,
					Price = p.Price,
					ReturnedPrice = p.ReturnedPrice,
					ToGoPrice = p.ToGoPrice,
					ToStayPrice = p.ToStayPrice
				}).ToListAsync();

			return shifts;
		}

		public async Task<List<Protocol.PrintingProtocol.MenuClassShift>> GetMenuClassShiftsForPrinting(List<int> ids, DateTime dateTime) {
			List<Protocol.PrintingProtocol.MenuClassShift> menuClassShifts = await ctx.MenuClassShifts
				.Where(p => ids.Contains(p.Id) && SqlFunctions.DateDiff("day", p.DateTime, dateTime) == 0)
				.GroupBy(p => p.Id).Select(p => new Protocol.PrintingProtocol.MenuClassShift {
					Id = p.Key,
					DateTime = p.Select(a => a.DateTime).FirstOrDefault(),
					MenuClassShiftDetails = p.Select(d => new Protocol.PrintingProtocol.MenuClassShiftDetail {
						MenuClass = d.MenuClass.Name,
						Price = d.Price
					}).ToList()
				}).ToListAsync();

			return menuClassShifts;
		}

		public async Task<List<Protocol.PrintingProtocol.Printer>> GetPrinters() {
			return await ctx.Printers.Where(p => p.Usable).Select(p => new Protocol.PrintingProtocol.Printer {
				Id = p.Id,
				Name = p.Name,
				IpAddress = p.IpAddress,
				Usable = p.Usable
			}).ToListAsync();
		}
	}
}