using HotelDAO.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Threading.Tasks;

namespace OrderSystem.Waiter {
	public partial class HotelManager : HotelDAO.BaseHotelManager {
		public HotelManager(string connString) : base(connString) { }

		public async Task<dynamic> GetAreas() {
			return await ctx.Areas.Where(p => p.Usable == true).Select(p => new {
				p.Id,
				p.Name,
				p.Description,
				p.DepartmentReciptId,
				p.DepartmentServeId,
			}).ToListAsync();
		}

		public async Task<dynamic> GetDesks() {
			return await ctx.Desks.Where(p => p.Usable)
				.Select(p => new {
					Area = new {
						p.Area.Id,
						p.Area.Name,
						p.Area.Type
					},
					p.Id,
					p.QrCode,
					p.Name,
					p.Description,
					p.Status,
					p.Order,
					p.HeadCount,
					p.MinPrice,
				}).ToListAsync();
		}

		public async Task<dynamic> GetRemarks() {
			return await ctx.Remarks
				.Select(p => new {
					p.Id,
					p.Name,
					p.Price
				}).ToListAsync();
		}

		public async Task<dynamic> GetOtherPayKind() {
			return await formatPayKind(ctx.PayKinds
				.Where(p => p.Type == PayKindType.Other))
				.FirstOrDefaultAsync();
		}

		public async Task<bool> ToggleMenuStatus(string menuId, MenuStatus status) {
			var menu = await ctx.Menus.Where(m => m.Id == menuId).FirstOrDefaultAsync();
			if(menu == null) {
				return false;
			}
			if(!Enum.IsDefined(status.GetType(), status)) {
				return false;
			}
			menu.Status = status;
			ctx.SaveChanges();
			return true;
		}

		public async Task<List<dynamic>> GetHistoryDines() {
			IQueryable<Dine> linq = ctx.Dines.Where(p => p.Status != DineStatus.Shifted);
			return await formatDine(linq.OrderByDescending(p => p.Id)).ToListAsync();
		}
		public async Task<List<dynamic>> GetHistoryDines(string waiterId) {
			IQueryable<Dine> linq = ctx.Dines.Where(p => p.WaiterId == waiterId && p.Status != DineStatus.Shifted);
			return await formatDine(linq.OrderByDescending(p => p.Id)).ToListAsync();
		}
		public async Task<List<dynamic>> GetHistoryDines(string waiterId, string deskId) {
			IQueryable<Dine> linq = ctx.Dines.Where(p => p.WaiterId == waiterId && p.DeskId == deskId && p.Status != DineStatus.Shifted);
			return await formatDine(linq.OrderByDescending(p => p.Id)).ToListAsync();
		}

		public async Task ShiftDines() {
			var dines = ctx.Dines.Where(d => d.Status != DineStatus.Shifted);
			foreach(var dine in dines) {
				dine.Status = DineStatus.Shifted;
			}
			var desks = ctx.Desks;
			foreach(var desk in desks) {
				desk.Status = DeskStatus.StandBy;
			}
			await ctx.SaveChangesAsync();
		}

		public async Task AddShifts(Models.Shift shift) {
			int id = 1;
			DateTime now = DateTime.Now;
			var Day = await ctx.PayKindShifts.Where(p => SqlFunctions.DateDiff("day", p.DateTime, now) == 0).ToListAsync();
			if(Day.Count != 0) {
				id = Day.Max(p => p.Id) + 1;
			}

			ctx.Shifts.Add(new Shift {
				Id = id,
				DateTime = now,
				AveragePrice = shift.AveragePrice,
				CustomerCount = shift.CustomerCount,
				DeskCount = shift.DeskCount,
				GiftPrice = shift.GiftPrice,
				OriPrice = shift.OriPrice,
				PreferencePrice = shift.PreferencePrice,
				Price = shift.Price,
				ReturnedPrice = shift.ReturnedPrice,
				ToGoPrice = shift.ToGoPrice,
				ToStayPrice = shift.ToStayPrice
			});

			shift.PayKindShifts.ForEach(p => {
				ctx.PayKindShifts.Add(new PayKindShift {
					Id = id,
					DateTime = now,
					PayKindId = p.PayKindId,
					RealPrice = p.RealPrice,
					ReceivablePrice = p.ReceivablePrice
				});
			});

			shift.MenuClassShifts.ForEach(p => {
				ctx.MenuClassShifts.Add(new MenuClassShift {
					Id = id,
					DateTime = now,
					MenuClassId = p.MenuClassId,
					Price = p.Price
				});
			});

			await ctx.SaveChangesAsync();
		}
	}

	// 服务员相关
	public partial class HotelManager {
		public async Task<List<StaffRoleSchema>> GetStaffRoles(string staffId) {
			ICollection<StaffRole> roles = await ctx.Staffs
				.Include(p => p.StaffRoles)
				.Where(p => p.Id == staffId)
				.Select(p => p.StaffRoles)
				.FirstOrDefaultAsync();
			List<StaffRoleSchema> schemas = new List<StaffRoleSchema>();
			if(roles == null) {
				return schemas;
			}
			foreach(StaffRole role in roles) {
				List<StaffRoleSchema> tSchemas = await ctx.StaffRoleSchemas.Where(p => p.StaffRole.Id == role.Id).ToListAsync();
				tSchemas?.ForEach(s => {
					if(!schemas.Contains(s)) {
						schemas.Add(s);
					}
				});
			}
			return schemas;
		}
		public async Task<bool> IsStaffHasSchema(string staffId, Schema schema) {
			List<StaffRoleSchema> schemas = await GetStaffRoles(staffId);
			if(schemas.Count == 0) {
				return false;
			}
			foreach(StaffRoleSchema s in schemas) {
				if(s.Schema == schema) {
					return true;
				}
			};
			return false;
		}
		public async Task<dynamic> GetStaffs() {
			return await ctx.Staffs.Select(p => new {
				p.Id,
				p.Name,
				Schemas = p.StaffRoles.SelectMany(pp => pp.Schemas.Select(s => s.Schema))
			}).ToListAsync();
		}
	}
}