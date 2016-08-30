using HotelDAO.Models;
using Protocol;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Threading.Tasks;

namespace HotelDAO {
	public partial class BaseHotelManager {
		public async Task<dynamic> GetFormatedDineById(string dineId) {
			dynamic dine = await formatDine(ctx.Dines
				.Where(p => p.Id == dineId))
				.FirstOrDefaultAsync();
			return dine;
		}

		public async Task<List<dynamic>> GetFormatedHistoryDines(string userId) {
			return await formatDine(ctx.Dines
				.Where(p => p.UserId == userId)
				.OrderByDescending(p => p.Id))
				.ToListAsync();
		}

		protected IQueryable<dynamic> formatDine(IQueryable<Dine> dine) {
			return dine.Select(p => new {
				p.Id,
				p.Status,
				p.Type,
				p.From,
				p.HeadCount,
				p.Price,
				p.OriPrice,
				p.Change,
				p.Discount,
				p.DiscountName,
				p.DiscountType,
				p.BeginTime,
				p.IsPaid,
				p.IsOnline,
				p.UserId,
				p.WeChatOpenId,
				Waiter = new {
					p.Waiter.Id,
					p.Waiter.Name
				},
				Clerk = new {
					p.Clerk.Id,
					p.Clerk.Name
				},
				Remarks = p.Remarks.Select(pp => new {
					pp.Id,
					pp.Name
				}),
				Desk = new {
					p.Desk.Id,
					p.Desk.QrCode,
					p.Desk.Name,
					p.Desk.Description,
					AreaType = p.Desk.Area.Type
				},
				DineMenus = p.DineMenus.Select(d => new {
					d.Id,
					d.Status,
					d.Count,
					d.OriPrice,
					d.Price,
					d.RemarkPrice,
					Remarks = d.Remarks.Select(r => new {
						r.Id,
						r.Name,
						r.Price
					}),
					Menu = new {
						d.Menu.Id,
						d.Menu.Code,
						d.Menu.Name,
						d.Menu.NameAbbr,
						d.Menu.PicturePath,
						d.Menu.Unit
					},
					ReturnedWaiter = new {
						d.ReturnedWaiter.Id,
						d.ReturnedWaiter.Name
					},
					d.ReturnedReason
				}),
				DinePaidDetails = p.DinePaidDetails.Select(d => new {
					d.Price,
					d.RecordId,
					PayKind = new {
						d.PayKind.Id,
						d.PayKind.Name,
						d.PayKind.Type
					}
				}),
				TakeOut = new {
					p.TakeOut.Address,
					p.TakeOut.Name,
					p.TakeOut.PhoneNumber,
					p.TakeOut.RecordId
				}
			});
		}
	}
}