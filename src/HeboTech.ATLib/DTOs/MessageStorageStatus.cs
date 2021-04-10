namespace HeboTech.ATLib.DTOs
{
    public class MessageStorageStatus
    {
        public MessageStorageStatus(string mem, int used, int total)
        {
            Mem = mem;
            Used = used;
            Total = total;
        }

        public string Mem { get; }
        public int Used { get; }
        public int Total { get; }

        public override string ToString()
        {
            return $"{Mem} (Used: {Used}, Total: {Total})";
        }
    }
}
