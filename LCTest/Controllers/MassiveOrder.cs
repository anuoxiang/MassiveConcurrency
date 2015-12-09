
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
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
        public static readonly SqlDataAdapter adapter = new SqlDataAdapter();
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
            LoadCustomers();
            LoadItems();
            LoadOrders();
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
            Monitor.TryEnter(Customers, 2000);
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    ItemStocks.Add(new ItemStock()
                    {
                        Id = reader.GetInt32(0),
                        classId = reader.GetInt32(1),
                        Stock = reader.GetInt32(2),
                        Name = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    });
                }
            }
            Monitor.Exit(Customers);
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
                        order.Items.Add(new OrderDetail()
                        {
                            Id = reader.GetInt32(0),
                            OrderId = order.Id,
                            ItemId = reader.GetInt32(2),
                            Amount = reader.GetInt32(3)
                        });
                    }
                }
            }

            Monitor.Exit(Orders);
            conn.Close();
            logger.Info("载入完成");
        }
        #endregion

        /// <summary>
        /// 验证用户
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public static string CheckUser(int Id)
        {
            var u = Customers.Where(r => r.Id == Id).FirstOrDefault();
            if (u != null)
                return u.Name;
            else
                return "";
        }

        /// <summary>
        /// 产生订单
        /// </summary>
        /// <param name="customer"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public static int MakeOrder(Customer customer, Order order)
        {

            return 0;
        }

    }
}