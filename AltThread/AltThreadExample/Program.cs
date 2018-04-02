using System;
using System.Threading;
using AltThread;

namespace AltThreadExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // When a threadnet is created it automatically detaches from what created it
            // main() will continue to process.
            ThreadNet Tnet = new ThreadNet(
                new ThreadNodeConfig()
                {
                    Name = "TestNetwork",
                }
            );
            ThreadNet.Start(Tnet);

            Tnet.Child(Handler1);

            Thread.Sleep(1000 * 60);
        }

        private static void Handler1(object PacketIN)
        {
            ThreadPacket Packet = (ThreadPacket)PacketIN;

            Console.WriteLine("Releasing test packet");
            Packet.Post(1, "test post", "string");
        }
    }
}
