namespace eScope.Protocol
{
    // detects lost frames from the 8-bit frame counter (flow) sent by the target in each header
    public class FlowCounter
    {
        const int MAX_LOST = 64; // larger gap: corrupted header or target restart, resync instead of inventing samples
        readonly string Name;
        byte expected = 0;
        volatile bool synced = false;
        int lost = 0;

        public FlowCounter(string name)
        {
            Name = name;
        }
        // next frame becomes the reference (open, reconfiguration)
        public void Resync()
        {
            synced = false;
        }
        public void Check(byte flowId)
        {
            if (synced)
            {
                int gap = (byte)(flowId - expected);
                if (gap > MAX_LOST)
                {
                    Console.WriteLine($"{Name} Flow resync : {flowId} but expect {expected}");
                }
                else if (gap != 0)
                {
                    Console.WriteLine($"{Name} Flow error : {gap} frame(s) lost");
                    lost += gap;
                }
            }
            else
            {
                lost = 0;
            }
            synced = true;
            expected = (byte)(flowId + 1);
        }
        // frames lost since the previous call, reported with the next periodic frame
        public int Take()
        {
            int l = lost;
            lost = 0;
            return l;
        }
    }
}
