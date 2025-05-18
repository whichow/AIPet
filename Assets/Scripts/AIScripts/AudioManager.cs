using System;
using System.Collections.Generic;
using UnityEngine;
using Vonweller;
using System.Collections;
using System.Threading.Tasks;

public class AudioManager
{
    [Header("音频设置")]
    public int recordSampleRate = 16000;  // 录音采样率
    public int playSampleRate = 24000;    // 播放采样率
    public int channels = 1;              // 通道数
    public int recordFrameSize = 960;     // 录音帧大小（60ms at 16kHz）
    public int playFrameSize = 1440;      // 播放帧大小（60ms at 24kHz）

    // 音频属性
    public AudioClip recordingClip { get; private set; }
    public bool isRecording = false;

    // 编解码器
    private OpusCodec _recordCodec;  // 用于录音编码
    private OpusCodec _playCodec;    // 用于播放解码

    // 播放相关
    public AudioSource audioSource;
    private AudioClip streamingClip;
    private List<float> playbackBuffer = new List<float>();
    private bool isNewSession = false;
    private object bufferLock = new object();

    /// <summary>
    /// 初始化音频管理器
    /// </summary>
    public void AudioManagerInit()
    {
        InitializeCodecs();
        InitStreamingPlayback();
    }

    /// <summary>
    /// 初始化Opus编解码器
    /// </summary>
    private void InitializeCodecs()
    {
        try
        {
            // 创建录音编码器
            _recordCodec = new OpusCodec(
                sampleRate: recordSampleRate,
                channels: channels,
                frameSize: recordFrameSize
            );

            // 创建播放解码器
            _playCodec = new OpusCodec(
                sampleRate: playSampleRate,
                channels: channels,
                frameSize: playFrameSize
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"Opus编解码器初始化失败: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 初始化流式播放
    /// </summary>
    private void InitStreamingPlayback()
    {
        if (audioSource == null)
        {
            GameObject audioSourceObj = GameObject.FindObjectOfType<AudioSource>().gameObject;
            if (audioSourceObj == null)
            {
                Debug.LogWarning("未找到AudioSource对象，创建新对象");
                audioSourceObj = new GameObject("AudioSource");
                audioSource = audioSourceObj.AddComponent<AudioSource>();
            }
            else
            {
                audioSource = audioSourceObj.GetComponent<AudioSource>();
            }
        }

        if (streamingClip == null)
        {
            // 增加缓冲区长度到5秒，以提供更大的缓冲空间
            int bufferLength = playSampleRate * 1; // 修改为1秒缓冲
            streamingClip = AudioClip.Create("StreamingPlayback", bufferLength, channels, playSampleRate, true, OnAudioRead);
            audioSource.clip = streamingClip;
            audioSource.loop = true;
            audioSource.volume = 1f;
            audioSource.mute = false;
            
            // 等待缓冲区有足够数据后再开始播放
            WaitForBufferAndPlay();
        }
    }

    private async void WaitForBufferAndPlay()
    {
        // 等待缓冲区有至少1秒的数据
        while (playbackBuffer.Count < playSampleRate)
        {
            await Task.Yield();
        }
        audioSource.Play();
    }

    private void OnAudioRead(float[] data)
    {
        lock (bufferLock)
        {
            if (isNewSession)
            {
                playbackBuffer.Clear();
                isNewSession = false;
            }

            // 从缓冲区复制数据到目标数组
            int count = Mathf.Min(data.Length, playbackBuffer.Count);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    data[i] = playbackBuffer[i];
                }
                playbackBuffer.RemoveRange(0, count); // 移除已播放的数据
            }
            else
            {
                // 没有数据时输出静音，但添加淡入淡出效果
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
            }
        }
    }

    /// <summary>
    /// 开始录音
    /// </summary>
    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("未检测到麦克风设备，无法录音");
            return;
        }

        try
        {
            recordingClip = Microphone.Start(null, true, 10, recordSampleRate);
            isRecording = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"录音失败: {e.Message}");
        }
    }

    /// <summary>
    /// 停止录音
    /// </summary>
    public void StopRecording()
    {
        if (isRecording)
        {
            Microphone.End(null);
            isRecording = false;
            Debug.Log("停止录音");
        }
    }

    /// <summary>
    /// 获取当前录音数据
    /// </summary>
    public float[] GetRecordedData(int sampleCount)
    {
        if (!isRecording || recordingClip == null)
        {
            return null;
        }

        float[] data = new float[sampleCount];
        int position = Microphone.GetPosition(null);

        if (position < 0 || recordingClip.samples <= 0)
        {
            return null;
        }

        // 计算从哪个位置开始读取数据
        int startPosition = (position - sampleCount) % recordingClip.samples;
        if (startPosition < 0) startPosition += recordingClip.samples;

        recordingClip.GetData(data, startPosition);

        // 如果数据长度不匹配，进行裁剪
        if (data.Length != recordFrameSize * channels)
        {
            float[] resizedData = new float[recordFrameSize * channels];
            int copyLength = Math.Min(data.Length, resizedData.Length);
            Array.Copy(data, resizedData, copyLength);
            data = resizedData;
        }

        return data;
    }

    /// <summary>
    /// 编码音频数据
    /// </summary>
    public byte[] EncodeAudio(float[] pcmData)
    {
        try
        {
            if (pcmData == null || pcmData.Length == 0)
            {
                Debug.LogError("PCM数据为空");
                return null;
            }

            return _recordCodec.Encode(pcmData);
        }
        catch (Exception e)
        {
            Debug.LogError($"编码异常：{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 解码Opus音频数据
    /// </summary>
    public float[] DecodeAudio(byte[] opusData, bool decodeFEC = false)
    {
        if (opusData == null || opusData.Length == 0)
        {
            Debug.LogError("Opus数据为空");
            return null;
        }

        try
        {
            return _playCodec.Decode(opusData, decodeFEC);
        }
        catch (Exception e)
        {
            Debug.LogError($"解码异常：{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 重置播放缓冲区
    /// </summary>
    public void ResetPlayback()
    {
        lock (bufferLock)
        {
            playbackBuffer.Clear();
            isNewSession = true;
            Debug.Log("播放缓冲区已重置");
        }
    }

    /// <summary>
    /// 播放音频数据
    /// </summary>
    public void PlayAudio(float[] pcmData)
    {
        if (pcmData == null || pcmData.Length == 0)
        {
            Debug.LogWarning("尝试播放空音频数据");
            return;
        }

        lock (bufferLock)
        {            
            playbackBuffer.AddRange(pcmData);

            // 增加最大缓冲区大小到10秒
            int maxBufferSize = playSampleRate * 10; // 10秒缓冲
            if (playbackBuffer.Count > maxBufferSize)
            {
                int removeCount = playbackBuffer.Count - maxBufferSize;
                playbackBuffer.RemoveRange(0, removeCount);
                Debug.LogWarning($"缓冲区过大，移除了{removeCount}个样本");
            }
        }
    }

    public void Dispose()
    {
        _recordCodec?.Dispose();
        _playCodec?.Dispose();
    }
}

