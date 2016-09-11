using HotelDAO.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace OrderSystem {
	public partial class HotelManager : HotelDAO.BaseHotelManager {
		public HotelManager(string connString) : base(connString) { }

		public async Task<Desk> GetDeskByQrCode(string qrCode) {
			return await ctx.Desks.Include(p => p.Area).FirstOrDefaultAsync(p => p.QrCode == qrCode);
		}

		public async Task<PayKind> GetPayKindById(int payKindId) {
			return await ctx.PayKinds.FirstOrDefaultAsync(p => p.Id == payKindId);
		}

		public async Task<Customer> GetCustomer(string userId) {
			return await ctx.Customers.Include(p => p.VipLevel).FirstOrDefaultAsync(p => p.Id == userId);
		}
		public async Task<List<string>> GetUserAddresses(string userId) {
			YummyOnlineDAO.Models.YummyOnlineContext yummyOnlineCtx = new YummyOnlineDAO.Models.YummyOnlineContext();
			return await yummyOnlineCtx.UserAddresses.Where(p => p.UserId == userId).Select(p => p.Address).ToListAsync();
		}

		public async Task<DinePaidDetail> GetDineOnlinePaidDetail(string dineId) {
			return await ctx.DinePaidDetails.Include(p=>p.PayKind).FirstOrDefaultAsync(p => p.DineId == dineId && p.PayKind.Type == PayKindType.Online);
		}

		public async Task<bool> IsDinePaid(string dineId) {
			bool isPaid = await ctx.Dines.Where(p => p.Id == dineId).Select(p => p.IsPaid).FirstOrDefaultAsync();
			return isPaid;
		}
		public async Task<int> GetHistoryDinesCount(string userId) {
			return await ctx.Dines.CountAsync(p => p.UserId == userId);
		}

		public async Task<bool> TransferDines(string oldUserId, string newUserId) {
			try {
				List<Dine> dines = await ctx.Dines.Where(p => p.UserId == oldUserId).ToListAsync();
				foreach(Dine d in dines) {
					d.UserId = newUserId;
				}
				await ctx.SaveChangesAsync();
				return true;
			}
			catch {
				return false;
			}
		}
	}
}