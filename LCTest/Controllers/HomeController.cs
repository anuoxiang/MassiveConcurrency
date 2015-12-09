using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;

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
            if (UserId != null)
            {
                string name = MassiveOrder.CheckUser((int) UserId);

            }
            ViewBag.Message = "Your app description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
