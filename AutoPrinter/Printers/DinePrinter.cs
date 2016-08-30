using Protocol;
using Protocol.PrintingProtocol;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Drawing.Printing;

namespace AutoPrinter {
	public class DineDriverPrinter : BaseDinePrinter {
		public void Print(DineForPrinting protocol, List<PrintType> printTypes, bool isFullDineMenus) {
			foreach(PrintType type in printTypes) {
				if(type == PrintType.Recipt) {
					if(protocol.Dine.Desk.ReciptPrinter == null || protocol.Dine.Desk.ReciptPrinter.Name == "不打印") {
						continue;
					}

					PrintDocument pd = new PrintDocument();
					pd.PrinterSettings.PrinterName = protocol.Dine.Desk.ReciptPrinter.Name;
					pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", protocol.PrinterFormat.PaperSize, maxHeight);
					pd.PrintPage += (sender, e) => {
						drawRecipt(e.Graphics, protocol, isFullDineMenus);
					};
					pd.Print();
				}
				else if(type == PrintType.ServeOrder) {
					if(protocol.Dine.Desk.ServePrinter == null || protocol.Dine.Desk.ServePrinter.Name == "不打印") {
						continue;
					}

					PrintDocument pd = new PrintDocument();
					pd.PrinterSettings.PrinterName = protocol.Dine.Desk.ServePrinter.Name;
					pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", protocol.PrinterFormat.PaperSize, maxHeight);
					pd.PrintPage += (sender, e) => {
						drawServeOrder(e.Graphics, protocol);
					};
					pd.Print();
				}
				else if(type == PrintType.KitchenOrder) {
					foreach(DineMenu dineMenu in protocol.Dine.DineMenus.Where(p => p.Status != HotelDAO.Models.DineMenuStatus.Returned)) {
						if(dineMenu.Menu.Printer == null || dineMenu.Menu.Printer.Name == "不打印") {
							continue;
						}

						if(!dineMenu.Menu.IsSetMeal) {
							PrintDocument pd = new PrintDocument();
							pd.PrinterSettings.PrinterName = dineMenu.Menu.Printer.Name;
							pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", protocol.PrinterFormat.PaperSize, maxHeight);
							pd.PrintPage += (sender, e) => {
								drawKitchenOrder(e.Graphics, protocol, dineMenu, null);
							};
							pd.Print();
						}
						else {
							foreach(SetMealMenu setMealMenu in dineMenu.Menu.SetMealMenus) {
								PrintDocument pd = new PrintDocument();
								pd.PrinterSettings.PrinterName = dineMenu.Menu.Printer.Name;
								pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", protocol.PrinterFormat.PaperSize, maxHeight);
								pd.PrintPage += (sender, e) => {
									drawKitchenOrder(e.Graphics, protocol, dineMenu, setMealMenu);
								};
								pd.Print();
							}
						}
					}
				}
			}
		}
	}
	public class DinePrinter : BaseDinePrinter {
		public async Task Print(DineForPrinting protocol, List<PrintType> printTypes, bool isFullDineMenus) {
			handleIPPrinterFormat(protocol.PrinterFormat);

			List<Task> allTasks = new List<Task>();
			foreach(PrintType type in printTypes) {
				if(type == PrintType.Recipt) {
					if(protocol.Dine.Desk.ReciptPrinter == null) {
						continue;
					}

					IPAddress ip = IPAddress.Parse(protocol.Dine.Desk.ReciptPrinter.IpAddress);
					Bitmap bmp = generateReciptBmp(protocol, isFullDineMenus);
					allTasks.Add(IPPrinter.GetInstance().Print(ip, bmp, protocol.PrinterFormat.ColorDepth));
				}
				else if(type == PrintType.ServeOrder) {
					if(protocol.Dine.Desk.ServePrinter == null) {
						continue;
					}

					IPAddress ip = IPAddress.Parse(protocol.Dine.Desk.ServePrinter.IpAddress);
					Bitmap bmp = generateServeOrderBmp(protocol);
					allTasks.Add(IPPrinter.GetInstance().Print(ip, bmp, protocol.PrinterFormat.ColorDepth));
				}
				else if(type == PrintType.KitchenOrder) {
					foreach(DineMenu dineMenu in protocol.Dine.DineMenus.Where(p => p.Status != HotelDAO.Models.DineMenuStatus.Returned)) {
						if(dineMenu.Menu.Printer == null) {
							continue;
						}

						if(!dineMenu.Menu.IsSetMeal) {
							IPAddress ip = IPAddress.Parse(dineMenu.Menu.Printer.IpAddress);
							Bitmap bmp = generateKitchenOrderBmp(protocol, dineMenu, null);
							allTasks.Add(IPPrinter.GetInstance().Print(ip, bmp, protocol.PrinterFormat.ColorDepth));
						}
						else {
							foreach(SetMealMenu setMealMenu in dineMenu.Menu.SetMealMenus) {
								IPAddress ip = IPAddress.Parse(dineMenu.Menu.Printer.IpAddress);
								Bitmap bmp = generateKitchenOrderBmp(protocol, dineMenu, setMealMenu);
								allTasks.Add(IPPrinter.GetInstance().Print(ip, bmp, protocol.PrinterFormat.ColorDepth));
							}
						}
					}
				}
			}

			foreach(var t in allTasks) {
				await t;
			}
		}
	}
}
