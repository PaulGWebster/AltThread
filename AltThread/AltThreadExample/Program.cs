using System;
using System.Threading;
using AltThread;

namespace AltThreadExample
{
    class Program
    {
        private static Random rnd = new Random();

        static void Main(string[] args)
        {
            // When a threadnet is created it automatically detaches from what created it
            // main() will continue to process.
            AltThreadKernel ExampleNet = new AltThreadKernel();
            ExampleNet.Start(ExampleNet);

            // Some clients
            ExampleNet.Child(ClientFunc);
            ExampleNet.Child(ClientFunc);
            ExampleNet.Child(ClientFunc);
            ExampleNet.Child(ClientFunc);

            // Start a post
            ExampleNet.Post(1, "SomeTag", "A-");

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void ClientFunc(Object PacketIN)
        {
            AltThreadPacket Packet = (AltThreadPacket)PacketIN;

            Console.WriteLine("{1} -> {0}", Packet.Target, Packet.Sender);

            Thread.Sleep(100);
            Packet.Post((uint)rnd.Next(1, 5), "tag", "");

        }
    }
}
