using SkiaSharp;
using Tiktoken;

namespace AOAIProxy.Helpers
{
    public class TokenHelper
    {
        static Dictionary<string, Encoder> encoders = new Dictionary<string, Encoder>();

        static HashSet<string> OpenAIModels = new HashSet<string>
        {
            "gpt-3.5-turbo-0125",
            "gpt-4-0314",
            "gpt-4-32k-0314",
            "gpt-4-0613",
            "gpt-4-32k-0613",
            "gpt-4o-mini-2024-07-18",
            "gpt-4o-2024-08-06"
        };
        public static int calculateToken(string text, string model = "gpt-4")
        {
            if(encoders.ContainsKey(model))
            {
                return encoders[model].CountTokens(text);
            }
            else
            {
                var encoder = ModelToEncoder.For(model);
                return encoder.CountTokens(text);
            }
           
        }
        public static int NumTokensFromMessages(List<Dictionary<string, string>> messages, string model = "gpt-4o-mini-2024-07-18")
        {
            int tokensPerMessage;
            int tokensPerName;
            Encoder encoding;

            try
            {
                if (encoders.ContainsKey(model))
                {
                    encoding= encoders[model];
                }
                else
                {
                   encoding = ModelToEncoder.For(model);
                }
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine("Warning: model not found. Using cl100k_base encoding.");
                encoding = ModelToEncoder.For("cl100k_base");
            }

            if (OpenAIModels.Contains(model))
            {
                tokensPerMessage = 3;
                tokensPerName = 1;
            }
            else if (model.Contains("gpt-3.5-turbo"))
            {
                Console.WriteLine("Warning: gpt-3.5-turbo may update over time. Returning num tokens assuming gpt-3.5-turbo-0125.");
                return NumTokensFromMessages(messages, "gpt-3.5-turbo-0125");
            }
            else if (model.Contains("gpt-4o-mini"))
            {
                Console.WriteLine("Warning: gpt-4o-mini may update over time. Returning num tokens assuming gpt-4o-mini-2024-07-18.");
                return NumTokensFromMessages(messages, "gpt-4o-mini-2024-07-18");
            }
            else if (model.Contains("gpt-4o"))
            {
                Console.WriteLine("Warning: gpt-4o and gpt-4o-mini may update over time. Returning num tokens assuming gpt-4o-2024-08-06.");
                return NumTokensFromMessages(messages, "gpt-4o-2024-08-06");
            }
            else if (model.Contains("gpt-4"))
            {
                Console.WriteLine("Warning: gpt-4 may update over time. Returning num tokens assuming gpt-4-0613.");
                return NumTokensFromMessages(messages, "gpt-4-0613");
            }
            else
            {
                throw new NotImplementedException($"NumTokensFromMessages() is not implemented for model {model}.");
            }

            int numTokens = 0;
            foreach (var message in messages)
            {
                numTokens += tokensPerMessage;
                foreach (var kvp in message)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        numTokens += encoding.CountTokens(kvp.Value);

                        if (kvp.Key == "name")
                        {
                            numTokens += tokensPerName;
                        }
                    }
                }
            }

            numTokens += 3; // every reply is primed with <|start|>assistant<|message|>
            return numTokens;
        }

        public static int CalculateVisionPricing(int width, int height, string detail = "high")
        {
            if (detail == "low")
            {
                return 85;
            }

            // 如果宽或高超过2048，按比例缩放到2048以内
            if (width > 2048 || height > 2048)
            {
                const int maxSize = 2048;
                double aspectRatio = (double)width / height;

                if (aspectRatio > 1)
                {
                    width = maxSize;
                    height = (int)(maxSize / aspectRatio);
                }
                else
                {
                    height = maxSize;
                    width = (int)(maxSize * aspectRatio);
                }
            }

            // 如果宽和高都超过768，则将最小边缩放到768
            const int minSize = 768;
            double newAspectRatio = (double)width / height;

            if (width > minSize && height > minSize)
            {
                if (newAspectRatio > 1)
                {
                    height = minSize;
                    width = (int)(minSize * newAspectRatio);
                }
                else
                {
                    width = minSize;
                    height = (int)(minSize / newAspectRatio);
                }
            }

            // 计算tiles的数量
            int tilesWidth = (int)Math.Ceiling((double)width / 512);
            int tilesHeight = (int)Math.Ceiling((double)height / 512);

            return 85 + 170 * (tilesWidth * tilesHeight);
        }


        public static (int, int) GetImageSizeFromBase64(string base64Data)
        {
            // 移除 "data:image/png;base64," 前缀
            string base64 = base64Data.Substring(base64Data.IndexOf(",") + 1);

            // 将Base64字符串解码为字节数组
            byte[] imageBytes = Convert.FromBase64String(base64);

            // 使用 SkiaSharp 处理图像
            using (var image = SKBitmap.Decode(imageBytes))
            {
                // 获取图像的宽度和高度
                int width = image.Width;
                int height = image.Height;
                imageBytes = null;
                GC.Collect();
                return (width, height);
            }
        }

        public static int CountImageTokenOnce(string base64String)
        {
            var (width, height) = GetImageSizeFromBase64(base64String);
            return CalculateVisionPricing(width, height);

        }
    }
}
