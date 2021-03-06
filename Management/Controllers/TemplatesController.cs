﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Management;
using Management.ObjectClasses;
using HotelDAO.Models;
using System.Data.Entity;
using Newtonsoft.Json;
using YummyOnlineDAO.Models;
using Management.Models;
using Protocol;
using System.Data.Entity.SqlServer;

namespace Management.Controllers
{
    public class TemplatesController : BaseController
    {
        // GET: Error
        public ActionResult Error()
        {
            return View("Error-500");
        }

        public ActionResult Order()
        {
            return View("Order");
        }

        public ActionResult TakeOut()
        {
            return View("TakeOut");
        }

        public ActionResult Return()
        {
            return View("Return");
        }


        public ActionResult Conbine()
        {
            return View("Conbine");
        }

        public ActionResult Replace()
        {
            return View("Replace");
        }
        public ActionResult HandOut()
        {
            return View("HandOut");
        }

        public ActionResult AddMenu()
        {
            return View("AddMenu");
        }

        public ActionResult RePrinter()
        {
            return View("RePrinter");
        }

        public ActionResult PayAll()
        {
            return View("PayAll");
        }
        /// <summary>
        /// 获取信息
        /// </summary>
        /// <returns>订单 和 区域 </returns>
        public async Task<JsonResult> getArea()
        {
            using (var db = new HotelContext(Session["ConnectString"] as string))
            {
                var Areas = await db.Areas.Where(area => area.Usable == true)
                    .ToListAsync();
                var Desks = await db.Desks.Where(d => d.Usable == true)
                    .Select(d => new
                    {
                        d.AreaId,
                        d.Name,
                        d.Status,
                        d.Id,
                        d.Order
                    })
                    .OrderBy(d => d.Order)
                    .ToListAsync();
                return Json(new { Areas = Areas, Desks = Desks });
            }
        }
        /// <summary>
        /// 获取支付方式列表  线下的
        /// </summary>
        /// <returns>列表</returns>
        public async Task<JsonResult> getPay()
        {
            var Dines = db.Dines
                    .Include(p => p.DineMenus.Select(pp => pp.Remarks))
                    .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                    .Where(order => order.IsPaid == false && order.IsOnline == false)
                    .Select(d => new
                    {
                        d.Discount,
                        d.DiscountName,
                        d.Id,
                        DineMenus = d.DineMenus.Select(dd => new
                        {
                            Count = dd.Count,
                            DineId = dd.DineId,
                            Id = dd.Id,
                            Menu = dd.Menu,
                            MenuId = dd.MenuId,
                            OriPrice = dd.OriPrice,
                            Price = dd.Price,
                            RemarkPrice = dd.RemarkPrice,
                            Remarks = dd.Remarks,
                            ReturnedWaiterId = dd.ReturnedWaiterId,
                            Status = dd.Status,
                            Type = dd.Type
                        }),
                        Menu = d.DineMenus.Select(dd => new { dd.Menu, dd.Menu.MenuPrice }),
                        d.DeskId,
                        d.BeginTime,
                        d.HeadCount,
                        d.UserId,
                        d.OriPrice,
                        d.Price
                    });
            var Pays = db.PayKinds.Where(p => p.Usable == true && (p.Type == PayKindType.Offline || p.Type == PayKindType.Points || p.Type == PayKindType.Cash)).Select(p => new { p.Id, p.Name, p.Type });
            var Discounts = db.TimeDiscounts;
            var day = DateTime.Now.DayOfWeek;
            var onsale = await db.MenuOnSales.Where(m => m.OnSaleWeek == day).ToListAsync();
            var PayElements = new
            {
                Dines = await Dines.ToListAsync(),
                Pays = await Pays.ToListAsync(),
                Discounts = await Discounts.ToListAsync(),
                OnSaleMenus = onsale
            };
            return Json(PayElements);
        }
        /// <summary>
        /// 用户登陆 
        /// </summary>
        /// <param name="CustomerId"></param>
        /// <param name="Phone"></param>
        /// <param name="Password"></param>
        /// <returns>先判断是否已经有用户，如果没有根据手机找，再更改匿名用户</returns>
        public async Task<JsonResult> getUser(string CustomerId, string Phone, string Password)
        {
            string PassHash = Models.Method.GetMd5(Password);
            var sys = new YummyOnlineContext();
            var User = db.Customers.Where(c => c.Id == CustomerId)
                        .Include(p => p.VipLevel.VipDiscount);//找到了一个已有用户
            if (User.Count() > 0)
            {
                var data = await User.FirstOrDefaultAsync();
                return Json(new { Status = true, data = data });
            }
            else
            {
                var login = sys.Users.Where(c => c.PhoneNumber == Phone && c.PasswordHash == PassHash);
                if (login.Count() == 0) return Json(new { Status = false });
                var lg = await login.FirstOrDefaultAsync();
                string Id = lg.Id;
                //登陆替换匿名用户
                var dineAnonymous = db.Dines.Where(d => d.UserId == CustomerId);
                foreach (var p in dineAnonymous)
                {
                    p.UserId = Id;
                }
                db.SaveChanges();
                var us = db.Customers.Where(c => c.Id == Id).Include(p => p.VipLevel.VipDiscount);
                var data = await us.FirstOrDefaultAsync();
                return Json(new { Status = true, data = data });
            }
        }
        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="PayList"></param>
        /// <param name="Dine"></param>
        /// <param name="UserId"></param>
        /// <returns>支付完成之后的订单</returns>
        [Authorize]
        public async Task<JsonResult> PayDine(List<PayMoney> PayList, PayDine Dine, string UserId)
        {
            if (Dine.Discount <= 0) return Json(new { Status = false, ErrorMessage = "折扣方案为负，重新输入" });
            decimal totalPrcie = 0;//总价
            var dine = await db.Dines.Where(d => d.Id == Dine.Id)
                .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                .FirstOrDefaultAsync();
            var HtManage = new HotelManager(ConnectingStr);
            var NowDay = DateTime.Now.DayOfWeek;
            var SpecialMenus = await db.MenuOnSales.Where(d => d.OnSaleWeek == NowDay).ToListAsync();
            foreach (var menu in dine.DineMenus)
            {
                if (menu.Status == DineMenuStatus.Normal)
                {
                    var temp = SpecialMenus.Where(d => d.Id == menu.MenuId).FirstOrDefault();
                    if (temp != null)
                    {
                        totalPrcie += temp.Price;
                    }
                    else
                    {
                        if (menu.Menu.MenuPrice.ExcludePayDiscount)
                        {//不打折
                            totalPrcie += menu.OriPrice * menu.Count + menu.RemarkPrice;
                        }
                        else
                        {//整单打折
                            totalPrcie += menu.OriPrice * menu.Count * (decimal)(Dine.Discount * 1.0 / 100) + menu.RemarkPrice;
                        }
                    }
                }

            }
            decimal PayTotal = 0;//总共价格
            decimal Point = 0;//积分
            decimal Cash = 0;//现金
            decimal PayExceptPoint = 0; //除去积分支付的价钱
            foreach (var pay in PayList)
            {
                if (pay.Type == (int)PayKindType.Points) Point = pay.Number;
                if (pay.Type == (int)PayKindType.Cash) Cash = pay.Number;
                PayTotal += pay.Number;
            }
            PayExceptPoint = PayTotal - Point;
            if (PayTotal < totalPrcie)
            {//支付钱不对
                return Json(new { Status = false, ErrorMessage = "金额不正确重新输入" });
            }
            else if (PayTotal == totalPrcie)
            {//支付钱和价钱相等 
                foreach (var pay in PayList)
                {
                    var paid = new DinePaidDetail();
                    paid.DineId = Dine.Id;
                    paid.PayKindId = pay.Id;
                    paid.Price = pay.Number;
                    db.DinePaidDetails.Add(paid);
                }
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == UserId);
                if (customer != null)
                {
                    customer.Points -= (int)Point;
                    customer.Points += (int)(PayExceptPoint);
                }
                var SysCustomer = await sysdb.Users.FirstOrDefaultAsync(d => d.Id == UserId);
                if (SysCustomer != null)
                {
                    SysCustomer.Price += totalPrcie;
                    await sysdb.SaveChangesAsync();
                }

            }
            else
            {//支付找零
                decimal charge = PayTotal - totalPrcie;
                if (Cash >= charge)
                {//支付找零
                    Cash -= charge;
                    var paid = new DinePaidDetail();
                    paid.DineId = Dine.Id;
                    paid.PayKindId = await db.PayKinds
                        .Where(p => p.Type == PayKindType.Cash)
                        .Select(p => p.Id).FirstOrDefaultAsync();
                    paid.Price = Cash;
                    db.DinePaidDetails.Add(paid);
                    foreach (var pay in PayList)
                    {
                        paid = new DinePaidDetail();
                        if (pay.Type == (int)PayKindType.Cash) continue;
                        paid.DineId = Dine.Id;
                        paid.PayKindId = pay.Id;
                        paid.Price = pay.Number;
                        db.DinePaidDetails.Add(paid);
                    }
                    var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == UserId);
                    if (customer != null)
                    {
                        customer.Points -= Point > totalPrcie ? (int)totalPrcie : (int)Point;
                        if (totalPrcie - Point > 0)
                        {
                            customer.Points += (int)(totalPrcie - Point);
                        }
                    }
                    var SysCustomer = await sysdb.Users.FirstOrDefaultAsync(d => d.Id == UserId);
                    if (SysCustomer != null)
                    {
                        SysCustomer.Price += totalPrcie;
                        await sysdb.SaveChangesAsync();
                    }
                    dine.Change = charge;
                }
                else
                {
                    return Json(new ErrorState("找零金额不能大于现金金额"));
                }
            }
            dine.Price = totalPrcie;
            dine.Discount = Dine.Discount * 1.0 / 100;
            if (Dine.Type == 0 || Dine.Type == null)
            {
                dine.DiscountType = DiscountType.Custom;

            }
            else if (Dine.Type == 1)
            {
                dine.DiscountType = DiscountType.Time;
            }
            else if (Dine.Type == 2)
            {
                dine.DiscountType = DiscountType.Vip;
            }
            dine.DiscountName = Dine.DiscountName;
            foreach (var i in dine.DineMenus)
            {
                if (i.Status == DineMenuStatus.Normal)
                {
                    var temp = SpecialMenus.Where(d => d.Id == i.MenuId).FirstOrDefault();
                    if (temp != null)
                    {
                        continue;
                    }
                    else
                    {
                        if (i.Menu.MenuPrice.ExcludePayDiscount)
                        {

                        }
                        else
                        {
                            i.Price = i.OriPrice * (decimal)(Dine.Discount * 1.0 / 100);
                        }
                    }
                }
            }
            dine.IsPaid = true;
            db.SaveChanges();
            try
            {
                var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                if (isprint)
                {
                    if (dine.Status == DineStatus.Printed)
                    {
                        MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, dine.Id, new List<int>(), new List<PrintType>() { PrintType.Recipt }));
                    }
                    else
                    {
                        MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, dine.Id, new List<int>(), new List<PrintType>() { PrintType.Recipt, PrintType.KitchenOrder, PrintType.ServeOrder }));
                    }
                }
            }
            catch
            {

            }

            HtManage.ManageLog($"HotelId: {(Session["User"] as RStatus).HotelId.ToString()},DineId:{dine.Id} compeleted");
            MvcApplication.client.Send(new NewDineInformProtocol((int)(Session["User"] as RStatus).HotelId, dine.Id, true));
            var CurDesk = dine.DeskId;
            var CleanDeskDine = db.Dines.Where(d => d.IsPaid == false && d.IsOnline == false && d.DeskId == CurDesk).Count();
            if (CleanDeskDine == 0)
            {
                var CleanDesk = await db.Desks.FirstOrDefaultAsync(d => d.Id == CurDesk);
                CleanDesk.Status = DeskStatus.StandBy;
                db.SaveChanges();
                await MvcApplication.ws.SendToClient((int)(Session["User"] as RStatus).HotelId, "desk");
            }
            else
            {

            }
            var Dines = await db.Dines
                   .Include(p => p.DineMenus.Select(pp => pp.Remarks))
                   .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                   .Where(order => order.IsPaid == false && order.IsOnline == false)
                   .Select(d => new
                   {
                       d.Discount,
                       d.DiscountName,
                       d.Id,
                       d.DineMenus,
                       Menu = d.DineMenus.Select(dd => new { dd.Menu, dd.Menu.MenuPrice }),
                       d.BeginTime,
                       d.DeskId,
                       d.Remarks,
                       d.HeadCount,
                       d.UserId,
                       d.OriPrice,
                       d.Price
                   }).ToListAsync();
            return Json(new { Status = true, Dines = Dines });
        }
        /// <summary>
        /// 获取开桌信息
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetOpenElements()
        {
            JsonSerializerSettings setting = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            var Discounts = await db.TimeDiscounts.ToListAsync();
            var menus = await db.Menus
                .Where(m => m.Status == MenuStatus.Normal && m.Usable == true)
                .Include(m => m.Classes)
                .Include(m => m.Remarks)
                .Include(m => m.MenuPrice)
                .Select(m => new { m.Id, m.Name, m.Code, m.MinOrderCount, m.PicturePath, m.Remarks, m.MenuPrice, m.Classes, m.EnglishName, m.NameAbbr })
                .ToListAsync();
            var specials = await db.MenuOnSales.ToListAsync();
            foreach (var i in menus)
            {
                foreach (var j in specials)
                {
                    if (j.OnSaleWeek == DateTime.Now.DayOfWeek && j.Id == i.Id)
                    {
                        i.MenuPrice.Price = j.Price;
                        i.MenuPrice.ExcludePayDiscount = true;
                    }
                }
            }
            var Classes = await db.MenuClasses.Where(m => m.Usable == true && m.IsShow == true).ToListAsync();
            return JsonConvert.SerializeObject(new { Menus = menus, Discounts = Discounts, Classes = Classes }, setting);
        }
        /// <summary>
        /// 开桌时用户登录
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<JsonResult> OpenLogin(string username, string password)
        {
            string PassHash = Models.Method.GetMd5(password);
            var sys = new YummyOnlineContext();
            var login = sys.Users.Where(c => c.PhoneNumber == username && c.PasswordHash == PassHash);
            if (login.Count() == 0) return Json(new { Status = false });
            var lg = await login.FirstOrDefaultAsync();
            string Id = lg.Id;
            var us = db.Customers.Where(c => c.Id == Id).Include(p => p.VipLevel.VipDiscount);
            var data = await us.FirstOrDefaultAsync();
            return Json(new { Status = true, data = data });
        }
        /// <summary>
        /// 开桌
        /// </summary>
        /// <param name="OrderInfo">用户点单信息</param>
        /// <param name="UserId">用户</param>
        /// <param name="OpenDiscount">折扣信息</param>
        /// <returns></returns>
        public async Task<JsonResult> OpenDesk(OpenInfo OrderInfo, string UserId, OpenDiscount OpenDiscount)
        {
            if (OpenDiscount.Discount > 100 || OpenDiscount.Discount <= 0) { return Json(new { Succeeded = false, ErrorMessage = "别逗了，我哪来那么多钱" }); }
            int HotelId = (int)(Session["User"] as RStatus).HotelId;
            string ClerkId = (Session["User"] as RStatus).ClerkId;
            string Token = await sysdb.SystemConfigs.Select(s => s.Token).FirstOrDefaultAsync();
            var ht = new HotelContext(Session["ConnectString"] as string);
            int PayKindId = await ht.PayKinds
                                .Where(p => p.Usable == true && p.Type == PayKindType.Cash)
                                .Select(p => p.Id)
                                .FirstOrDefaultAsync();
            string DiscountName = null;
            float? Discount = null;
            if (OpenDiscount.IsSet != null)
            {
                //自定义打折方案
                Discount = (float)OpenDiscount.Discount / 100;
                DiscountName = "自定义";
            }
            var openUrl = sysdb.SystemConfigs.FirstOrDefault()?.OrderSystemUrl;
            var result = await Method.postHttp(openUrl + "/Payment/ManagerPay",
                new
                {
                    Cart = new
                    {
                        HeadCount = OrderInfo.HeadCount,
                        Price = OrderInfo.Price,
                        PriceInPoints = 0,
                        Invoice = "",
                        DeskId = OrderInfo.Desk.Id,
                        PayKind = new { Id = PayKindId },
                        OrderedMenus = OrderInfo.OrderedMenus
                    },
                    CartAddition = new
                    {
                        Token = Token,
                        HotelId = HotelId,
                        WaiterId = ClerkId,
                        UserId = UserId,
                        Discount = Discount,
                        DiscountName = DiscountName,
                        GiftMenus = OrderInfo.SendMenus
                    }
                });
            PostData pd = JsonConvert.DeserializeObject<PostData>(result);
            if (pd.Succeeded)
            {
                try
                {
                    var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                    if (isprint) MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, pd.Data, new List<int>(), new List<PrintType>() { PrintType.Recipt, PrintType.KitchenOrder, PrintType.ServeOrder }));
                }
                catch
                {

                }
            }
            return Json(result);
        }
        /// <summary>
        /// 退菜所需要的内容
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> getReturn()
        {
            var Desk = await db.Desks.Where(d => d.Usable).OrderBy(d => d.Order).Select(d => new { d.Id, d.Name, d.Status }).ToListAsync();
            var UnpaidDines = await db.Dines
                    .Include(p => p.DineMenus.Select(pp => pp.Remarks))
                    .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                    .Where(order => order.IsPaid == false && order.IsOnline == false)
                    .Select(d => new
                    {
                        d.Discount,
                        d.DiscountName,
                        d.Id,
                        DineMenus = d.DineMenus.Select(dd => new
                        {
                            dd.Id,
                            dd.Menu.Name,
                            dd.OriPrice,
                            dd.Price,
                            dd.RemarkPrice,
                            dd.Count,
                            dd.Status,
                            dd.Type,
                            Remarks = dd.Remarks
                        }),
                        Menu = d.DineMenus.Select(dd => new { dd.Menu, dd.Menu.MenuPrice }),
                        d.BeginTime,
                        d.DeskId,
                        d.Remarks,
                        d.HeadCount,
                        d.UserId,
                        d.OriPrice,
                        d.Price
                    }).ToListAsync();
            var Reasons = await db.ReturnedReasons.ToListAsync();
            return Json(new { Desks = Desk, Dines = UnpaidDines, Reasons = Reasons });
        }
        /// <summary>
        /// 退单
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> ReturnDine(string DineId)
        {
            var DltDine = await db.Dines.FirstOrDefaultAsync(d => d.Id == DineId && d.IsPaid == false);
            string DeskId = DltDine.DeskId;
            var Menus = await db.DineMenus.Where(d => d.DineId == DineId).ToListAsync();
            foreach (var menu in Menus)
            {
                db.DineMenus.Remove(menu);
                db.SaveChanges();
            }
            db.Dines.Remove(DltDine);
            db.SaveChanges();
            var HtManager = new HotelManager(ConnectingStr);
            HtManager.ManageLog($"HotelId: {(Session["User"] as RStatus).HotelId.ToString()},DineId:{DltDine.Id} Returned");
            var desks = await db.Dines.Where(d => d.DeskId == DeskId && d.IsPaid == false && d.IsOnline == false).ToListAsync();
            if (desks.Count() == 0)
            {
                var clean = await db.Desks.FirstOrDefaultAsync(d => d.Id == DeskId);
                clean.Status = DeskStatus.StandBy;
                db.SaveChanges();
            }
            return null;
        }
        /// <summary>
        /// 退菜
        /// </summary>
        /// <param name="DineId"></param>
        /// <param name="MenuId"></param>
        /// <returns></returns>
        public async Task<JsonResult> ReturnMenu(string DineId, int Id, string Reason)
        {
            var HtManager = new HotelManager(ConnectingStr);
            string ClerkId = (Session["User"] as RStatus).ClerkId;
            if (ClerkId == "") return Json(new ErrorState("服务员不正确"));
            var Dine = db.Dines.Where(d => d.Id == DineId);
            var Menu = db.DineMenus.Where(m => m.Id == Id && m.Status == DineMenuStatus.Normal);
            if (Dine.Count() == 0 || Menu.Count() == 0) return Json(new ErrorState("点单或菜品数量不正确"));
            var dn = await Dine.FirstOrDefaultAsync();
            var mn = await Menu.FirstOrDefaultAsync();
            var RMenu = db.DineMenus.Where(m => m.Id == Id && m.Status == DineMenuStatus.Returned);
            var AdReMenu = new DineMenu();
            dn.OriPrice -= mn.OriPrice;
            dn.Price -= mn.Price;
            if (RMenu.Count() == 0)
            {
                AdReMenu.Dine = mn.Dine;
                AdReMenu.DineId = mn.DineId;
                AdReMenu.Menu = mn.Menu;
                AdReMenu.MenuId = mn.MenuId;
                AdReMenu.OriPrice = mn.OriPrice;
                AdReMenu.Price = mn.Price;
                AdReMenu.Remarks = mn.Remarks;
                AdReMenu.Count = 1;
                AdReMenu.Status = DineMenuStatus.Returned;
                AdReMenu.RemarkPrice = 0;
                AdReMenu.ReturnedWaiterId = ClerkId;
                AdReMenu.ReturnedReason = Reason;
                db.DineMenus.Add(AdReMenu);
            }
            else
            {
                var rmenu = await RMenu.FirstOrDefaultAsync();
                rmenu.Count++;
                rmenu.ReturnedWaiterId = ClerkId;
            }
            if (mn.Count == 1)
            {
                db.DineMenus.Remove(mn);
                dn.OriPrice -= mn.RemarkPrice;
                dn.Price -= mn.RemarkPrice;
                AdReMenu.RemarkPrice = mn.RemarkPrice;
                if (dn.Price == 0)
                {
                    dn.IsPaid = true;
                    db.SaveChanges();
                    var countDesk = await db.Dines.Where(d => d.IsPaid == false && d.DeskId == dn.DeskId && d.IsOnline == false).ToListAsync();
                    if (countDesk.Count() == 0)
                    {
                        var desk = await db.Desks.Where(d => d.Id == dn.DeskId).FirstOrDefaultAsync();
                        desk.Status = DeskStatus.StandBy;
                        await MvcApplication.ws.SendToClient((int)(Session["User"] as RStatus).HotelId, "desk");
                    }
                }
            }
            else
            {
                mn.Count--;
            }
            db.SaveChanges();
            HtManager.ManageLog($"HotelId: {(Session["User"] as RStatus).HotelId.ToString()}, DineId:{DineId} Id:{Id}", HotelDAO.Models.Log.LogLevel.Warning, $"ClerkId: {ClerkId}");
            try
            {
                var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                if (isprint && dn.Status == DineStatus.Printed) MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, DineId, new List<int>() { Id }, new List<PrintType>() { PrintType.KitchenOrder }));
            }
            catch
            {

            }
            var Desk = await db.Desks.Where(d => d.Usable).Select(d => new { d.Id, d.Name, d.Status }).ToListAsync();
            var UnpaidDines = await db.Dines
                    .Include(p => p.DineMenus.Select(pp => pp.Remarks))
                    .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                    .Where(order => order.IsPaid == false && order.IsOnline == false)
                    .Select(d => new
                    {
                        d.Discount,
                        d.DiscountName,
                        d.Id,
                        DineMenus = d.DineMenus.Select(dd => new
                        {
                            dd.Id,
                            dd.Menu.Name,
                            dd.OriPrice,
                            dd.Price,
                            dd.RemarkPrice,
                            dd.Count,
                            dd.Status,
                            dd.Type,
                            Remarks = dd.Remarks
                        }),
                        Menu = d.DineMenus.Select(dd => new { dd.Menu, dd.Menu.MenuPrice }),
                        d.BeginTime,
                        d.DeskId,
                        d.Remarks,
                        d.HeadCount,
                        d.UserId,
                        d.OriPrice,
                        d.Price
                    }).ToListAsync();
            return Json(new { Status = true, Dines = UnpaidDines, Desks = Desk });
        }
        /// <summary>
        /// 获取合单信息
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> GetConbine()
        {
            var Desks = await db.Desks.Where(d => d.Status == DeskStatus.Used && d.Usable == true).OrderBy(d => d.Order).ToListAsync();
            var Dines = await db.Dines.Where(order => order.IsPaid == false && order.IsOnline == false)
                .Select(dine => new { dine.Id, dine.DeskId }).ToListAsync();
            return Json(new { Desks = Desks, Dines = Dines });
        }
        /// <summary>
        /// 合单函数
        /// </summary>
        /// <param name="ConbineDines"></param>
        /// <returns></returns>
        public async Task<JsonResult> ConbineDine(List<ConbineDine> ConbineDines)
        {
            decimal allPrice = 0;
            decimal allOPrice = 0;
            string FirstDineId = ConbineDines[0].Id;
            foreach (var dine in ConbineDines)
            {
                var conbineDine = await db.Dines.FirstOrDefaultAsync(d => d.Id == dine.Id);
                allPrice += conbineDine.Price;
                allOPrice += conbineDine.OriPrice;
                if (conbineDine.Id != FirstDineId)
                {
                    conbineDine.IsPaid = true;
                    conbineDine.Status = DineStatus.Finished;
                    db.SaveChanges();
                    var desk = await db.Dines.Where(m => m.DeskId == conbineDine.DeskId && m.IsPaid == false && m.IsOnline == false).ToListAsync();
                    if (desk.Count() == 0)
                    {
                        var clean = await db.Desks.FirstOrDefaultAsync(d => d.Usable == true && d.Id == conbineDine.DeskId);
                        clean.Status = DeskStatus.StandBy;
                        await MvcApplication.ws.SendToClient((int)(Session["User"] as RStatus).HotelId, "desk");
                    }
                    conbineDine.Price = 0;
                    conbineDine.OriPrice = 0;
                }
                var conbineMenus = await db.DineMenus.Where(d => d.DineId == dine.Id).ToListAsync();
                foreach (var menu in conbineMenus)
                {
                    menu.DineId = FirstDineId;
                }

            }
            var targetDine = await db.Dines.FirstOrDefaultAsync(d => d.Id == FirstDineId);
            targetDine.Price = allPrice;
            targetDine.OriPrice = allOPrice;
            db.SaveChanges();
            var Desks = await db.Desks.Where(d => d.Status == DeskStatus.Used && d.Usable == true).OrderBy(d => d.Order).ToListAsync();
            var Dines = await db.Dines.Where(d => d.IsOnline == false && d.IsPaid == false).Select(dine => new { dine.Id, dine.DeskId }).ToListAsync();
            return Json(new { Desks = Desks, Dines = Dines });
        }
        /// <summary>
        /// 换桌信息
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> getReplace()
        {
            var Desks = await db.Desks
                .Where(d => d.Status == DeskStatus.Used && d.Usable == true)
                .OrderBy(d => d.Order).ToListAsync();
            var Dines = await db.Dines.Where(d => d.IsOnline == false && d.IsPaid == false)
                .Select(dine => new { dine.Id, dine.DeskId }).ToListAsync();
            var TotalDesk = await db.Desks.Where(d => d.Usable == true).ToListAsync();
            return Json(new { Dines = Dines, Desks = Desks, TotalDesk = TotalDesk });
        }
        /// <summary>
        /// 换桌
        /// </summary>
        /// <param name="DineId"></param>
        /// <param name="DeskId"></param>
        /// <returns></returns>
        public async Task<JsonResult> ChangeDesk(string DineId, string DeskId)
        {
            var curDine = await db.Dines.FirstOrDefaultAsync(d => d.Id == DineId && d.IsPaid == false);
            if (curDine != null)
            {
                string OriDeskId = curDine.DeskId;
                if (curDine.DeskId == DeskId) { return Json(new { Status = false, Message = "别逗了，相同桌台不需要更改" }); }
                else
                {
                    var TargetDesk = await db.Desks.FirstOrDefaultAsync(d => d.Id == DeskId);
                    TargetDesk.Status = DeskStatus.Used;
                    curDine.DeskId = DeskId;
                    db.SaveChanges();
                    var Clean = await db.Dines.Where(d => d.DeskId == OriDeskId && d.IsOnline == false && d.IsPaid == false).ToListAsync();
                    if (Clean.Count() == 0)
                    {
                        var DeskClean = await db.Desks.FirstOrDefaultAsync(d => d.Id == OriDeskId && d.Usable == true);
                        DeskClean.Status = DeskStatus.StandBy;
                    }
                }
            }
            else
            {
                return Json(new { Status = false, Message = "根本没有这单，换毛啊" });
            }
            db.SaveChanges();
            var Desks = await db.Desks
                .Where(d => d.Status == DeskStatus.Used && d.Usable == true).ToListAsync();
            var Dines = await db.Dines.Where(d => d.IsOnline == false && d.IsPaid == false)
                .Select(dine => new { dine.Id, dine.DeskId }).ToListAsync();
            var TotalDesk = await db.Desks.Where(d => d.Usable == true).ToListAsync();
            return Json(new { Status = true, Dines = Dines, Desks = Desks, TotalDesk = TotalDesk });
        }

        public async Task<JsonResult> GetHandOut()
        {
            var dines = await db.Dines.Where(d => d.IsPaid == true && d.Status != DineStatus.Shifted).Select(d => d.Id).ToListAsync();
            var pays = await db.DinePaidDetails
                .Where(d => dines.Contains(d.DineId))
                .GroupBy(d => d.PayKindId)
                .Select(g => new
                {
                    Id = g.Key,
                    Total = g.Sum(gg => gg.Price)
                }).ToListAsync();
            var isPoint = await db.HotelConfigs.Select(h => h.PointsRatio).FirstOrDefaultAsync();
            var PayKinds = new List<PayKind>();
            if (isPoint != 0)
            {
                PayKinds = await db.PayKinds.Where(d => d.Usable == true && d.Type != PayKindType.Other).ToListAsync();
            }
            else
            {
                PayKinds = await db.PayKinds.Where(d => d.Usable == true && (d.Type == PayKindType.Cash || d.Type == PayKindType.Offline || d.Type == PayKindType.Online)).ToListAsync();
            }
            return Json(new { PayList = pays, PayKinds = PayKinds });
        }
        /// <summary>
        /// 获取交接班次数
        /// </summary>
        /// <param name="Time">日期</param>
        /// <returns></returns>
        public async Task<JsonResult> GetNumbers(string Time)
        {
            DateTime Date;
            if (Time == null)
            {
                Date = DateTime.Now;
            }
            else
            {
                Date = Convert.ToDateTime(Time);
            }
            var Numbers = await db.Shifts.Where(d => SqlFunctions.DateDiff("day", d.DateTime, Date) == 0)
                .GroupBy(d => d.Id)
                .Select(d => new
                {
                    Id = d.Key
                })
                .ToListAsync();
            return Json(new { Numbers = Numbers });
        }

        /// <summary>
        /// 补打印 交接班
        /// </summary>
        /// <param name="Time">时间</param>
        /// <param name="frequencies">班次</param>
        /// <returns></returns>
        public JsonResult RePrintCheck(string Time, List<int> frequencies)
        {
            DateTime Date;
            if (Time == null)
            {
                Date = DateTime.Now;
            }
            else
            {
                Date = Convert.ToDateTime(Time);
            }
            MvcApplication.client.Send(new RequestPrintShiftsProtocol((int)(Session["User"] as RStatus).HotelId, frequencies, Date));
            return null;
        }
        /// <summary>
        /// 交接
        /// </summary>
        /// <param name="Profit">交接</param>
        /// <returns></returns>
        public async Task<JsonResult> CheckOut(List<Profit> Profit)
        {
            int Id = 1;
            var Now = DateTime.Now;
            var dines = await db.Dines.Where(d => d.Status != DineStatus.Shifted)
                .ToListAsync();
            var DineIds = dines.Select(d => d.Id);
            var PayList = await db.DinePaidDetails.Where(d => DineIds.Contains(d.DineId))
                .GroupBy(d => d.PayKindId)
                .Select(d => new
                {
                    PayKindId = d.Key,
                    PayTotal = d.Sum(dd => dd.Price)
                })
                .ToListAsync();
            if (Profit != null)
            {
                var Day = db.Shifts.Where(d => SqlFunctions.DateDiff("day", d.DateTime, DateTime.Now) == 0).ToList();
                if (Day.Count != 0)
                {
                    Id = Day.Max(d => d.Id) + 1;
                }
                foreach (var pft in Profit)
                {
                    db.Shifts.Add(new Shift
                    {
                        DateTime = Now,
                        PayKindId = pft.Id,
                        ReceivablePrice = PayList.Where(p => p.PayKindId == pft.Id).Select(p => p.PayTotal).FirstOrDefault(),
                        RealPrice = pft.Num,
                        Id = Id
                    });
                }
            }
            foreach (var dine in dines)
            {
                dine.Status = DineStatus.Shifted;
                db.SaveChanges();
            }
            var Desks = await db.Desks.Where(d => d.Usable == true).ToListAsync();
            foreach (var i in Desks)
            {
                i.Status = DeskStatus.StandBy;
                await db.SaveChangesAsync();
            }
            MvcApplication.client.Send(new RequestPrintShiftsProtocol((int)(Session["User"] as RStatus).HotelId, new List<int>() { Id }, DateTime.Now));
            return Json(new SuccessState());
        }
        /// <summary>
        /// 获取加菜信息
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> GetAddMenuEle()
        {
            var UnpaidDines = await db.Dines
                  .Include(p => p.DineMenus.Select(pp => pp.Remarks))
                  .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                  .Where(order => order.IsPaid == false && order.IsOnline == false)
                  .Select(d => new
                  {
                      d.Discount,
                      d.DiscountName,
                      d.Id,
                      DineMenus = d.DineMenus.Select(dd => new
                      {
                          Count = dd.Count,
                          DineId = dd.DineId,
                          Id = dd.Id,
                          Menu = dd.Menu,
                          MenuId = dd.MenuId,
                          OriPrice = dd.OriPrice,
                          Price = dd.Price,
                          RemarkPrice = dd.RemarkPrice,
                          Remarks = dd.Remarks,
                          ReturnedWaiterId = dd.ReturnedWaiterId,
                          Status = dd.Status
                      }),
                      Menu = d.DineMenus.Select(dd => new { dd.Menu, dd.Menu.MenuPrice }),
                      d.BeginTime,
                      d.DeskId,
                      Desk = db.Desks.Where(dd => dd.Id == d.DeskId).FirstOrDefault(),
                      d.HeadCount,
                      d.UserId,
                      d.OriPrice,
                      d.Price
                  }).OrderBy(d => d.Desk.Order).ToListAsync();
            var menus = await db.Menus
                .Where(m => m.Status == MenuStatus.Normal && m.Usable == true)
                .Include(m => m.Remarks)
                .Include(m => m.MenuPrice)
                .Select(m => new { m.Id, m.Name, m.Code, m.MinOrderCount, m.Remarks, m.MenuPrice })
                .ToListAsync();
            return Json(new { UnpaidDines = UnpaidDines, Menus = menus });
        }
        /// <summary>
        /// 补打印
        /// </summary>
        /// <param name="DineId"></param>
        /// <param name="Type"></param>
        /// <returns></returns>
        public async Task<JsonResult> RePrint(string DineId, int Type)
        {
            try
            {
                var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                if (isprint)
                {
                    if (Type == (int)ManagePrintType.Recipt)
                    {
                        MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, DineId, new List<int>(), new List<PrintType>() { PrintType.Recipt }));

                    }
                    else if (Type == (int)ManagePrintType.Kitchen)
                    {
                        MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, DineId, new List<int>(), new List<PrintType>() { PrintType.KitchenOrder, PrintType.ServeOrder }));
                    }
                }

            }
            catch
            {

            }
            return null;
        }


        /// <summary>
        /// 加菜
        /// </summary>
        /// <param name="Menus"></param>
        /// <returns></returns>
        public async Task<JsonResult> AddDineMenu(AddDineMenu Menus)
        {
            if (Menus.DineId == null) { return Json(new { Status = false, ErrorMessage = "未找到该订单" }); }
            var dine = await db.Dines.Where(d => d.Id == Menus.DineId).FirstOrDefaultAsync();
            if (dine.IsPaid == true) { return Json(new { Status = false, ErrorMessage = "不允许对已支付订单操作" }); }
            var menus = new List<int>();
            if (Menus.Menus != null)
            {
                foreach (var i in Menus.Menus)
                {
                    var menu = await db.Menus.Where(m => m.Id == i.Id)
                        .Include(m => m.MenuPrice)
                        .FirstOrDefaultAsync();
                    if (menu == null) { return Json(new { Status = false, ErrorMessage = "菜品有误，请检查数据" }); }
                    dine.OriPrice += menu.MenuPrice.Price * i.Num;
                    var temp = new DineMenu();
                    temp.DineId = dine.Id;
                    temp.MenuId = menu.Id;
                    temp.Count = i.Num;
                    temp.OriPrice = menu.MenuPrice.Price;
                    if (i.IsSend)
                    {
                        temp.Price = 0;
                        temp.RemarkPrice = 0;
                        temp.Type = DineMenuType.Gift;
                    }
                    else
                    {
                        temp.Status = DineMenuStatus.Normal;
                        if (menu.MenuPrice.ExcludePayDiscount)
                        {
                            //undiscount
                            temp.Price = menu.MenuPrice.Price;
                        }
                        else
                        {
                            temp.Price = menu.MenuPrice.Price * (decimal)dine.Discount;
                        }
                        var serMenu = await db.MenuOnSales.Where(d => d.Id == menu.Id).FirstOrDefaultAsync();
                        var SetMeal = await db.MenuSetMeals.Where(d => d.MenuSetId == menu.Id).FirstOrDefaultAsync();
                        if (dine.DiscountType == DiscountType.None)
                        {
                            if (SetMeal != null)
                            {
                                temp.Type = DineMenuType.SetMeal;
                            }
                            else if (serMenu != null)
                            {
                                temp.Type = DineMenuType.OnSale;
                            }
                            else
                            {
                                temp.Type = DineMenuType.None;
                            }
                        }
                        else
                        {
                            if (dine.DiscountType == DiscountType.PayKind)
                            {
                                temp.Type = DineMenuType.PayKindDiscount;
                            }
                            else if (dine.DiscountType == DiscountType.Custom)
                            {
                                temp.Type = DineMenuType.CustomDiscount;
                            }
                            else if (dine.DiscountType == DiscountType.Time)
                            {
                                temp.Type = DineMenuType.TimeDiscount;
                            }
                            else if (dine.DiscountType == DiscountType.Vip)
                            {
                                temp.Type = DineMenuType.VipDiscount;
                            }
                        }

                        if (i.Remarks != null)
                        {
                            temp.RemarkPrice = await db.Remarks
                                .Where(r => i.Remarks.Contains(r.Id))
                                .GroupBy(r => r.Id).Select(g => g.Sum(gg => gg.Price)).FirstOrDefaultAsync();
                        }
                        else
                        {
                            temp.RemarkPrice = 0;
                        }
                        dine.Price += temp.RemarkPrice + temp.Price * i.Num;
                    }
                    db.DineMenus.Add(temp);
                    db.SaveChanges();
                    menus.Add(temp.Id);
                    if (i.Remarks != null)
                    {
                        var menuRemark = await db.DineMenus.Where(d => d.Id == temp.Id)
                               .Include(d => d.Remarks)
                               .FirstOrDefaultAsync();
                        foreach (var j in i.Remarks)
                        {
                            var remark = await db.Remarks.FirstOrDefaultAsync(r => r.Id == j);
                            if (remark == null) { return Json(new { Status = false, ErrorMessage = "备注有误，请检查数据" }); }
                            menuRemark.Remarks.Add(remark);
                            db.SaveChanges();
                        }
                    }
                }
            }
            try
            {
                var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                if (isprint) MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId
                    , dine.Id, menus
                    , new List<PrintType>() { PrintType.KitchenOrder, PrintType.ServeOrder, PrintType.Recipt }));
            }
            catch
            {

            }
            var OriPrice = await db.Dines.Where(d => d.Id == dine.Id).Select(d => d.OriPrice).FirstOrDefaultAsync();
            var Price = await db.Dines.Where(d => d.Id == dine.Id).Select(d => d.Price).FirstOrDefaultAsync();
            return Json(new { Status = true, OriPrice = OriPrice, Price = Price });
        }
        /// <summary>
        /// 修改退菜信息
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="Reason"></param>
        /// <returns></returns>
        public async Task<JsonResult> EditReason(int Id, string Reason)
        {
            var reason = await db.ReturnedReasons.Where(r => r.Id == Id).FirstOrDefaultAsync();
            reason.Description = Reason;
            db.SaveChanges();
            return Json(new SuccessState());
        }

        /// <summary>
        /// 增加退菜信息
        /// </summary>
        /// <param name="Reason"></param>
        /// <returns></returns>
        public async Task<JsonResult> AddReason(string Reason)
        {
            var reason = new ReturnedReason
            {
                Description = Reason
            };
            db.ReturnedReasons.Add(reason);
            await db.SaveChangesAsync();
            return Json(new { Status = new SuccessState(), Id = reason.Id });
        }

        /// <summary>
        /// 删除退菜原因信息
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public async Task<JsonResult> DeleteReason(int Id)
        {
            var reason = await db.ReturnedReasons.Where(r => r.Id == Id).FirstOrDefaultAsync();
            db.ReturnedReasons.Remove(reason);
            db.SaveChanges();
            return Json(new SuccessState());
        }

        /// <summary>
        /// 获取补打印信息
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> getRePrinter()
        {
            var UnShiftDine = await db.Dines
                    .Include(p => p.DineMenus.Select(pp => pp.Remarks))
                    .Include(p => p.DineMenus.Select(pp => pp.Menu.MenuPrice))
                    .Where(d => d.Status != DineStatus.Shifted)
                    .Select(d => new
                    {
                        d.Discount,
                        d.DiscountName,
                        d.Id,
                        DineMenus = d.DineMenus.Select(dd => new
                        {
                            Count = dd.Count,
                            DineId = dd.DineId,
                            Id = dd.Id,
                            Menu = dd.Menu,
                            MenuId = dd.MenuId,
                            OriPrice = dd.OriPrice,
                            Price = dd.Price,
                            RemarkPrice = dd.RemarkPrice,
                            Remarks = dd.Remarks,
                            ReturnedWaiterId = dd.ReturnedWaiterId,
                            Status = dd.Status
                        }),
                        Menu = d.DineMenus.Select(dd => new { dd.Menu, dd.Menu.MenuPrice }),
                        d.DeskId,
                        d.UserId,
                        d.OriPrice,
                        d.Price,
                        d.Status,
                        d.IsPaid
                    })
                    .ToListAsync();
            return Json(new { UnShiftDine = UnShiftDine });
        }
        /// <summary>
        /// 补打印订单
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public async Task<JsonResult> RePrintDine(string Id)
        {

            var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
            if (isprint)
            {
                MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, Id, new List<int>(), new List<PrintType>() { PrintType.Recipt }));
            }
            return null;
        }
        /// <summary>
        /// 智能获取地址
        /// </summary>
        /// <param name="Phone"></param>
        /// <returns></returns>
        public async Task<JsonResult> SmartChoose(string Phone)
        {
            var User = await sysdb.Users
                .Include(u => u.UserAddresses)
                .Where(u => u.PhoneNumber == Phone)
                .ToListAsync();
            if (User == null)
            {
                var NUser = new YummyOnlineDAO.Models.User()
                {
                    PhoneNumber = Phone
                };
                sysdb.Users.Add(NUser);
                await db.SaveChangesAsync();
                return Json(new SuccessState(NUser));
            }
            else
            {
                return Json(new SuccessState(User));
            }
        }
        /// <summary>
        /// 删除用户地址
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="Address"></param>
        /// <returns></returns>
        public async Task<JsonResult> DeleteAddress(string UserId, string Address)
        {
            var Addresses = await sysdb.UserAddresses.Where(d => d.UserId == UserId && d.Address == Address).FirstOrDefaultAsync();
            sysdb.UserAddresses.Remove(Addresses);
            await sysdb.SaveChangesAsync();
            var User = await sysdb.Users
               .Include(u => u.UserAddresses)
               .Where(u => u.Id == UserId)
               .ToListAsync();
            return Json(new SuccessState(User));
        }

        public async Task<JsonResult> OpenReserve(OpenInfo OrderInfo, OpenDiscount OpenDiscount, string Address, string ShiftNum, string Phone)
        {
            if (OpenDiscount.Discount > 100 || OpenDiscount.Discount <= 0) { return Json(new { Succeeded = false, ErrorMessage = "别逗了，我哪来那么多钱" }); }
            int HotelId = (int)(Session["User"] as RStatus).HotelId;
            string ClerkId = (Session["User"] as RStatus).ClerkId;
            string Token = await sysdb.SystemConfigs.Select(s => s.Token).FirstOrDefaultAsync();
            var ht = new HotelContext(Session["ConnectString"] as string);
            int PayKindId = await ht.PayKinds
                                .Where(p => p.Usable == true && p.Type == PayKindType.Cash)
                                .Select(p => p.Id)
                                .FirstOrDefaultAsync();
            string DiscountName = null;
            float? Discount = null;
            if (OpenDiscount.IsSet != null)
            {
                //自定义打折方案
                Discount = (float)OpenDiscount.Discount / 100;
                DiscountName = "自定义";
            }
            var User = await sysdb.Users.Where(d => d.PhoneNumber == Phone).FirstOrDefaultAsync();
            if (User == null)
            {
                User = new User()
                {
                    PhoneNumber = Phone
                };
                sysdb.Users.Add(User);
                await sysdb.SaveChangesAsync();
            }
            var address = await sysdb.UserAddresses.Where(d => d.UserId == User.Id && d.Address == Address).FirstOrDefaultAsync();
            if (address == null)
            {
                sysdb.UserAddresses.Add(new UserAddress()
                {
                    UserId = User.Id,
                    Address = Address
                });
                await sysdb.SaveChangesAsync();
            }
            var openUrl = sysdb.SystemConfigs.FirstOrDefault()?.OrderSystemUrl;
            var result = await Method.postHttp(openUrl + "/Payment/ManagerPay",
                new
                {
                    Cart = new
                    {
                        HeadCount = OrderInfo.HeadCount,
                        Price = OrderInfo.Price,
                        PriceInPoints = 0,
                        Invoice = "",
                        DeskId = OrderInfo.Desk.Id,
                        PayKind = new { Id = PayKindId },
                        OrderedMenus = OrderInfo.OrderedMenus
                    },
                    CartAddition = new
                    {
                        Token = Token,
                        HotelId = HotelId,
                        WaiterId = ClerkId,
                        UserId = User.Id,
                        Discount = Discount,
                        DiscountName = DiscountName,
                        GiftMenus = OrderInfo.SendMenus,
                        DineType = 1
                    }
                });
            PostData pd = JsonConvert.DeserializeObject<PostData>(result);
            if (pd.Succeeded)
            {
                db.TakeOuts.Add(new TakeOut()
                {
                    DineId = pd.Data,
                    Address = Address,
                    RecordId = ShiftNum
                });
                await db.SaveChangesAsync();
                try
                {
                    var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                    if (isprint) MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, pd.Data, new List<int>(), new List<PrintType>() { PrintType.Recipt, PrintType.KitchenOrder, PrintType.ServeOrder }));
                }
                catch
                {

                }
            }
            return Json(new SuccessState());
        }

        /// <summary>
        /// 获取外卖统一支付的信息
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> getSpePayEle()
        {
            var HotelManager = new HotelManager(ConnectingStr);
            var Desks = await HotelManager.GetTakeOutDeskes();
            var DeskIds = Desks.Select(d => d.Id);
            List<Dine> Dines = new List<Dine>();
            if (DeskIds.Count() > 0)
            {

                Dines = await db.Dines
                    .Include(d => d.DineMenus.Select(dd => dd.Menu.MenuPrice))
                    .Include(d => d.DineMenus.Select(dd => dd.Remarks))
                    .Include(d => d.TakeOut)
                    .Where(d => DeskIds.Contains(d.DeskId) && d.IsPaid == false).ToListAsync();
            }
            var PayKinds = await HotelManager.GetOfflinePayKinds();
            return Json(new SuccessState(new { Dines = Dines, Desks = Desks, PayKinds = PayKinds }));
        }


        public async Task<JsonResult> SpecialPay(decimal Price, List<string> DineIds, int Id)
        {
            var HotelManager = new HotelManager(ConnectingStr);
            var UnPaidDines = await db.Dines.Where(d => DineIds.Contains(d.Id)).ToListAsync();
            decimal priceAll = 0;
            var myDesks = await db.Desks.Where(d => d.Usable == true).ToListAsync();
            var myDines = await db.Dines.Where(d => d.IsPaid == false && d.IsOnline == false).ToListAsync();
            if (UnPaidDines == null || UnPaidDines.Count == 0)
            {
                return Json(new ErrorState("未选择订单"));
            }
            else
            {
                foreach (var i in UnPaidDines)
                {
                    priceAll += i.Price;
                }
            }
            if (priceAll > Price)
            {
                return Json(new ErrorState("付款金额不够"));
            }
            else
            {
                var isprint = await db.HotelConfigs.Select(h => h.HasAutoPrinter).FirstOrDefaultAsync();
                for (var i = 0; i < UnPaidDines.Count(); i++)
                {
                    db.DinePaidDetails.Add(new DinePaidDetail
                    {
                        DineId = UnPaidDines[i].Id,
                        PayKindId = Id,
                        Price = UnPaidDines[i].Price
                    });

                    UnPaidDines[i].IsPaid = true;
                    MvcApplication.client.Send(new NewDineInformProtocol((int)(Session["User"] as RStatus).HotelId, UnPaidDines[i].Id, true));
                    await db.SaveChangesAsync();
                    var CurDesk = UnPaidDines[i].DeskId;
                    var CleanDeskDine = myDines.Where(d => d.DeskId == CurDesk && d.IsPaid == false).Count();
                    if (CleanDeskDine == 0)
                    {
                        var CleanDesk = myDesks.FirstOrDefault(d => d.Id == CurDesk);
                        CleanDesk.Status = DeskStatus.StandBy;
                        await db.SaveChangesAsync();
                        await MvcApplication.ws.SendToClient((int)(Session["User"] as RStatus).HotelId, "desk");
                    }
                    try
                    {
                        if (isprint)
                        {
                            if (UnPaidDines[i].Status == DineStatus.Printed)
                            {
                                MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, UnPaidDines[i].Id, new List<int>(), new List<PrintType>() { PrintType.Recipt }));
                            }
                            else
                            {
                                MvcApplication.client.Send(new RequestPrintDineProtocol((int)(Session["User"] as RStatus).HotelId, UnPaidDines[i].Id, new List<int>(), new List<PrintType>() { PrintType.Recipt, PrintType.KitchenOrder, PrintType.ServeOrder }));
                            }
                        }
                    }
                    catch
                    {

                    }
                }

            }
            var Desks = await HotelManager.GetTakeOutDeskes();
            var DeskIds = Desks.Select(d => d.Id);
            List<Dine> Dines = new List<Dine>();
            if (DeskIds.Count() > 0)
            {

                Dines = await db.Dines
                    .Include(d => d.DineMenus.Select(dd => dd.Menu.MenuPrice))
                    .Include(d => d.DineMenus.Select(dd => dd.Remarks))
                    .Include(d => d.TakeOut)
                    .Where(d => DeskIds.Contains(d.DeskId) && d.IsPaid == false).ToListAsync();
            }
            return Json(new SuccessState(new { Dines = Dines, Desks = Desks }));
        }

    }
}