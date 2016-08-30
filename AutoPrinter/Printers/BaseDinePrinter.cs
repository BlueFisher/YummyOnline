using Protocol.PrintingProtocol;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPrinter {
	public class BaseDinePrinter : BasePrinter {
		/// <summary>
		/// 生成收银条图片
		/// </summary>
		protected Bitmap generateReciptBmp(DineForPrinting protocol, bool isFullDineMenus) {
			Bitmap bmp = new Bitmap(protocol.PrinterFormat.PaperSize, maxHeight);
			Graphics g = Graphics.FromImage(bmp);
			int realHeight = drawRecipt(g, protocol, isFullDineMenus);
			return cutBmp(bmp, realHeight);
		}
		/// <summary>
		/// 根据Graphics绘制收银条
		/// </summary>
		protected int drawRecipt(Graphics g, DineForPrinting protocol, bool isFullDineMenus) {
			PrinterGraphics printerG = new PrinterGraphics(g, protocol.PrinterFormat.PaperSize, protocol.PrinterFormat.Font, protocol.PrinterFormat.PaddingRight);

			printerG.DrawStringLine($"欢迎光临{protocol.Hotel.Name}", protocol.PrinterFormat.ReciptBigFontSize, align: StringAlignment.Center);
			printerG.DrawStringLine($"{protocol.Hotel.Address}", protocol.PrinterFormat.ReciptSmallFontSize, align: StringAlignment.Center);
			printerG.DrawStringLine($"收据", protocol.PrinterFormat.ReciptSmallFontSize, align: StringAlignment.Center);
			printerG.TrimY(5);

			printerG.DrawStringLine($"单号: {protocol.Dine.Id}", protocol.PrinterFormat.ReciptFontSize);
			printerG.DrawStringLine($"时间: {protocol.Dine.BeginTime.ToString("M-d HH:mm")}", protocol.PrinterFormat.ReciptFontSize);

			printGrid55(printerG, new string[] { $"顾客: {protocol.User?.Id}", $"服务员: {protocol.Dine.Waiter.Name}" }, protocol.PrinterFormat.ReciptFontSize);

			if(protocol.Dine.Type == HotelDAO.Models.DineType.ToStay) {
				printerG.DrawStringLine($"餐桌: {protocol.Dine.Desk.Name}", protocol.PrinterFormat.ReciptFontSize + 2);
			}
			else if(protocol.Dine.Type == HotelDAO.Models.DineType.ToGo) {
				printerG.DrawStringLine($"外卖: {protocol.Dine.Desk.Name}", protocol.PrinterFormat.ReciptBigFontSize);
				if(protocol.Dine.TakeOut.RecordId != null) {
					printerG.DrawStringLine($"外卖平台编号: {protocol.Dine.TakeOut.RecordId}", protocol.PrinterFormat.ReciptBigFontSize);
				}
				printerG.DrawStringLine($"姓名: {protocol.Dine.TakeOut.Name}", protocol.PrinterFormat.ReciptBigFontSize);
				printerG.DrawStringLine($"手机: {protocol.Dine.TakeOut.PhoneNumber}", protocol.PrinterFormat.ReciptBigFontSize);
				printerG.DrawStringLine($"地址: {protocol.Dine.TakeOut.Address}", protocol.PrinterFormat.ReciptBigFontSize);
			}

			printHr(printerG);

			printGridRecipt(printerG, new string[] { "名称", "数量", "单价", "折后小计" }, protocol.PrinterFormat.ReciptFontSize);

			printHr(printerG);

			decimal priceAll = 0m;
			foreach(DineMenu dineMenu in protocol.Dine.DineMenus.Where(p => p.Status != HotelDAO.Models.DineMenuStatus.Returned)) {
				// 打印具体菜品信息
				printGridRecipt(printerG, new string[] {
					dineMenu.Menu.Name,
					dineMenu.Count.ToString(),
					dineMenu.OriPrice.ToString(),
					(dineMenu.Price * dineMenu.Count).ToString()
				}, protocol.PrinterFormat.ReciptFontSize);

				priceAll += dineMenu.Price * dineMenu.Count;

				// 如果菜品为套餐，则打印套餐包含的具体菜品信息
				if(dineMenu.Menu.IsSetMeal) {
					List<SetMealMenu> setMealMenus = dineMenu.Menu.SetMealMenus;
					for(int i = 0; i < setMealMenus.Count; i++) {
						char tab = '├';
						if(i == setMealMenus.Count - 1) {
							tab = '└';
						}
						printGridRecipt(printerG, new string[] {
							$"{tab} {setMealMenus[i].Name}",
							setMealMenus[i].Count.ToString(),
							null, null
						}, protocol.PrinterFormat.ReciptFontSize);
					}
				}

				// 打印菜品的备注信息
				var remarks = dineMenu.Remarks.ToList();
				for(int i = 0; i < dineMenu.Remarks.Count; i++) {
					char tab = '├';
					if(i == dineMenu.Remarks.Count - 1) {
						tab = '└';
					}
					printGridRecipt(printerG, new string[] {
						$"{tab} {remarks[i].Name}",
						null,
						0 == remarks[i].Price ? null : remarks[i].Price.ToString(),
						0 == remarks[i].Price ? null : remarks[i].Price.ToString()
					}, protocol.PrinterFormat.ReciptFontSize);

					priceAll += remarks[i].Price;
				}
			};

			printHr(printerG);

			if(isFullDineMenus) {
				priceAll = protocol.Dine.Price;
			}
			printGrid55f(printerG, new string[] { "总计", priceAll.ToString() }, protocol.PrinterFormat.ReciptBigFontSize);

			if(protocol.Dine.Discount < 1) {
				printerG.DrawStringLine($"{protocol.Dine.DiscountName}: {protocol.Dine.Discount * 10}折", protocol.PrinterFormat.ReciptFontSize);
			}

			string paidWay = protocol.Dine.IsOnline ? "线上支付" : "线下支付";
			printerG.DrawStringLine($"支付方式: {paidWay}", protocol.PrinterFormat.ReciptFontSize);
			foreach(DinePaidDetail dinePaidDetail in protocol.Dine.DinePaidDetails) {
				decimal dinePaidDetailPrice = dinePaidDetail.Price;
				if(dinePaidDetail.PayKind.Type == HotelDAO.Models.PayKindType.Cash) {
					dinePaidDetailPrice += protocol.Dine.Change;
				}
				printerG.DrawStringLine($"{dinePaidDetail.PayKind.Name}: ￥{dinePaidDetailPrice}", protocol.PrinterFormat.ReciptFontSize);
			}
			if(protocol.Dine.IsPaid) {
				printerG.DrawStringLine($"找零: ￥{protocol.Dine.Change}", protocol.PrinterFormat.ReciptFontSize);
			}
			else {
				printerG.DrawStringLine("未支付", protocol.PrinterFormat.ReciptFontSize);
			}

			printHr(printerG);

			printerG.DrawStringLine($"订餐电话: {protocol.Hotel.Tel}", protocol.PrinterFormat.ReciptFontSize, style: FontStyle.Bold);
			printerG.DrawStringLine("此小票恕不做开发票凭据，如需开票请用餐后立即与收银台联系，过时不候！", protocol.PrinterFormat.ReciptFontSize, style: FontStyle.Bold);
			printerG.DrawStringLine("[上海乔曦信息技术有限公司竭诚为您服务021-66601020]", protocol.PrinterFormat.ReciptSmallFontSize);

			printEnd(printerG);

			g.Dispose();

			return printerG.GetHeight();
		}

		/// <summary>
		/// 生成传菜单图片
		/// </summary>
		protected Bitmap generateServeOrderBmp(DineForPrinting protocol) {
			Bitmap bmp = new Bitmap(protocol.PrinterFormat.PaperSize, maxHeight);
			Graphics g = Graphics.FromImage(bmp);
			int realHeight = drawServeOrder(g, protocol);
			return cutBmp(bmp, realHeight);
		}
		/// <summary>
		/// 根据Graphics绘制传菜单
		/// </summary>
		protected int drawServeOrder(Graphics g, DineForPrinting protocol) {
			PrinterGraphics printerG = new PrinterGraphics(g, protocol.PrinterFormat.PaperSize, protocol.PrinterFormat.Font, protocol.PrinterFormat.PaddingRight);

			printerG.DrawStringLine(protocol.Dine.Desk.ServeDepartmentName, protocol.PrinterFormat.ServeOrderFontSize);

			printerG.DrawStringLine($"单号: {protocol.Dine.Id}", protocol.PrinterFormat.ServeOrderSmallFontSize);
			printerG.DrawStringLine($"时间: {protocol.Dine.BeginTime.ToString("M-d HH:mm")}", protocol.PrinterFormat.ServeOrderSmallFontSize);

			printerG.DrawStringLine($"餐桌: {protocol.Dine.Desk.Name}", protocol.PrinterFormat.ServeOrderFontSize);

			printHr(printerG);

			printGrid82(printerG, new string[] { "名称", "数量" }, protocol.PrinterFormat.ServeOrderSmallFontSize);

			printHr(printerG);

			foreach(DineMenu dineMenu in protocol.Dine.DineMenus.Where(p => p.Status != HotelDAO.Models.DineMenuStatus.Returned)) {
				// 打印具体菜品信息
				printGrid82(printerG, new string[] { dineMenu.Menu.Name, dineMenu.Count.ToString() }, protocol.PrinterFormat.ServeOrderFontSize);

				// 如果菜品为套餐，则打印套餐包含的具体菜品信息
				if(dineMenu.Menu.IsSetMeal) {
					List<SetMealMenu> setMealMenus = dineMenu.Menu.SetMealMenus;
					for(int i = 0; i < setMealMenus.Count; i++) {
						char tab = '├';
						if(i == setMealMenus.Count - 1) {
							tab = '└';
						}
						printGrid82(printerG, new string[] { $"{tab} {setMealMenus[i].Name}", setMealMenus[i].Count.ToString() }, protocol.PrinterFormat.ServeOrderFontSize);
					}
				}

				// 打印菜品的备注信息
				var remarks = dineMenu.Remarks.ToList();
				for(int i = 0; i < dineMenu.Remarks.Count; i++) {
					char tab = '├';
					if(i == dineMenu.Remarks.Count - 1) {
						tab = '└';
					}
					printGrid82(printerG, new string[] { $"{tab} {remarks[i].Name}", null, }, protocol.PrinterFormat.ServeOrderFontSize);
				}
			};

			printEnd(printerG);

			return printerG.GetHeight();
		}

		/// <summary>
		/// 生成厨房单图片
		/// </summary>
		protected Bitmap generateKitchenOrderBmp(DineForPrinting protocol, DineMenu dineMenu, SetMealMenu setMealMenu) {
			Bitmap bmp = new Bitmap(protocol.PrinterFormat.PaperSize, maxHeight);
			Graphics g = Graphics.FromImage(bmp);
			int realHeight = drawKitchenOrder(g, protocol, dineMenu, setMealMenu);
			return cutBmp(bmp, realHeight);
		}
		/// <summary>
		/// 根据Graphics绘制厨房单
		/// </summary>
		protected int drawKitchenOrder(Graphics g, DineForPrinting protocol, DineMenu dineMenu, SetMealMenu setMealMenu) {
			PrinterGraphics printerG = new PrinterGraphics(g, protocol.PrinterFormat.PaperSize, protocol.PrinterFormat.Font, protocol.PrinterFormat.PaddingRight);

			printerG.DrawStringLine(dineMenu.Menu.DepartmentName, protocol.PrinterFormat.KitchenOrderFontSize);

			printerG.DrawStringLine($"单号: {protocol.Dine.Id}", protocol.PrinterFormat.KitchenOrderSmallFontSize);
			printerG.DrawStringLine($"时间: {protocol.Dine.BeginTime.ToString("M-d HH:mm")}", protocol.PrinterFormat.KitchenOrderSmallFontSize);

			if(dineMenu.Status == HotelDAO.Models.DineMenuStatus.Returned) {
				string returnStr = "退菜";
				if(dineMenu.ReturnedReason != null)
					returnStr += $", 理由: {dineMenu.ReturnedReason}";
				printerG.DrawStringLine(returnStr, protocol.PrinterFormat.KitchenOrderFontSize);
			}

			printerG.DrawStringLine($"餐桌: {protocol.Dine.Desk.Name}", protocol.PrinterFormat.KitchenOrderFontSize);

			printGrid82(printerG, new string[] { dineMenu.Menu.Name, dineMenu.Count.ToString() }, protocol.PrinterFormat.KitchenOrderFontSize);

			if(setMealMenu != null) {
				printGrid82(printerG, new string[] { $"└ {setMealMenu.Name}", setMealMenu.Count.ToString() }, protocol.PrinterFormat.KitchenOrderFontSize);
			}

			// 打印菜品的备注信息
			var remarks = dineMenu.Remarks.ToList();
			for(int i = 0; i < dineMenu.Remarks.Count; i++) {
				char tab = '├';
				if(i == dineMenu.Remarks.Count - 1) {
					tab = '└';
				}
				printGrid82(printerG, new string[] { $"{tab} {remarks[i].Name}", null, }, protocol.PrinterFormat.KitchenOrderFontSize);
			}

			printEnd(printerG);

			return printerG.GetHeight();
		}
	}
}
