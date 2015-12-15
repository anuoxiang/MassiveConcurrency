using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Web;
using LCTest.Models;
using NLog;
using WebGrease.Css.Extensions;


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


        public static readonly SqlConnection connforOrder = new SqlConnection();
        public static readonly SqlConnection connforItem = new SqlConnection();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static bool IsReady = false;
        private const string ServerAddr = "192.168.1.2";

        //private const string connStr = "Data Source=192.168.1.2;Initial Catalog=LCTest;Integrated Security=false;User ID=sa;Password=Kdc123456;";
        private const string connStr = "Data Source=.\\SQLEXPRESS;Initial Catalog=LCTest;Integrated Security=true;";
        //private const string ServerAddr = "";
        #region 一次运行

        /// <summary>
        /// 静态函数初始化
        /// </summary>
        static MassiveOrder()
        {
            Customers = new List<Customer>();
            Orders = new List<Order>();
            ItemStocks = new List<ItemStock>();
            connforOrder.ConnectionString = connStr;
            connforItem.ConnectionString = connStr;
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
            SqlCommand command = new SqlCommand("SELECT Id,OpenId,Name FROM CRM_Customer;", connforOrder);
            connforOrder.Open();
            SqlDataReader reader = command.ExecuteReader();
            while (!Monitor.TryEnter(Customers, 200))
            {
                Thread.Sleep(100);
            }
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
            connforOrder.Close();
            logger.Info("载入完成");
        }

        /// <summary>
        /// 载入已经产生的订单信息
        /// </summary>
        private static void LoadItems()
        {
            logger.Info("载入商品与库存信息");
            SqlCommand command = new SqlCommand("SELECT Id,classId,Stock,Name FROM EB_Item;", connforOrder);
            connforOrder.Open();
            SqlDataReader reader = command.ExecuteReader();
            while (!Monitor.TryEnter(ItemStocks, 200))
            {
                Thread.Sleep(100);
            }
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
            connforOrder.Close();
            logger.Info("载入完成");
        }

        private static void LoadOrders()
        {
            logger.Info("载入订单信息");
            SqlCommand command = new SqlCommand("SELECT Id,customerId FROM EB_Order;", connforOrder);
            connforOrder.Open();
            SqlDataReader reader = command.ExecuteReader();
            while (!Monitor.TryEnter(Orders, 200))
            {
                Thread.Sleep(100);
            }
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
                    var order = Orders.First(r => r.Id == reader.GetInt32(1));
                    if (order != null)
                    {
                        var item = ItemStocks.FirstOrDefault(r => r.Id == reader.GetInt32(2));
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
            connforOrder.Close();
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
            var u = Customers.FirstOrDefault(r => r.Id == Id);
            return u;
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
            //优先级低，只等待2秒
            if (!Monitor.TryEnter(ItemStocks, 200))
            {
                Msg = "排队";
                result = MakeOrderResult.LockedTimeOut;
                return result;
            }

            foreach (var orderDetail in order.Items)
            {
                var item = ItemStocks.FirstOrDefault(r => r.Id == orderDetail.ItemId);
                if (item != null)
                {
                    if (item.Stock >= 1)
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
                //Thread writeThread = new Thread(SaveItem);
                //writeThread.Start();
            }
            Monitor.Exit(ItemStocks);

            if (result == MakeOrderResult.OK)
            {
                order.Status = OrderStatus.Submited;
                //内存订单启动子线程写入数据库，锁定内存变量
                while (!Monitor.TryEnter(Orders, 200))
                    Thread.Sleep(100);
                Orders.Add(order);
                //解锁
                Monitor.Exit(Orders);
                //多线程启动，写入数据库
                //Thread writeThread = new Thread(SaveOrder);
                //writeThread.Start();
            }
            //解锁

            return result;
        }

        public static List<OrderDetail> Bought(Int32 CustomerId)
        {
            while (!Monitor.TryEnter(Orders, 200))
                Thread.Sleep(100);
            List<OrderDetail> lists = new List<OrderDetail>();
            Orders.Where(r => r.customerId == CustomerId)
                .Select(r => r.Items).ForEach(i => lists.AddRange(i));
            Monitor.Exit(Orders);
            return lists;
        }

        #endregion

        #region 数据库写入
        /// <summary>
        /// 商品数量更新
        /// </summary>
        private static void SaveItem()
        {
            while (!Monitor.TryEnter(ItemStocks, 200))
                Thread.Sleep(100);
            var writeItems = ItemStocks.Where(i => i.IsModify).ToList();
            if (writeItems.Count > 0)
            {
                if (connforItem.State != ConnectionState.Open) connforItem.Open();
                //商品（库存）修改
                for (int i = 0; i < writeItems.Count; i++)
                {
                    string commandstr = string.Format("UPDATE EB_Item SET Stock = {0} WHERE Id = {1}",
                        ItemStocks[i].Stock, ItemStocks[i].Id);
                    using (SqlCommand command = new SqlCommand(commandstr, connforItem))
                    {
                        command.ExecuteNonQuery();
                    }
                    ItemStocks[i].IsModify = false;
                }
                connforItem.Close();
            }
            Monitor.Exit(ItemStocks);
        }
        /// <summary>
        /// 写入数据库
        /// </summary>
        private static void SaveOrder()
        {
            while (!Monitor.TryEnter(Orders, 200))
                Thread.Sleep(100);
            int cnt = Orders.Count(o => o.Status == OrderStatus.Submited);

            if (cnt > 0)
            {
                var writeOrders = Orders.Where(o => o.Status == OrderStatus.Submited).ToList();
                //订单新增

                if (connforOrder.State != ConnectionState.Open) connforOrder.Open();
                for (var i = 0; i < writeOrders.Count(); i++)
                {
                    string commandstr = string.Format("INSERT INTO EB_Order([customerId]) VALUES({0});SELECT @@IDENTITY",
                                        writeOrders[i].customerId);
                    SqlCommand command = new SqlCommand(commandstr, connforOrder);
                    int id = Convert.ToInt32(command.ExecuteScalar());
                    writeOrders[i].Id = id;
                    writeOrders[i].Status = OrderStatus.Saved;
                    //插入明细

                    foreach (var orderDetail in writeOrders[i].Items)
                    {
                        orderDetail.OrderId = id;
                        commandstr = string.Format("INSERT INTO EB_OrderDetail(OrderId,ItemId,Amount) VALUES({0},{1},1);SELECT @@IDENTITY",
                        id, orderDetail.ItemId);
                        command.CommandText = commandstr;
                        orderDetail.Id = Convert.ToInt32(command.ExecuteScalar());
                    }
                }
                connforOrder.Close();
            }
            Monitor.Exit(Orders);
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