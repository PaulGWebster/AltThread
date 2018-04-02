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
            AltThreadKernel Tnet1 = new AltThreadKernel();
            Tnet1.Start(Tnet1);
            Tnet1.Child(Handler1);
            Tnet1.Child(Handler1);

            AltThreadKernel Tnet2 = new AltThreadKernel();
            Tnet2.Start(Tnet2);
            Tnet2.Child(Handler1);

            Tnet1.Post(1, "string", "test");
            Tnet2.Post(1, "string", "test");

            while(true)
            {
                Thread.Sleep(1000);
            }
        }

        
        private static void Handler1(Object PacketIN)
        {
            AltThreadPacket Packet = (AltThreadPacket)PacketIN;

            Console.WriteLine((string)Packet.Payload);

            Thread.Sleep(1000);

            Packet.Post(1, "string", "test post");
        }
        
    }
}
