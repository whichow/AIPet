using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Unity.VisualScripting;
using Vonweller;

public class VL : MonoBehaviour
{
    // 配置参数
    [Header("API 设置")]
    public string apiKey = Constants.VLconfig.apiKey;
    [Header("音色 设置")]
    public string voice = Constants.VLconfig.voice;

    public async Task<string> StartStreamingSession(string base64Image)
    {
        Debug.LogWarning("开始第三方平台识别画面内容");
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // 设置超时时间
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var requestPayload = new
            {
                model = "qwen-omni-turbo",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/png;base64,{base64Image}"
                                }
                            },
                            new
                            {
                                type = "text",
                                text = "图中描绘的是什么景象？"
                            }
                        }
                    }
                },
                modalities = new[] { "text", "audio" },
                audio = new { voice = voice, format = "wav" },
                stream = true,
                stream_options = new { include_usage = true }
            };

            var response = await httpClient.PostAsync(
                "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
                new StringContent(JsonConvert.SerializeObject(requestPayload), Encoding.UTF8, "application/json")
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.LogError($"API请求失败: {response.StatusCode}, 错误信息: {errorContent}");
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var text = new StringBuilder();
            var audioData = new StringBuilder();


            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line?.StartsWith("data: ") == true)
                {
                    ProcessStreamChunk(line, audioData, text);
                }
            }

            return text.ToString();
        }
        catch (HttpRequestException e)
        {
            Debug.LogError($"网络请求异常: {e.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            Debug.LogError("请求超时");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"流式会话异常: {e.Message}");
            return null;
        }
    }

    void ProcessStreamChunk(string chunkData, StringBuilder audioData, StringBuilder text)
    {
        try
        {
            var json = chunkData.Substring(6); // 移除"data: "前缀
            var chunk = JsonConvert.DeserializeObject<StreamChunk>(json);

            // 处理文本转录
            if (!string.IsNullOrEmpty(chunk?.Choices?[0].Delta?.Audio?.Transcript))
            {
                Debug.Log($"[AI响应] {chunk.Choices[0].Delta.Audio.Transcript}");
                text.Append(chunk.Choices[0].Delta.Audio.Transcript);
            }
            // 处理音频数据
            //var audioDataBase64 = chunk?.Choices?[0].Delta?.Audio?.Data;
            //if (!string.IsNullOrEmpty(audioDataBase64))
            //{
            //    try
            //    {
            //        var wavBytes = Convert.FromBase64String(audioDataBase64);
                    
            //    }
            //    catch (FormatException e)
            //    {
            //        Debug.LogWarning($"音频数据格式错误: {e.Message}");
            //    }
            //}

            // 处理用量统计
            if (chunk?.Usage != null)
            {
                Debug.Log($"[用量] 总tokens: {chunk.Usage.TotalTokens}");
            }
        }
        catch (JsonException e)
        {
            Debug.LogWarning($"JSON解析异常: {e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"数据处理异常: {e.Message}");
        }
    }

    // 数据模型
    [Serializable]
    class StreamChunk
    {
        public Choice[] Choices;
        public UsageInfo Usage;
    }

    [Serializable]
    class Choice
    {
        public DeltaContent Delta;
    }

    [Serializable]
    class DeltaContent
    {
        public AudioContent Audio;
    }

    [Serializable]
    class AudioContent
    {
        public string Data;
        public string Transcript;
    }

    [Serializable]
    class UsageInfo
    {
        [JsonProperty("total_tokens")]
        public int TotalTokens;
    }
}
