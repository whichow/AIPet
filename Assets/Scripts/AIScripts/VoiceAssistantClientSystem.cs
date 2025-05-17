///=====================================================
/// - FileName:      VoiceAssistantClientSystem.cs
/// - Namespace:     Vonweller
/// - Description:   高级语音助手客户端系统（使用System.Net.WebSockets）
///=====================================================
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using XiaozhiAI.Services.Mqtt;
using XiaozhiAI.Models.IoT;
using System.Net.Http;
using System.Text;


#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

namespace Vonweller
{
    public class VoiceAssistantClientSystem
    {

        #region 字段属性

        
        private const int MaxBufferSeconds = 20; // 最大缓冲时间
        public VoiceAssistantConfig config = new VoiceAssistantConfig();
        public AudioManager audioManager;
        public WebSocketManager wsManager;
        private string sessionId { get; set; }
        private string ttsState = "idle";
        private string listenState = "stop";
        private string keyState = "release";
        private bool isManualMode;
        private bool isPlaying;
        public MqttService mqttService;
        public ThingManager thingManager = ThingManager.GetInstance();
        // VAD相关参数
        private bool useVAD = true;                // 是否使用VAD
        private float vadThreshold = 0.02f;        // VAD阈值，可根据环境噪音调整
        private int vadSilenceFrames = 30;         // 静音帧数阈值(约0.5秒)
        private int currentSilenceFrames = 0;      // 当前连续静音帧数
        private bool isSpeaking = false;           // 是否检测到说话
        private float lastTtsEndTime = 0f;         // 上次TTS结束的时间
        private float ttsCooldownTime = 1.5f;      // TTS结束后的冷却时间(秒)
        private bool isInCooldown = false;         // 是否处于冷却期



        private string deviceMac; // 设备MAC地址
        private string url;
        private string OTA;

        [Serializable]
        private class OtaApplication
        {
            public string name;
            public string version;
        }

        [Serializable]
        private class OtaPostData
        {
            public OtaApplication application;
        }

        [Serializable]
        private class OtaActivation
        {
            public string code;
        }

        [Serializable]
        private class OtaFirmware
        {
            public string version;
        }

        [Serializable]
        private class OtaResponse
        {
            public OtaActivation activation;
            public OtaFirmware firmware;
        }
        #endregion
        public void Init()
        {
            // 可以从配置中读取VAD参数
            useVAD = config.useVAD;
            vadThreshold = config.vadThreshold;
            vadSilenceFrames = config.vadSilenceFrames;
        }

        public async Task webSocket_IOT_Mqtt_Microphone_InIt(string URL, string OTAurl)
        {
            try
            {
                this.deviceMac = MySystemInfo.GetMacAddress();
                this.url = URL;
                this.OTA = OTAurl;
                Debug.Log("开始AI初始化");

                // 确保在安卓平台上等待权限请求完成

                await RequestMicrophonePermission();
                await RequestCameraPermission();


                isManualMode = config.manualMode;

                // 初始化音频管理器
                audioManager = new AudioManager();
                audioManager.AudioManagerInit();

                var ota = new Ota(this.OTA, this.deviceMac, "7b94d69a-9808-4c59-9c9b-704333b38aff", "Unity-ID");
                

                // 初始化物联网管理器
                thingManager = ThingManager.GetInstance();
                if (thingManager == null)
                {
                    Debug.LogError("物联网管理器初始化失败");
                    return;
                }

                // 初始化WebSocket管理器
                wsManager = new WebSocketManager();
                wsManager.audioManager = audioManager;
                // 等待WebSocket连接
                await ConnectWebSocket();
                await Task.Delay(1000);


                // 初始化MQTT服务
                await StartMqtt();

                // 初始化物联网设备
                await InitializeIotDevices();

                // 开始音频处理
                await SendAudioCoroutine();

                // 启动更新循环
                // ActionKit.OnUpdate().Register((a) => Update()).StartGlobal();

                Debug.Log("AI初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"AI初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task StartMqtt()
        {
            try
            {
                // 创建服务实例
                mqttService = new MqttService();
                if (mqttService == null)
                {
                    Debug.LogError("MQTT服务创建失败");
                    return;
                }

                var mqttHandlerManager = new MqttMessageHandlerManager(mqttService);
                // 注册MQTT消息处理器
                mqttHandlerManager.RegisterHandler(new DefaultMqttMessageHandler());

                // 连接到MQTT服务器
                await mqttService.ConnectAsync();
                Debug.Log("MQTT服务已启动");
            }
            catch (Exception ex)
            {
                Debug.LogError($"MQTT服务启动失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task InitializeIotDevices()
        {
            try
            {
                if (thingManager == null)
                {
                    Debug.LogError("物联网管理器未初始化");
                    return;
                }

                // 发送设备描述
                if (!string.IsNullOrEmpty(sessionId))
                {
                    // 添加设备
                    thingManager.ID = sessionId;
                    thingManager.AddThing(new XiaozhiAI.Models.IoT.Things.Lamp());
                    thingManager.AddThing(new XiaozhiAI.Models.IoT.Things.Speaker());
                    thingManager.AddThing(new XiaozhiAI.Models.IoT.Things.Camera());
                    thingManager.AddThing(new XiaozhiAI.Models.IoT.Things.DuoJi());
                    thingManager.AddThing(new XiaozhiAI.Models.IoT.Things.Screenctr());
                    thingManager.AddThing(new XiaozhiAI.Models.IoT.Things.OpenApp());
                    Debug.Log("发送IoT设备描述");
                    await SendIotDescriptors();
                }
                else
                {
                    Debug.LogWarning("Session ID为空，跳过物联网设备初始化");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"物联网设备初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task ConnectWebSocket()
        {
            Debug.LogWarning("ConnectWebSocket");
            await wsManager.ConnectAsync(
                string.IsNullOrEmpty(this.url) ? config.wsUrl : this.url,
                config.accessToken,
                string.IsNullOrEmpty(this.deviceMac) ? config.deviceMac : this.deviceMac,
                config.deviceUuid,
                HandleMessage
            );
            await SendHelloMessage();
        }

        public async Task SendHelloMessage()
        {
            var helloMsg = new
            {
                type = "hello",
                version = 1,
                transport = "websocket",
                audio_params = new
                {
                    format = "opus",
                    sample_rate = 16000,  // 更新为录音采样率
                    channels = 1,
                    frame_duration = 60   // 更新为60ms帧大小
                }
            };
            string json = JsonConvert.SerializeObject(helloMsg);
            await wsManager.SendMessageAsync(json);
        }

        public async Task SendDectMessage(string msg)
        {
            var helloMsg = new
            {
                type = "listen",
                state = "detect",
                text = msg,

            };
            string json = JsonConvert.SerializeObject(helloMsg);
            await wsManager.SendMessageAsync(json);
        }

        //接收服务端消息
        private async void HandleMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogWarning("收到空消息");
                return;
            }

            Debug.LogWarning("AI:::" + message);
            try
            {
                var msg = JObject.Parse(message);
                if (msg == null)
                {
                    Debug.LogError("消息解析失败");
                    return;
                }

                var msgType = msg["type"]?.ToString();
                if (string.IsNullOrEmpty(msgType))
                {
                    Debug.LogError("消息类型为空");
                    return;
                }

                if (msgType == "hello")
                {
                    sessionId = msg["session_id"]?.ToString();
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        Debug.LogError("session_id为空");
                        return;
                    }
                    await StartListening();
                }
                else if (msgType == "llm")
                {
                    var emotion = msg["emotion"]?.ToString();
                    if (!string.IsNullOrEmpty(emotion))
                    {
                        var t = $"[AI emotion]:\n【{emotion}】";
                        Debug.Log($"AI emotion: {emotion}");
                        // TODO: 通过事件或其他方式通知UI更新
                        // var getcmaer = GameObject.Find("AI_(Clone)")?.GetComponentInChildren<Getcmaer>();
                        // if (getcmaer != null)
                        // {
                        //     getcmaer.settext(t);
                        // }
                        // var hotGameStart = GameObject.Find("HotGameStart(Clone)")?.GetComponent<HotGameStart>();
                        // if (hotGameStart != null)
                        // {
                        //     hotGameStart.AnimationCtrLive2D(emotion);
                        // }
                    }
                }
                else if (msgType == "stt")
                {
                    var text = msg["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var t = "[用户]:\n" + text;
                        Debug.Log($"用户: {text}");
                        // TODO: 通过事件或其他方式通知UI更新
                        // var getcmaer = GameObject.Find("AI_(Clone)")?.GetComponentInChildren<Getcmaer>();
                        // if (getcmaer != null)
                        // {
                        //     getcmaer.settext(t);
                        //     getcmaer.Addplayerchat(text);
                        // }
                    }
                }
                else if (msgType == "tts")
                {
                    var state = msg["state"]?.ToString();
                    if (string.IsNullOrEmpty(state))
                    {
                        Debug.LogError("tts状态为空");
                        return;
                    }

                    ttsState = state;
                    if (state == "start" || state == "sentence_start")
                    {
                        if (audioManager != null && audioManager.isRecording)
                        {
                            isPlaying = true;
                            audioManager.StopRecording();
                        }
                        var text = msg["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var t = "[AI]:\n" + text;
                            Debug.Log($"AI: {text}");
                            // TODO: 通过事件或其他方式通知UI更新
                            // var getcmaer = GameObject.Find("AI_(Clone)")?.GetComponentInChildren<Getcmaer>();
                            // if (getcmaer != null)
                            // {
                            //     getcmaer.settext(t);
                            //     getcmaer.AddAIchat(text);
                            // }
                        }
                    }
                    else if (state == "stop")
                    {
                        Debug.Log($"TTS播放结束，进入冷却期");
                        isPlaying = false;
                        lastTtsEndTime = Time.time;
                        isInCooldown = true;
                        await DelayedStartListening();
                    }
                }
                else if (msgType == "goodbye")
                {
                    sessionId = null;
                    listenState = "stop";
                }
                else if (msgType == "iot")
                {
                    HandleIotMessage(msg);
                }
            }
            catch (JsonException ex)
            {
                Debug.LogError($"JSON解析错误: {ex.Message}\n消息内容: {message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"消息处理错误: {ex.Message}\n{ex.StackTrace}\n消息内容: {message}");
            }
        }


        // 添加延迟启动监听的方法
        private async Task DelayedStartListening()
        {
            // 等待冷却时间
            await Task.Delay((int)ttsCooldownTime * 1000);
            isInCooldown = false;
            Debug.Log("冷却期结束，重新开始监听");
            await StartListening();
        }

        private async Task StartListening()
        {
            if (!isManualMode)
            {
                listenState = "start";
                var listenMsg = new
                {
                    session_id = sessionId,
                    type = "listen",
                    state = "start",
                    mode = "auto"
                };
                string json = JsonConvert.SerializeObject(listenMsg);
                await wsManager.SendMessageAsync(json);
            }
        }

        public async Task Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                await OnSpaceKeyPress();
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                await OnSpaceKeyRelease();
            }
        }

        public async Task OnSpaceKeyPress()
        {
            if (audioManager == null)
            {
                return;
            }
            // 重置播放缓冲
            audioManager.ResetPlayback();
            if (ttsState == "stop")
            {
                Debug.LogWarning($"重新链接");
                await ConnectWebSocket();
            }
            if (ttsState == "start" || ttsState == "sentence_start")
            {
                await wsManager.SendMessageAsync(JsonConvert.SerializeObject(new { type = "abort" }));
                isPlaying = false;
            }

            if (isManualMode)
            {
                listenState = "start";
                var listenMsg = new
                {
                    session_id = sessionId,
                    type = "listen",
                    state = "start",
                    mode = "manual"
                };
                string json = JsonConvert.SerializeObject(listenMsg);
                await wsManager.SendMessageAsync(json);
            }
        }

        private async Task OnSpaceKeyRelease()
        {
            keyState = "release";
            if (isManualMode)
            {
                await StopListening();
            }
        }

        private async Task StopListening()
        {
            listenState = "stop";
            var listenMsg = new
            {
                session_id = sessionId,
                type = "listen",
                state = "stop"
            };
            string json = JsonConvert.SerializeObject(listenMsg);
            await wsManager.SendMessageAsync(json);
        }

        private async Task SendAudioCoroutine()
        {
            // 检查是否有可用的麦克风
            bool hasMicrophone = Microphone.devices.Length > 0;

            if (hasMicrophone)
            {
                // 初始启动录音
                audioManager.StartRecording();
                Debug.Log($"开始录音，使用麦克风: {Microphone.devices[0]}");
            }
            else
            {
                Debug.LogWarning("未检测到麦克风设备，录音功能将不可用，但仍可接收服务器音频");

                // 在PC平台上，如果没有检测到麦克风，可以提示用户检查系统设置
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
                Debug.LogWarning("请检查系统麦克风设置，确保麦克风已连接并启用");
#endif
            }

            int lastPosition = 0;
            List<float> accumulatedSamples = new List<float>(); // 用于累积样本
            try
            {
                while (true)
                {
                    // 修改这里，不要在没有麦克风时直接返回
                    if (!hasMicrophone)
                    {
                        Debug.Log("麦克风不存在，但继续监听服务器音频");
                        await Task.Yield();
                        continue;
                    }

                    // 当处于播放状态时，确保录音停止，避免捕获回音
                    if (isPlaying)
                    {
                        if (hasMicrophone && audioManager.isRecording)
                        {
                            audioManager.StopRecording();
                            accumulatedSamples.Clear();
                            lastPosition = 0;
                            isSpeaking = false;
                            currentSilenceFrames = 0;
                        }
                        await Task.Yield();
                        continue;
                    }
                    else
                    {
                        // 如果当前不在播放且有麦克风且录音已停止，则重启录音
                        if (hasMicrophone && !audioManager.isRecording)
                        {
                            audioManager.StartRecording();
                            lastPosition = 0;
                            accumulatedSamples.Clear();
                            isSpeaking = false;
                            currentSilenceFrames = 0;
                        }
                    }

                    if (listenState == "start")
                    {
                        int position = Microphone.GetPosition(null);
                        if (position < lastPosition)
                        {
                            // 录音剪辑已循环：先读取从 lastPosition 到剪辑末尾的数据
                            int samplesToEnd = audioManager.recordingClip.samples - lastPosition;
                            float[] tailSamples = new float[samplesToEnd];
                            audioManager.recordingClip.GetData(tailSamples, lastPosition);
                            accumulatedSamples.AddRange(tailSamples);

                            // 再读取从剪辑开始到当前位置的数据
                            if (position > 0)
                            {
                                float[] headSamples = new float[position];
                                audioManager.recordingClip.GetData(headSamples, 0);
                                accumulatedSamples.AddRange(headSamples);
                            }
                            lastPosition = position;
                        }
                        else if (position > lastPosition)
                        {
                            // 正常情况：读取从 lastPosition 到 position 的数据
                            int samplesToRead = position - lastPosition;
                            float[] samples = new float[samplesToRead];
                            audioManager.recordingClip.GetData(samples, lastPosition);
                            accumulatedSamples.AddRange(samples);
                            lastPosition = position;

                            // 当累积样本达到或超过帧大小时，进行编码并发送
                            while (accumulatedSamples.Count >= audioManager.recordFrameSize * audioManager.channels)
                            {
                                float[] frameSamples = accumulatedSamples.GetRange(0, audioManager.recordFrameSize * audioManager.channels).ToArray();
                                accumulatedSamples.RemoveRange(0, audioManager.recordFrameSize * audioManager.channels);

                                // 添加VAD检测
                                if (useVAD && !isManualMode)
                                {
                                    // 如果在冷却期内，跳过VAD检测
                                    if (isInCooldown)
                                    {
                                        // 在冷却期内不进行VAD检测
                                        continue;
                                    }

                                    bool hasVoice = DetectVoiceActivity(frameSamples);

                                    if (hasVoice)
                                    {
                                        // 检测到语音
                                        currentSilenceFrames = 0;
                                        if (!isSpeaking)
                                        {
                                            // 检查是否刚从TTS播放结束不久
                                            if (Time.time - lastTtsEndTime < ttsCooldownTime * 3)
                                            {
                                                Debug.Log($"VAD: TTS结束后{Time.time - lastTtsEndTime:F2}秒内检测到声音，可能是回音，忽略");
                                                continue;
                                            }

                                            isSpeaking = true;
                                            Debug.Log("VAD: 检测到语音开始");
                                        }
                                    }
                                    else
                                    {
                                        // 未检测到语音
                                        currentSilenceFrames++;

                                        // 如果之前在说话，且静音超过阈值，认为说话结束
                                        if (isSpeaking && currentSilenceFrames > vadSilenceFrames)
                                        {
                                            isSpeaking = false;
                                            Debug.Log($"VAD: 检测到语音结束，静音帧数: {currentSilenceFrames}");

                                            // 自动停止监听并发送停止命令
                                            if (listenState == "start")
                                            {
                                                await StopListening();
                                            }
                                        }
                                    }
                                }

                                byte[] opusData = audioManager.EncodeAudio(frameSamples);
                                if (opusData != null) // 检查编码是否成功
                                {
                                    await wsManager.SendBinaryAsync(opusData);
                                }
                            }
                        }
                    }
                    await Task.Yield();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        // 添加VAD检测方法
        private bool DetectVoiceActivity(float[] samples)
        {
            // 简单的能量检测方法
            float energy = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                energy += samples[i] * samples[i];
            }
            energy /= samples.Length;

            // 在TTS播放结束后的一段时间内，提高VAD阈值，减少误触发
            float currentThreshold = vadThreshold;
            if (Time.time - lastTtsEndTime < ttsCooldownTime * 2)
            {
                // 在冷却期后的一段时间内，使用更高的阈值
                currentThreshold = vadThreshold * 1.5f;

                // 如果能量值接近阈值但未超过，记录日志但不触发
                if (energy > vadThreshold && energy <= currentThreshold)
                {
                    Debug.Log($"VAD: 冷却期内检测到低能量声音，能量值: {energy:F5}，当前阈值: {currentThreshold:F5}");
                }
            }

            // 判断能量是否超过阈值
            bool hasVoice = energy > currentThreshold;

            // 可以添加调试信息，帮助调整阈值
            if (hasVoice)
            {
                Debug.Log($"VAD: 检测到声音，能量值: {energy:F5}，当前阈值: {currentThreshold:F5}");
            }

            return hasVoice;
        }


        /// <summary>
        /// 发送 Iot设备描述同步到服务端
        /// </summary>
        /// <returns></returns>
        private async Task SendIotDescriptors()
        {
            try
            {
                var thingManager = ThingManager.GetInstance();
                var descriptorsJson = thingManager.GetDescriptorsJson();

                // 解析为对象
                var descriptorsObj = JObject.Parse(descriptorsJson);

                // 添加 session_id
                descriptorsObj["session_id"] = sessionId;

                // 直接发送对象，而不是字符串
                await wsManager.SendMessageAsync(descriptorsObj.ToString(Formatting.None));

                Debug.Log($"已发送IoT设备描述\n{descriptorsObj}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"发送IoT设备描述失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理物联Iot网消息
        /// </summary>
        /// <param name="data">带有iot的json数据</param>
        private async void HandleIotMessage(JObject data)
        {
            try
            {
                // 检查消息类型
                var type = data["type"]?.ToString();
                if (type != "iot")
                {
                    Debug.LogError($"非物联网消息类型: {type}");
                    return;
                }

                // 获取命令数组
                var commands = data["commands"] as JArray;
                if (commands == null || commands.Count == 0)
                {
                    Debug.LogError("物联网命令为空或格式不正确");
                    return;
                }

                foreach (JObject command in commands)
                {
                    try
                    {
                        // 记录接收到的命令
                        var mes = command.ToString(Newtonsoft.Json.Formatting.None);

                        // 如果MQTT服务已初始化，则发布消息
                        mqttService?.PublishAsync(mes).ConfigureAwait(false);

                        Debug.Log($"收到物联网命令: {mes}");

                        // 执行命令
                        var result = await thingManager.Invoke(command);
                        Debug.Log($"执行物联网命令结果: {result}");

                        // 命令执行后更新设备状态
                        UpdateIotStates();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"执行物联网命令失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理物联网消息失败: {ex.Message}");
            }
        }
        private void UpdateIotStates()
        {
            try
            {
                if (thingManager == null)
                {
                    Debug.LogError("ThingManager未初始化，无法更新状态");
                    return;
                }

                // 获取当前设备状态
                string statesJson = thingManager.GetStatesJson();
                if (string.IsNullOrEmpty(statesJson))
                {
                    Debug.LogError("获取设备状态失败，返回空JSON");
                    return;
                }

                // 发送状态更新
                if (wsManager != null)
                {
                    wsManager.SendMessageAsync(statesJson).ConfigureAwait(false);
                    Debug.Log("物联网设备状态已更新");
                }
                else
                {
                    Debug.LogError("WebSocket管理器未初始化，无法发送状态更新");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"更新物联网状态失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task RequestMicrophonePermission()
        {
            Debug.Log($"请求麦克风权限，当前平台: {Application.platform}");

#if PLATFORM_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);

                // 等待用户响应权限请求
                float timeWaited = 0;
                while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && timeWaited < 5.0f)
                {
                    await Awaitable.WaitForSecondsAsync(0.1f);
                    timeWaited += 0.1f;
                }

                if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    Debug.Log("麦克风权限已获取");
                }
                else
                {
                    Debug.LogWarning("用户拒绝了麦克风权限");
                }
            }
            else
            {
                Debug.Log("已有麦克风权限");
            }
#elif PLATFORM_IOS
            // iOS平台上，我们需要检查麦克风权限
            bool hasPermission = false;
            
            try
            {
                // 检查是否已经有麦克风设备
                if (Microphone.devices.Length > 0)
                {
                    // 尝试开始录音，这会触发权限请求
                    string deviceName = Microphone.devices[0];
                    int minFreq, maxFreq;
                    Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
                    
                    // 创建一个短暂的录音来触发权限请求
                    AudioClip tempClip = Microphone.Start(deviceName, false, 1, 16000);
                    
                    // 等待一小段时间，确保权限对话框显示并处理
                    float timeWaited = 0;
                    while (Microphone.IsRecording(deviceName) && timeWaited < 1.0f)
                    {
                        await Awaitable.WaitForSecondsAsync(0.1f);
                        timeWaited += 0.1f;
                    }
                    
                    // 停止临时录音
                    Microphone.End(deviceName);
                    UnityEngine.Object.Destroy(tempClip);
                    
                    // 再次检查是否有麦克风设备，如果有，则认为权限已获取
                    hasPermission = Microphone.devices.Length > 0;
                    
                    if (hasPermission)
                    {
                        Debug.Log("iOS 麦克风权限已获取");
                    }
                    else
                    {
                        Debug.LogWarning("iOS 用户可能拒绝了麦克风权限");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"iOS 麦克风权限请求失败: {ex.Message}");
            }
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            // PC平台（Windows、macOS、Linux）的处理
            Debug.Log("PC平台检查麦克风");
            
            if (Microphone.devices.Length > 0)
            {
                Debug.Log($"检测到 {Microphone.devices.Length} 个麦克风设备:");
                for (int i = 0; i < Microphone.devices.Length; i++)
                {
                    Debug.Log($"  {i+1}. {Microphone.devices[i]}");
                }
                
                // 在PC平台上，我们可以尝试获取麦克风设备的能力
                string deviceName = Microphone.devices[0];
                int minFreq, maxFreq;
                Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
                
                Debug.Log($"麦克风 '{deviceName}' 支持的频率范围: {minFreq}Hz - {maxFreq}Hz");
                Debug.Log("PC平台麦克风权限检查完成");
            }
            else
            {
                Debug.LogWarning("未检测到麦克风设备，录音功能将不可用");
            }
#else
            // 其他平台上，我们假设已经有权限
            Debug.Log("其他平台，假设已有麦克风权限");
#endif
        }

        private async Task RequestCameraPermission()
        {
            Debug.Log($"请求摄像头权限，当前平台: {Application.platform}");

#if PLATFORM_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);

                // 等待用户响应权限请求
                float timeWaited = 0;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && timeWaited < 5.0f)
                {
                    await Awaitable.WaitForSecondsAsync(0.1f);
                    timeWaited += 0.1f;
                }

                if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    Debug.Log("摄像头权限已获取");
                }
                else
                {
                    Debug.LogWarning("用户拒绝了摄像头权限");
                }
            }
            else
            {
                Debug.Log("已有摄像头权限");
            }
#elif PLATFORM_IOS
            // iOS平台上，我们需要在Info.plist中添加NSCameraUsageDescription
            bool hasPermission = false;
            
            try
            {
                // 在iOS上，尝试获取摄像头设备列表会触发权限请求
                WebCamDevice[] devices = WebCamTexture.devices;
                
                // 等待一小段时间，确保权限对话框显示并处理
                await Awaitable.WaitForSecondsAsync(0.5f);
                
                // 再次检查是否能获取设备列表
                devices = WebCamTexture.devices;
                hasPermission = devices.Length > 0;
                
                if (hasPermission)
                {
                    Debug.Log("iOS 摄像头权限已获取");
                }
                else
                {
                    Debug.LogWarning("iOS 用户可能拒绝了摄像头权限");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"iOS 摄像头权限请求失败: {ex.Message}");
            }
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            // PC平台（Windows、macOS、Linux）的处理
            Debug.Log("PC平台检查摄像头");
            
            try
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length > 0)
                {
                    Debug.Log($"检测到 {devices.Length} 个摄像头设备:");
                    for (int i = 0; i < devices.Length; i++)
                    {
                        Debug.Log($"  {i+1}. {devices[i].name}");
                    }
                }
                else
                {
                    Debug.LogWarning("未检测到摄像头设备");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"PC平台摄像头检查失败: {ex.Message}");
            }
#else
            // 其他平台上，我们假设已经有权限
            Debug.Log("其他平台，假设已有摄像头权限");
#endif
        }

        public void Destroy()
        {
            if (wsManager != null)
            {
                wsManager.DisconnectAsync();
            }
            if (audioManager != null)
            {
                audioManager.StopRecording();
                audioManager.Dispose();
            }
        }
    }
}
