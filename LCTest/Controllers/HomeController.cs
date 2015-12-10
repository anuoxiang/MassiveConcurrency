using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.XPath;
using LCTest.Models;
using Microsoft.Ajax.Utilities;
using WebGrease.Css.Extensions;

namespace LCTest.Controllers
{
    public class HomeController : Controller
    {
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
            //重复购买
            if (order.Items.Where(r => r.ClassId == item.ClassId).Count() > 0)
                return Json(false, JsonRequestBehavior.AllowGet);
            //历史订单重复购买
            List<OrderDetail> lists = new List<OrderDetail>();
            MassiveOrder.Orders.Where(r => r.customerId == customer.Id)
                .Select(r => r.Items).ForEach(i => lists.AddRange(i));
            if (lists.Where(r => r.ClassId == item.ClassId).Count() > 0)
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
            return Json(true, JsonRequestBehavior.AllowGet);
        }

        public JsonResult Order()
        {
            Models.Order order = (Models.Order)Session["cart"];
            Models.Customer customer = (Models.Customer)Session["customer"];

            //没有登录
            if (order == null || customer == null || order.Items.Count() == 0)
                return Json("未登录或未购物", JsonRequestBehavior.AllowGet);
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
            Models.Customer usr = (Models.Customer)Session["customer"];

            if (usr == null)
                return RedirectToAction("Index");

            List<Order> orders = new List<Order>();
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = "Data Source=(LocalDb)\\v11.0;Initial Catalog=LCTest;Integrated Security=True";
                conn.Open();
                SqlCommand command = new SqlCommand(string.Format("SELECT Id FROM EB_Order WHERE customerId = {0}", usr.Id), conn);
                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        var order = new Order
                        {
                            Id = reader.GetInt32(0),
                            customerId = reader.GetInt32(1)
                        };
                        orders.Add(order);
                    }
                }
            }

            return View(orders);
        }
    }
}
