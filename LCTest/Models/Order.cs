using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LCTest.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int? customerId { get; set; }
        public List<OrderDetail> Items { get; set; }
        public OrderStatus Status { get; set; }

        public Order()
        {
            Items = new List<OrderDetail>();
        }
    }

    public enum OrderStatus
    {
        Normal,
        Saved
    }
}