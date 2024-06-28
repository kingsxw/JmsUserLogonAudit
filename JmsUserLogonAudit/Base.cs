using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JmsUserLogonAudit
{
    internal class Base
    {
        internal static void PrintColoredString(string value, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ForegroundColor = lastColor;
        }
        internal static void PrintColoredString(string key, string value, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine("{0}\t\t{1} ", key, value);
            Console.ForegroundColor = lastColor;
        }
        internal static void PrintColoredString(string key, string value1, string value2, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine("{0}\t\t{1}\t\t{2} ", key, value1, value2);
            Console.ForegroundColor = lastColor;
        }
    }
    internal class JmsServer
    {
        public string name { get; set; }
        public string baseUri { get; set; }
        public string token { get; set; }
    }
    internal class SmbServer
    {
        public string host { get; set; }
        public bool netBiosOverTCP { get; set; } =false;
        public int port { get; set; } = 445;
        public string domain { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string shareName { get; set; }
        public string sharePath { get; set; }
        public bool overwrite { get; set; } = false;
    }
    internal class BackupOption
    {
        public int monthFromOffset { get; set; } = -12;
        public int monthToOffset { get; set; } = -1;
        public string localExportPath { get; set; } = "export";
        public bool overwrite { get; set; } = false;
    }

    internal class UserLogonLog
    {
        //[DataMember]
        public string username { get; set; }
        public string reason { get; set; }
        //public string backend { get; set; }
        public bool status { get; set; }
        //public string datetime { get; set; }
    }
    internal class UserLogonStatistic
    {
        public string username { get; set; }
        public int totalCount { get; set; } = 0;
        public int failCount { get; set; } = 0;
    }

    internal class FailLogonStatistic
    {
        public string reason { get; set; }
        public int count { get; set; } = 0;
    }
    internal class FullStatistic
    {
        public string hostname { get; set; }
        public string year { get; set; }
        public string month { get; set; }
        public int logonCount { get; set; }
        public int userCount { get; set; }
        public int failCount { get; set; }
        public List<UserLogonLog> logs { get; set; } = [];
        public List<UserLogonStatistic> userStats { get; set; } = [];
        public List<FailLogonStatistic> failStats { get; set; } = [];
    }
}
