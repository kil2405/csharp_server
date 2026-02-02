using DummyClient.Session;
using ServerCore;
using System;
using System.Net;
using System.Threading;

namespace DummyClient
{
    class Program
    {
        static int DummyClientCount { get; } = 500;

        static void Main(string[] args)
        {
            Thread.Sleep(5000);

            // 로컬 서버에 연결
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 7777);

            Connector connector = new Connector();

            connector.Connect(endPoint,
                () => { return SessionManager.Instance.Generate(); },
                Program.DummyClientCount);

            while(true)
            {
                Thread.Sleep(10000);
            }
        }
    }
}
