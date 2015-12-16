using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.XPath;
using LCTest.Models;
using Microsoft.Ajax.Utilities;
using NLog;
using WebGrease.Css.Extensions;

namespace LCTest.Controllers
{
    public class HomeController : Controller
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public ActionResult Index()
        {
            ViewBag.Message = "Modify this template to jump-start your ASP.NET MVC application.";
            MassiveOrder.Init();

            return View();
        }

        public ActionResult About(Int32? UserId)
        {
            //根据UserId判断用户是否是有权限用户
            Models.Customer usr = (Models.Customer)Session["customer"];

            if (UserId == null && usr == null)
                return RedirectToAction("Index");
            else if (UserId != null)
            {
                var usrn = MassiveOrder.CheckUser((int)UserId);
                if (usrn == null) return RedirectToAction("Index");
                Session["customer"] = usrn;
                Session["cart"] = new Models.Order { customerId = (int)UserId, Status = OrderStatus.InCart };
                logger.Info("登录者【{0}】", UserId);
            }

            return View(MassiveOrder.ItemStocks);
        }

        public JsonResult Check()
        {
            if (Session["customer"] != null)
            {
                return Json(Session["name"].ToString(), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult AddToCart(Int32? Id)
        {
            Models.Order order = (Models.Order)Session["cart"];
            Models.Customer customer = (Models.Customer)Session["customer"];
            //没有登录
            if (order == null || customer == null) return Json(false, JsonRequestBehavior.AllowGet);
            var item = MassiveOrder.ItemStocks.Where(r => r.Id == Id).FirstOrDefault();
            if (item==null)
                return Json(false, JsonRequestBehavior.AllowGet);
            //购物车重复购买
            
            if (order.Items.Where(r => r.ClassId == item.ClassId).Count() > 0)
                return Json(false, JsonRequestBehavior.AllowGet);
            //库存和历史订单判断是否可以购买
            if (!MassiveOrder.AllowBuy(customer.Id,item))
                return Json(false, JsonRequestBehavior.AllowGet);

            //加入购物车
            order.Items.Add(new OrderDetail
            {
                Id = 0,
                OrderId = order.Id,
                ItemId = item.Id,
                Amount = 1,
                Name = item.Name,
                ClassId = item.ClassId
            });
            Session["cart"] = order;
            logger.Info("添加购物车【{0}】：【{1}】", customer.Id, item.Id);
            return Json(true, JsonRequestBehavior.AllowGet);
        }

        public JsonResult Order()
        {
            Models.Order order = (Models.Order)Session["cart"];
            Models.Customer customer = (Models.Customer)Session["customer"];

            //没有登录
            if (customer == null)
            {
                logger.Info("收到无session用户");
                return Json("未登录", JsonRequestBehavior.AllowGet);
            }

            if (order == null)
            {
                logger.Info("登录者【{0}】订单Session丢失", customer.Id);
                return Json("订单session丢失", JsonRequestBehavior.AllowGet);
            }
            if (!order.Items.Any())
            {
                logger.Info("登录者【{0}】未购物", customer.Id);
                return Json("未购物", JsonRequestBehavior.AllowGet);
            }

            logger.Info("下单【{0}】", customer.Id);

            //生成订单
            string Msg;
            MakeOrderResult result = MassiveOrder.MakeOrder(customer, order, out Msg);
            if (result == MakeOrderResult.OK)
            {
                //清空购物车
                Session["cart"] = new Models.Order { customerId = customer.Id, Status = OrderStatus.InCart };
                Msg = "下单完成，请前往我的订单确认订单已经产生";
            }
            else if (result == MakeOrderResult.LockedTimeOut)
            {
                Msg = "抢购人过多，请稍后再试";
            }
            else
            {
                Msg = String.Format("[{0}]已经被抢完。", Msg);
            }

            return Json(Msg, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Contact()
        {

            ViewBag.UnSaved = MassiveOrder.Orders.Count(o => o.Status == OrderStatus.Submited);
            ViewBag.Saved = MassiveOrder.Orders.Count(o => o.Status == OrderStatus.Saved);
            ViewBag.InCart = MassiveOrder.Orders.Count(o => o.Status == OrderStatus.InCart);
            ViewBag.Total = MassiveOrder.Orders.Sum(o => o.Items.Count());
            List<ItemStock> iss = MassiveOrder.ItemStocks;
            return View(iss);
        }
    }
}
