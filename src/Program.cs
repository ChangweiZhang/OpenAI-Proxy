using AOAIProxy.Helpers;
using AOAIProxy.Models;
using AOAIProxy.Services;
using Yarp.ReverseProxy.Forwarder;
using Azure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Transforms;
using System.Net.Http.Headers;
HashSet<string> validHeaders = new HashSet<string>(new List<string>()
{
    "api-key",
    "authorization"
});
WebApplication app = null;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))

    .AddTransforms(context =>
    {
        var logger = app.Services.GetService<ILogger<UsageMessage>>();
        context.CopyRequestHeaders = false;
        context.AddRequestTransform(async requestContext =>
        {
            var cache = app.Services.GetService<IMemoryCache>();
            var reqId = Guid.NewGuid().ToString();
            using var reader = new StreamReader(requestContext.HttpContext.Request.Body);
            //requestContext.HeadersCopied = false;
            // TODO: size limits, timeouts
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrEmpty(body))
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                var tokenCount = 0;
                var requestData = JsonConvert.DeserializeObject<ImageRequestModel>(body);
                if (requestData.Stream)
                {
                    try
                    {
                        var tokenMessages = new List<Dictionary<string, string>>();

                        foreach (var message in requestData.Messages)
                        {

                            if (message.Content is string)
                            {
                                tokenMessages.Add(
                                  new Dictionary<string, string>
                                  {
                                      ["name"] = message.Name,
                                      ["role"] = message.Role,
                                      ["content"] = message.Content as string
                                  }
                                  );
                            }
                            else
                            {
                                tokenMessages.Add(
                                    new Dictionary<string, string>
                                    {
                                        ["name"] = message.Name,
                                        ["role"] = message.Role,
                                    }
                                    );
                                var imageData = (message.Content as JToken).AsJEnumerable();

                                foreach (var item in imageData)
                                {
                                    if (item.Value<string>("type") == "text")
                                    {
                                        tokenCount += TokenHelper.calculateToken(item.Value<string>("text"), requestData.Model);
                                    }
                                    if (item.Value<string>("type") == "image_url")
                                    {
                                        tokenCount += TokenHelper.CountImageTokenOnce(item["image_url"].Value<string>("url"));
                                    }

                                }

                            }
                        }
                        tokenCount += TokenHelper.NumTokensFromMessages(tokenMessages, requestData.Model);
                        cache.Set<UsageMessage>(reqId, new UsageMessage
                        {
                            RequestId = reqId,
                            prompt_tokens = tokenCount
                        },
                        TimeSpan.FromMinutes(5)
                        );
                        //if (!cacheResult)
                        //{
                        //    logger.LogError($"cache request id:{reqId} request content data fail");
                        //}
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "calculate stream mode request content error");
                    }
                }
                requestContext.HttpContext.Request.Headers.TryAdd("req-id", reqId);

                foreach (var header in requestContext.HttpContext.Request.Headers)
                {
                    if (validHeaders.Contains(header.Key.ToLower()))
                    {
                        requestContext.ProxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToList());
                    }
                }


                // Change Content-Length to match the modified body, or remove it.
                requestContext.HttpContext.Request.Body = new MemoryStream(bytes);
                // Request headers are copied before transforms are invoked, update any needed headers on the ProxyRequest
                requestContext.ProxyRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                requestContext.ProxyRequest.Content.Headers.ContentLength = bytes.Length;
            }
        });
        context.AddResponseTransform(async responseContext =>
        {
            //var logger = app.Services.GetService<ILogger>();
            StreamReader reader;
            var reqHeaders = responseContext.HttpContext.Request.Headers;
            var reqId = string.Empty;
            var apimRequestId = string.Empty;
            string model = string.Empty;
            foreach (var header in reqHeaders)
            {
                if (header.Key == "req-id")
                {
                    reqId = header.Value;
                    break;
                }
            }
            var subId = string.Empty;
            foreach (var header in reqHeaders)
            {
                if (header.Key == "sub-id")
                {
                    subId = header.Value;
                    break;
                }
            }
            foreach (var header in responseContext.ProxyResponse.Headers)
            {
                if (header.Key == "apim-request-id")
                {
                    apimRequestId = header.Value.FirstOrDefault();
                    break;
                }
            }
            if (responseContext.ProxyResponse.IsSuccessStatusCode)
            {

                var isStream = responseContext.ProxyResponse.Content.Headers.ContentType?.MediaType == "text/event-stream";
                string usageDataString = string.Empty;

                if (isStream)
                {
                    // 确保 SSE 流式传输
                    responseContext.HttpContext.Response.Headers.Remove("transfer-encoding");

                    // 处理 SSE 消息流并记录
                    var responseStream = await responseContext.ProxyResponse.Content.ReadAsStreamAsync();
                    reader = new StreamReader(responseStream);
                    var outputData = new StringBuilder();

                    // 逐行读取 SSE 消息并记录日志
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            // 记录 SSE 消息
                            //Console.WriteLine($"SSE Message: {line}");
                            var dataBody = line.Trim().Substring(6);
                            try
                            {
                                var completionUpdate = JsonConvert.DeserializeObject<StreamDataChunck>(dataBody);
                                if (completionUpdate?.choices.Count() > 0)
                                {
                                    outputData.Append(completionUpdate.choices[0].delta.content);
                                    if (string.IsNullOrEmpty(model))
                                    {
                                        model = completionUpdate.model;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "combine stream message content error");
                            }
                        }

                        // 将数据写回客户端
                        await responseContext.HttpContext.Response.WriteAsync(line + "\n");
                        await responseContext.HttpContext.Response.Body.FlushAsync(); // 确保流式输出
                    }
                    try
                    {
                        var data = outputData.ToString();
                        var tokenCount = TokenHelper.calculateToken(data);
                        var cache = app.Services.GetService<IMemoryCache>();

                        var usageMessage = cache.Get<UsageMessage>(reqId);
                        if (usageMessage != null)
                        {
                            usageMessage.Model = model;
                            usageMessage.SubId = subId;
                            usageMessage.completion_tokens = tokenCount;
                            usageMessage.total_tokens = usageMessage.prompt_tokens + usageMessage.completion_tokens;
                            cache.Remove(reqId);
                            usageDataString = JsonConvert.SerializeObject(usageMessage);
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "stream mode export to eventhub error");
                    }

                }
                else
                {
                    var stream = await responseContext.ProxyResponse.Content.ReadAsStreamAsync();
                    reader = new StreamReader(stream);
                    // TODO: size limits, timeouts
                    var body = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(body))
                    {
                        var bytes = Encoding.UTF8.GetBytes(body);
                        try
                        {
                            var responseData = JsonConvert.DeserializeObject<ResponseData>(body);
                            UsageMessage usageMessage = new UsageMessage();
                            usageMessage.Model = responseData.model;
                            usageMessage.SubId = subId;
                            usageMessage.prompt_tokens = responseData.usage.prompt_tokens;
                            usageMessage.completion_tokens = responseData.usage.completion_tokens;
                            usageMessage.total_tokens = responseData.usage.total_tokens;
                            usageDataString = JsonConvert.SerializeObject(usageMessage);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "non-stream mode export to eventhub error");
                        }
                        // Change Content-Length to match the modified body, or remove it.
                        responseContext.HttpContext.Response.ContentLength = bytes.Length;
                        // Response headers are copied before transforms are invoked, update any needed headers on the HttpContext.Response.
                        await responseContext.HttpContext.Response.Body.WriteAsync(bytes);

                    }

                }
                try
                {
                    {
                        var eventhubService = app.Services.GetService<EventHubService>();
                        var uploadResult = await eventhubService.SendAsync(usageDataString);
                        if (!uploadResult)
                        {
                            logger.LogError("upload usage data to eventhub error");
                        }
                    }


                }
                catch
                {

                }
            }
        });
    })
   .ConfigureHttpClient((context, httpClient) =>
    {
        httpClient.MaxResponseHeadersLength = 128 * 1024;
    })
   ;
// Add services to the container.
builder.Services.AddTransient<EventHubService>();
app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapReverseProxy();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        // Custom inline middleware
        var isValid = false;
        foreach (var header in context.Request.Headers)
        {
            if (validHeaders.Contains(header.Key.ToLower()))
            {
                isValid = true;
            }
        }
        if (isValid)
        {
            await next();
        }
        else
        {
            context.Response.StatusCode = 401;
            await context.Response.CompleteAsync();
        }
    });
});

async Task LogRequest(HttpContext context)
{
    using (StreamReader reader = new StreamReader(context.Request.Body, Encoding.UTF8))
    {
        string bodyContent = await reader.ReadToEndAsync();
        Console.WriteLine(bodyContent);
    }
}
async Task LogResponse(HttpContext context)
{
    using (StreamReader reader = new StreamReader(context.Response.Body, Encoding.UTF8))
    {
        string bodyContent = await reader.ReadToEndAsync();
        Console.WriteLine(bodyContent);
    }
}

app.Run();
