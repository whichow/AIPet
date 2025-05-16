using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Vonweller
{
    // 首先定义响应模型类
    public class OtaResponse
    {
        public ActivationInfo activation { get; set; }
        public FirmwareInfo firmware { get; set; }
    }

    public class ActivationInfo
    {
        public string code { get; set; }
    }

    public class FirmwareInfo
    {
        public string version { get; set; }
    }

    public class Ota
    {
        private readonly string _otaUrl;
        private readonly string _macAddress;
        private readonly string _clientId;
        private readonly string _userAgent;
        private readonly string _acceptLanguage = "zh-CN";
        private readonly HttpClient _httpClient = new HttpClient();
        public OtaResponse OTA_INFO { get; private set; }
        public Queue<string> queue = new Queue<string>();

        public Ota(string otaUrl, string macAddress, string clientId, string userAgent)
        {
            _otaUrl = otaUrl;
            _macAddress = macAddress;
            _clientId = clientId;
            _userAgent = userAgent;
            ConfigureHttpClient();
            StartOTA();
        }

        private async void StartOTA()
        {
            while (true)
            {
              await  GetOtaInfoAsync();
              await  Task.Delay(60000);//60秒一次
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Device-Id", _macAddress);
            _httpClient.DefaultRequestHeaders.Add("Client-Id", _clientId);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", _acceptLanguage);
        }

        private async Task GetOtaInfoAsync()
        {

            try
            {
                 string versions = "1.6.1";
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString("VersionAPP")))
                {
                    versions = PlayerPrefs.GetString("VersionAPP");
                }
                var postData = new
                {
                    version = 0,
                    language = _acceptLanguage,
                    flash_size = 16777216,
                    minimum_free_heap_size = 8457848,
                    mac_address = _macAddress,
                    uuid = _clientId,
                    chip_model_name = "UnitySimulator",
                    application = new
                    {
                        name = "UnityApp",
                        version = versions,
                        compile_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        idf_version = "6000.1.1f",
                        elf_sha256 = "1234567890abcdef1234567890abcdef1234567890abcdef"
                    },
                    partition_table = new[]
                    {
                        new
                        {
                            label = "nvs",
                            type = 1,
                            subtype = 2,
                            address = 36864,
                            size = 16384
                        },
                        new
                        {
                            label = "otadata",
                            type = 1,
                            subtype = 0,
                            address = 53248,
                            size = 8192
                        },
                        new
                        {
                            label = "ota_0",
                            type = 0,
                            subtype = 16,
                            address = 1048576,
                            size = 6291456
                        }
                    },
                    ota = new { label = "ota_0" },
                    board = new
                    {
                        type = "UnityApp",
                        name = "UnityApp",
                        ssid = "UnityApp",
                        rssi = -55,
                        channel = 1,
                        ip = "192.168.1.1",
                        mac = _macAddress
                    }
                };

                string jsonData = JsonConvert.SerializeObject(postData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(_otaUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();
                
                Debug.Log($"[OTA] 服务器响应: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[OTA] 请求失败: {response.StatusCode}, 错误信息: {responseBody}");
                    return;
                }

                if (string.IsNullOrEmpty(responseBody))
                {
                    Debug.LogError("[OTA] 服务器返回空响应");
                    return;
                }

                try
                {
                    OTA_INFO = JsonConvert.DeserializeObject<OtaResponse>(responseBody);
                    
                    if (OTA_INFO == null)
                    {
                        Debug.LogError("[OTA] 反序列化结果为空");
                        return;
                    }

                    // 处理 activation 信息
                    if (OTA_INFO.activation?.code != null)
                    {
                        var t = $"【OTA】请登录 http://159.75.202.173:8001 绑定 Code：{OTA_INFO.activation.code}";
                        queue.Enqueue(t);
                        Debug.Log(t);
                    }

                    // 处理 firmware 信息
                    if (OTA_INFO.firmware?.version != null)
                    {
                        CompareAndUpdateVersion(OTA_INFO.firmware.version);
                    }
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"[OTA] JSON 反序列化失败: {ex.Message}\n响应内容: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OTA] 请求异常: {ex.Message}\n{ex.StackTrace}");
                queue.Enqueue($"[OTA] 请求异常: {ex.Message}");
            }
        }

        private void CompareAndUpdateVersion(string newVersion)
        {

            if (string.IsNullOrEmpty( PlayerPrefs.GetString("VersionAPP")))
            {
                Debug.Log($"[OTA] 检查到新版本：{newVersion}，准备进行处理...");
                PlayerPrefs.SetString("VersionAPP", newVersion);
            }
            else
            {
                if (PlayerPrefs.GetString("VersionAPP") != newVersion)
                {
                    Debug.Log($"[OTA] 检查到新版本：{newVersion}，准备进行处理...");
                    PlayerPrefs.SetString("VersionAPP", newVersion);
                }
                else
                {
                    Debug.Log($"[OTA] 当前版本：{PlayerPrefs.GetString("VersionAPP")}，无需更新");
                    queue.Enqueue($"[OTA] 当前版本：{PlayerPrefs.GetString("VersionAPP")}，无需更新");
                }
            }
            

        }
    }
}