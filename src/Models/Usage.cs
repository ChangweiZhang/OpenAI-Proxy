namespace AOAIProxy.Models
{
    public class Usage
    {
        public int completion_tokens { get; set; }
        public int prompt_tokens { get; set; }
        public int total_tokens { get; set; }
    }
    public class UsageMessage
    {
        public string RequestId { get; set; }
        public string Model { get; set; }
        public string SubId { get; set; }
        public int completion_tokens { get; set; }
        public int prompt_tokens { get; set; }
        public int total_tokens { get; set; }
    }
}
