using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LCTest.Models
{

    public class OrderDetail
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ItemId { get; set; }
        public int Amount { get; set; }
    }
}