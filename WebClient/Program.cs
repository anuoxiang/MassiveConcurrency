using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;

namespace WebClient
{
    class Program
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly int Currence = 100;//默认并发数

        private static string Step1Url = "http://192.168.1.65/Home/About?UserId={0}";
        private static string Step2Url = "http://192.168.1.65/Home/AddToCart?Id={0}";
        private static string Step3Url = "http://192.168.1.65/Home/Order";

        static void Main(string[] args)
        {

            SqlConnection conn = new SqlConnection();
            conn.ConnectionString = "Data Source=192.168.1.2;Initial Catalog=LCTest;Integrated Security=false;User ID=sa;Password=Kdc123456;";
            SqlCommand command = new SqlCommand("SELECT Id,OpenId,Name FROM CRM_Customer;", conn);
            conn.Open();
            SqlDataReader reader = command.ExecuteReader();
            List<Int32> Ids = new List<int>();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Ids.Add(reader.GetInt32(0));
                }
            }

            Console.ReadKey();
        }


        static void Test()
        {
            logger.Info("载入用户信息");
            SqlConnection conn = new SqlConnection();
            conn.ConnectionString = "Data Source=.\\SQLEXPRESS;Initial Catalog=LCTest;Integrated Security=True";
            SqlCommand command = new SqlCommand("SELECT Id,OpenId,Name FROM CRM_Customer;", conn);
            conn.Open();
            SqlDataReader reader = command.ExecuteReader();
            List<Int32> Ids = new List<int>();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Ids.Add(reader.GetInt32(0));
                }
            }
            logger.Info("载入完成");


            //读取用户Id号码
            //设置多少个并发请求
            //生成浏览器
            //保存当前时间点
            //
        }
    }
}
