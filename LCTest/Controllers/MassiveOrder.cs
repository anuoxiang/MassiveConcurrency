using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Web;
using LCTest.Models;
using NLog;


namespace LCTest.Controllers
{
    /// <summary>
    /// 无状态
    /// 静态化
    /// </summary>
    public class MassiveOrder
    {
        /// <summary>
        /// 客户
        /// </summary>
        public static List<Models.Customer> Customers { get; set; }
        /// <summary>
        /// 订单表
        /// </summary>
        public static List<Models.Order> Orders { get; set; }
        /// <summary>
        /// 商品库存
        /// </summary>
        public static List<Models.ItemStock> ItemStocks { get; set; }


        public static readonly SqlConnection conn = new SqlConnection();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static bool IsReady = false;
        #region 一次运行

        /// <summary>
        /// 静态函数初始化
        /// </summary>
        static MassiveOrder()
        {
            Customers = new List<Customer>();
            Orders = new List<Order>();
            ItemStocks = new List<ItemStock>();
            conn.ConnectionString = "Data Source=(LocalDb)\\v11.0;Initial Catalog=LCTest;Integrated Security=True";
            //conn.Open();

        }

        public static void Init()
        {
            if (IsReady) return;
            LoadCustomers();
            LoadItems();
            LoadOrders();
            IsReady = true;
        }
        /// <summary>
        /// 载入客户信息
        /// </summary>
        private static void LoadCustomers()
        {
            logger.Info("载入用户信息");
            SqlCommand command = new SqlCommand("SELECT Id,OpenId,Name FROM CRM_Customer;", conn);
            conn.Open();
            SqlDataReader reader = command.ExecuteReader();
            Monitor.TryEnter(Customers, 2000);
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Customers.Add(new Customer()
                    {
                        Id = reader.GetInt32(0),
                        OpenId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
                }
            }
            Monitor.Exit(Customers);
            reader.Close();
            conn.Close();
            logger.Info("载入完成");
        }

        /// <summary>
        /// 载入已经产生的订单信息
        /// </summary>
        private static void LoadItems()
        {
            logger.Info("载入商品与库存信息");
            SqlCommand command = new SqlCommand("SELECT Id,classId,Stock,Name FROM EB_Item;", conn);
            conn.Open();
            SqlDataReader reader = command.ExecuteReader();
            Monitor.TryEnter(ItemStocks, 2000);
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    ItemStocks.Add(new ItemStock()
                    {
                        Id = reader.GetInt32(0),
                        ClassId = reader.GetInt32(1),
                        Stock = reader.GetInt32(2),
                        Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        IsModify = false
                    });
                }
            }
            Monitor.Exit(ItemStocks);
            reader.Close();
            conn.Close();
            logger.Info("载入完成");
        }

        private static void LoadOrders()
        {
            logger.Info("载入订单信息");
            SqlCommand command = new SqlCommand("SELECT Id,customerId FROM EB_Order;", conn);
            conn.Open();
            SqlDataReader reader = command.ExecuteReader();
            Monitor.TryEnter(Orders, 2000);
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Orders.Add(new Order()
                    {
                        Id = reader.GetInt32(0),
                        customerId = (int)reader.GetInt32(1),
                        Status = OrderStatus.Saved
                    });
                }
            }

            reader.Close();
            command.CommandText = "SELECT Id,OrderId,ItemId,Amount FROM EB_OrderDetail";
            reader = command.ExecuteReader();
            List<OrderDetail> details = new List<OrderDetail>();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    var order = Orders.Where(r => r.Id == reader.GetInt32(1)).First();
                    if (order != null)
                    {
                        var item = ItemStocks.Where(r => r.Id == reader.GetInt32(2)).FirstOrDefault();
                        order.Items.Add(new OrderDetail()
                        {
                            Id = reader.GetInt32(0),
                            OrderId = order.Id,
                            ItemId = reader.GetInt32(2),
                            Amount = reader.GetInt32(3),
                            Name = item.Name,
                            ClassId = item.ClassId
                        });
                    }
                }
            }

            Monitor.Exit(Orders);
            conn.Close();
            logger.Info("载入完成");
        }
        #endregion

        #region 外部接口

        /// <summary>
        /// 验证用户
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public static Customer CheckUser(int Id)
        {
            var u = Customers.Where(r => r.Id == Id).FirstOrDefault();
            if (u != null)
                return u;
            else
                return null;
        }


        /// <summary>
        /// 产生订单
        /// </summary>
        /// <param name="customer"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public static MakeOrderResult MakeOrder(Customer customer, Order order, out string Msg)
        {
            //1，锁定库存
            //2，验证是否可以减去库存
            //3，减去库存
            //4，解锁
            Msg = "OK";
            MakeOrderResult result = MakeOrderResult.OK;
            Monitor.TryEnter(ItemStocks, 2000);
            bool flag = false;
            foreach (var orderDetail in order.Items)
            {
                var item = ItemStocks.Where(r => r.Id == orderDetail.ItemId).FirstOrDefault();
                if (item != null)
                {
                    if (item.Stock > 1)
                    {
                        item.Stock--;
                        item.IsModify = true;
                    }
                    else
                    {
                        Msg = string.Format("{0}已经抢完", item.Name);
                        result = MakeOrderResult.Soldout;
                        break;
                    }
                }
                else
                {
                    Msg = string.Format("{0}已经下架", orderDetail.Name);
                    result = MakeOrderResult.Soldout;
                    break;
                }
            }
            Monitor.Exit(ItemStocks);

            if (result == MakeOrderResult.OK)
            {
                order.Status = OrderStatus.Submited;
                Orders.Add(order);
                //多线程启动，写入数据库
                Thread writeThread = new Thread(UpdateDatabase);
                writeThread.Start();
            }
            return result;
        }


        #endregion

        #region 数据库写入

        /// <summary>
        /// 写入数据库
        /// </summary>
        private static void UpdateDatabase()
        {
            Thread.Sleep(1000);
            int cnt = Orders.Where(o => o.Status == OrderStatus.Submited).Count();
            if (cnt > 0)
            {
                Monitor.TryEnter(ItemStocks, 2000);
                var writeOrders = Orders.Where(o => o.Status == OrderStatus.Submited);
                //订单新增

                if (conn.State != ConnectionState.Connecting) conn.Open();
                foreach (var writeOrder in writeOrders)
                {
                    string commandstr = string.Format("INSERT INTO EB_Order([customerId]) VALUES({0});SELECT @@IDENTITY",
                        writeOrder.customerId);
                    SqlCommand command = new SqlCommand(commandstr, conn);
                    int id = Convert.ToInt32(command.ExecuteScalar());
                    writeOrder.Id = id;
                    writeOrder.Status = OrderStatus.Saved;
                    //插入明细

                    foreach (var orderDetail in writeOrder.Items)
                    {
                        orderDetail.OrderId = id;
                        commandstr = string.Format("INSERT INTO EB_OrderDetail(OrderId,ItemId,Amount) VALUES({0},{1},1);SELECT @@IDENTITY",
                        id, orderDetail.ItemId);
                        command.CommandText = commandstr;
                        orderDetail.Id = Convert.ToInt32(command.ExecuteScalar());
                    }
                }

                var writeItems = ItemStocks.Where(i => i.IsModify);
                //商品（库存）修改
                foreach (var itemStock in writeItems)
                {
                    string commandstr = string.Format("UPDATE EB_Item SET Stock = {0} WHERE Id = {1}", itemStock.Stock, itemStock.Id);
                    SqlCommand command = new SqlCommand(commandstr, conn);
                    command.ExecuteNonQuery();
                }

                Monitor.Exit(ItemStocks);
            }

        }
        #endregion

    }

    public enum MakeOrderResult
    {
        /// <summary>
        /// 完成
        /// </summary>
        OK,
        /// <summary>
        /// 锁超时
        /// </summary>
        LockedTimeOut,
        /// <summary>
        /// 抢完
        /// </summary>
        Soldout
    }
}