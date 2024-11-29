using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AOAIProxy.Models
{
    public class ImageRequestModel
    {
        public string Model { get; set; }
        public List<Message> Messages { get; set; }
        public bool Stream { get; set; }
    }

    public class Message
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public object Content { get; set; }
    }


    public class ContentItem
    {
        public string Type { get; set; }
        public string Text { get; set; }
        [JsonProperty("image_url")]
        public ImageUrl ImageUrl { get; set; }
    }

    public class ImageUrl
    {
        public string Url { get; set; }
    }

}
