﻿using HotelDAO.Models;
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
						p.Area.Name
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

		public async Task<List<dynamic>> GetHistoryDines(string waiterId, string deskId = null) {
			IQueryable<Dine> linq = ctx.Dines.Where(p => p.WaiterId == waiterId && p.Status != DineStatus.Shifted);
			if(deskId != null) {
				linq = linq.Where(p => p.DeskId == deskId);
			}
			return await formatDine(linq.OrderByDescending(p => p.Id)).ToListAsync();
		}

		public async Task ShiftDines() {
			var dines = ctx.Dines.Where(d => d.IsPaid == true && d.Status != DineStatus.Shifted);
			foreach(var dine in dines) {
				dine.Status = DineStatus.Shifted;
			}
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