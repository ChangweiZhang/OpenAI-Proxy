namespace AOAIProxy.Models
{
    public class StreamDataChunck
    {
        public Choice[] choices { get; set; }
        public int created { get; set; }
        public string id { get; set; }
        public string model { get; set; }
        public string _object { get; set; }
        public string system_fingerprint { get; set; }
    }

    public class Choice
    {
        public Content_Filter_Results content_filter_results { get; set; }
        public Delta delta { get; set; }
        public object finish_reason { get; set; }
        public int index { get; set; }
    }

    public class Content_Filter_Results
    {
    }

    public class Delta
    {
        public string content { get; set; }
        public string role { get; set; }
    }

}
