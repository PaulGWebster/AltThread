using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AltThread
{

    public class AltThreadKernel
    {
        // A Static memory object 
        private static Dictionary<uint, Dictionary<uint, AltThreadKernelSet>> AltThreadNet = null;
        private static Dictionary<string, uint> AltThreadState = null;

        private uint AltThreadInstanceNetworkID = 0;

        // Start the main processing loop for message passing (Primary network)
        public uint Start(AltThreadKernel Tnet)
        {
            // Initilize the primary store if not done so already
            if (AltThreadNet == null)
            {
                AltThreadNet = new Dictionary<uint, Dictionary<uint, AltThreadKernelSet>>();
                AltThreadState = new Dictionary<string, uint>() {
                    { "GlobalNetworkID",0 },
                };
            }

            // Grab the rescue ID from the global stash
            uint NetworkID = (uint)AltThreadState["GlobalNetworkID"];
            AltThreadState.Add(NetworkID.ToString(), 1);

            // Increment it for the next network creation
            AltThreadState["GlobalNetworkID"] = (uint)AltThreadState["GlobalNetworkID"] + 1;
            // Create a personal reference to this ID so children know the networkid
            AltThreadInstanceNetworkID = AltThreadState["GlobalNetworkID"] - 1;

            // Add the network by ID to the primary memory store
            // Initilize the new space for the network stack
            AltThreadNet.Add(NetworkID, new Dictionary<uint, AltThreadKernelSet>() );
            AltThreadNet[NetworkID].Add(0,new NetThreadKernelSet());

            // Initilize the queue hints
            ((NetThreadKernelSet)AltThreadNet[NetworkID][0]).ChildQueueHint = new BlockingCollection<uint>();

            // And the TX/RX
            AltThreadNet[NetworkID][0].TX = new BlockingCollection<AltThreadPacket>();
            AltThreadNet[NetworkID][0].RX = new BlockingCollection<AltThreadPacket>();

            // Create a shortcut to the storage area
            NetThreadKernelSet KernelSetShortcut = (NetThreadKernelSet)AltThreadNet[NetworkID][0];

            // Save the kernel
            KernelSetShortcut.Kernel = Tnet;
            // Create a place for the thread starter
            KernelSetShortcut.ThreadStart = new ParameterizedThreadStart(AltThreadWorker);
            // A place to put the thread ref its self
            KernelSetShortcut.Thread = new Thread(KernelSetShortcut.ThreadStart);
            // Define how many workers (TODO)
            KernelSetShortcut.Workers = 1;
            // Store the ID for this network
            KernelSetShortcut.NetworkID = NetworkID;
            // Type of set
            KernelSetShortcut.Child = false;

            // Start the worker its self
            KernelSetShortcut.Thread.Start(new uint[] { NetworkID, 0 });

            // Return a reference to the primary store or not
            return NetworkID;
        }

        // A method for retrieving the next packet from mid function ball
        public AltThreadPacket Pull()
        {
            return Pull(0);
        }
        public AltThreadPacket Pull(uint OriginClientID)
        {
            if (AltThreadNet[AltThreadInstanceNetworkID][OriginClientID].RX.Count > 0)
            {
                return AltThreadNet[AltThreadInstanceNetworkID][OriginClientID].RX.Take();
            }
            return null;
        }

        // Shut down the entire thread network
        public void Shutdown()
        {
            // Shut down all threads first
            List<uint> DeleteKeys = new List<uint>();
            foreach (KeyValuePair<uint,AltThreadKernelSet> Val in AltThreadNet[AltThreadInstanceNetworkID])
            {
                if (Val.Key != 0)
                {
                    DeleteKeys.Add(Val.Key);
                }
            }
            foreach (uint Key in DeleteKeys)
            {
                Shutdown(Key);
            }
        }

        // Shutdown a client thread
        public void Shutdown(uint ClientID)
        {
            // Send an abort signal to the thread
            AltThreadNet[AltThreadInstanceNetworkID][ClientID].Thread.Abort();

            // Clear up its work space
            lock(AltThreadNet[AltThreadInstanceNetworkID])
            {
                AltThreadNet[AltThreadInstanceNetworkID][ClientID].TX = null;
                AltThreadNet[AltThreadInstanceNetworkID][ClientID].RX = null;
                ((ChildThreadKernelSet)AltThreadNet[AltThreadInstanceNetworkID][ClientID]).Stash.Clear();
                AltThreadNet[AltThreadInstanceNetworkID].Remove(ClientID);
            }
        }

        // We do not deal with posts directly from the NET controller
        // Add it to the input queue for whichever thread sent it
        public void Post(uint TargetChildID, string Type, object Payload)
        {
            this.Post(TargetChildID, Type, Payload, 0);
        }
        public void Post(uint TargetChildID, string Type, object Payload, uint OriginClientID)
        {
            uint NetworkID = this.AltThreadInstanceNetworkID;

            // Generate the packet
            AltThreadPacket Packet = new AltThreadPacket()
            {
                Target = TargetChildID,
                Sender = OriginClientID,
                Payload = Payload,
                Type = Type,
            };

            // First locate the stack that we are sending from, if it does not exist just drop the packet silently
            if (AltThreadNet[NetworkID].ContainsKey(TargetChildID))
            {
                if (TargetChildID != 0)
                {
                    // Update the stash to that of the target, Core does not have a stash
                    Packet.Stash = ((ChildThreadKernelSet)AltThreadNet[NetworkID][TargetChildID]).Stash;
                }
                AltThreadNet[NetworkID][OriginClientID].TX.Add(Packet);
                // Hint to the network worker that data is ready to be recv'd from this thread
                ((NetThreadKernelSet)AltThreadNet[NetworkID][0]).ChildQueueHint.Add(OriginClientID);
            }
        }

        // The worker its self, this is multiuse as we always pass it a reference to its
        // entry in the global store
        private static void AltThreadWorker (object PassedIDs)
        {
            uint[] PassedArgs = (uint[])PassedIDs;
            uint NetworkID = PassedArgs[0];
            uint ChildID = PassedArgs[1];

            Console.WriteLine("Starting network id: {0}, childid: {1}",NetworkID,ChildID);

            AltThreadKernelSet MyStack = AltThreadNet[NetworkID][ChildID];

            while (true)
            {
                if (!MyStack.Child)
                {
                    // We have a blocking notify queue so lets wait for it to do something
                    uint ClientIDHint = ((NetThreadKernelSet)AltThreadNet[NetworkID][0]).ChildQueueHint.Take();
                    // Ok we got a hint, lets check it out
                    // Does this ID still exist?
                    if (!AltThreadNet[NetworkID].ContainsKey(ClientIDHint))
                    {
                        continue;
                    }
                    // Does it have anything in its TX queue?
                    if (AltThreadNet[NetworkID][ClientIDHint].TX.Count == 0)
                    {
                        continue;
                    }
                    // Ok must be good lets get the packet
                    AltThreadPacket Packet = AltThreadNet[NetworkID][ClientIDHint].TX.Take();
                    // Check the place its going exists, if it does move it there.
                    if (AltThreadNet[NetworkID].ContainsKey(Packet.Target))
                    {
                        //Packet.Stash = AltThreadNet[NSID][ClientIDHint].
                        AltThreadNet[NetworkID][Packet.Target].RX.Add(Packet);
                    }
                    // We succesfully moved a packet, now upto the other worker to pick it up!
                }
                else
                {
                    // Our job is simply delivery
                    // Grab the packet
                    AltThreadPacket Packet = AltThreadNet[NetworkID][ChildID].RX.Take();

                    // Extract the function reference
                    ((ChildThreadKernelSet)AltThreadNet[NetworkID][ChildID]).Function(Packet);
                }
                //Console.WriteLine("Core({0}:{1}) running", NetworkID, ChildID);
            }

            // If we got here the thread is exiting, we need to clean the thread space
            //Console.WriteLine("Thread({0}:{1}) exiting..",NetworkID,ChildID);
        }

        // A place to store information about our different threads
        private class AltThreadKernelSet
        {
            public Thread Thread { get; internal set; }
            public uint NetworkID { get; internal set; }
            public uint ChildID { get; internal set; }
            public bool Child { get; internal set; }
            public ParameterizedThreadStart ThreadStart { get; internal set; }
            public BlockingCollection<AltThreadPacket> TX { get; internal set; }
            public BlockingCollection<AltThreadPacket> RX { get; internal set; }
        }
        private class ChildThreadKernelSet : AltThreadKernelSet
        {
            public Action<object> Function { get; internal set; }
            public Dictionary<string, object> Stash { get; internal set; }
        }
        private class NetThreadKernelSet : AltThreadKernelSet
        {
            public int Workers { get; internal set; }
            public AltThreadKernel Kernel { get; internal set; }
            public BlockingCollection<uint> ChildQueueHint { get; set; }
        }

        public uint Child(Action<object> FunctionRef)
        {
            // First lets get relevent ID
            string NetworkID = this.AltThreadInstanceNetworkID.ToString();
            uint ChildID = AltThreadState[NetworkID];

            // And increment it so the next child has the right one
            // Maybe we should consider locking altthreadstate while doing this?
            AltThreadState[NetworkID] = ChildID + 1;

            // Now lets generate a base set
            ChildThreadKernelSet ChildSet = new ChildThreadKernelSet
            {
                ThreadStart = new ParameterizedThreadStart(AltThreadWorker),
                ChildID = ChildID,
                NetworkID = this.AltThreadInstanceNetworkID,
                Child = true,
                Function = FunctionRef,
                TX = new BlockingCollection<AltThreadPacket>(),
                RX = new BlockingCollection<AltThreadPacket>(),
                Stash = new Dictionary<string, object>()
            };

            // And generate the thread its self
            ChildSet.Thread = new Thread(ChildSet.ThreadStart);

            // Add the thread to the global store
            AltThreadNet[this.AltThreadInstanceNetworkID].Add(ChildID, ChildSet);

            // Start the child
            ChildSet.Thread.Start(new uint[] { ChildSet.NetworkID, ChildID });

            // Set the name of the thread to be
            ChildSet.Thread.Name = ChildID.ToString();

            // Return the ChildID only
            return ChildID;
        }
    }

    public class AltThreadPacket : AltThreadKernel
    {
        public uint Target { get; internal set; }
        public uint Sender { get; internal set; }
        public object Payload { get; internal set; }
        public string Type { get; internal set; }
        public Dictionary<string,object> Stash { get; internal set;  }
        public new void Post(uint TargetChildID, string Type, object Payload)
        {
            this.Post(TargetChildID, Type, Payload, this.Target);
        }
        public new AltThreadPacket Pull()
        {
            return this.Pull(this.Target);
        }
        public new void Shutdown()
        {
            this.Shutdown(this.Target);
        }
    }
}

