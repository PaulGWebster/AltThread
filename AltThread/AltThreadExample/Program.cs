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

            Thread.Sleep(1000);

            // Add whatever we got in payload to our stash, pass the double of it back to the next worker
            if (Packet.Stash.ContainsKey("Spot"))
            {
                Packet.Stash["Spot"] = Packet.Stash["Spot"] + (string)Packet.Payload;
            }
            else
            {
                Packet.Stash.Add("Spot", (string)Packet.Payload);
            }

            Console.WriteLine("Client({0}) stash: ({1})", Packet.Target, Packet.Stash["Spot"]);

            if (Packet.Target == 4) {
                Packet.Post(1, "tag", Packet.Stash["Spot"]);
                Packet.Shutdown();
            }
            else
            {
                Packet.Post(Packet.Target + 1, "tag", Packet.Stash["Spot"]);
            }
        }
    }
}
