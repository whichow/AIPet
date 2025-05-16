using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Vonweller
{
    public class OpusCodec : IDisposable
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string OpusLib = "__Internal";
#elif UNITY_ANDROID && !UNITY_EDITOR
        private const string OpusLib = "libopus";  // libopus.so will be loaded
#else
        private const string OpusLib = "opus";  // opus.dll on Windows, libopus.so on Linux/Android
#endif
        private const CallingConvention OpusCallingConvention = CallingConvention.Cdecl;

        #region Native Methods
        private enum OpusErrors
        {
            Ok = 0,
            BadArg = -1,
            BufferTooSmall = -2,
            InternalError = -3,
            InvalidPacket = -4,
            Unimplemented = -5,
            InvalidState = -6,
            AllocFail = -7
        }

        private enum Application
        {
            Voip = 2048,
            Audio = 2049,
            RestrictedLowLatency = 2051
        }

        private enum Ctl
        {
            SetBitrateRequest = 4002,
            GetBitrateRequest = 4003,
            SetInbandFECRequest = 4012,
            GetInbandFECRequest = 4013,
            SetPacketLossPercRequest = 4014,
            GetPacketLossPercRequest = 4015,
            ResetState = 4028
        }

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out int error);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern void opus_encoder_destroy(IntPtr encoder);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern void opus_decoder_destroy(IntPtr decoder);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_encode_float(IntPtr encoder, float[] pcm, int frame_size, byte[] data, int max_data_bytes);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_decode_float(IntPtr decoder, byte[] data, int len, float[] pcm, int frame_size, int decode_fec);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_encoder_ctl(IntPtr encoder, int request, int value);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_decoder_ctl(IntPtr decoder, int request, int value);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern void opus_pcm_soft_clip(IntPtr pcm, int frameSize, int channels, float[] softClipMem);
        #endregion

        private readonly IntPtr _encoder;
        private readonly IntPtr _decoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        private readonly float[] _softClipMem;
        private bool _disposed;

        public OpusCodec(
            int sampleRate = 48000,
            int channels = 1,
            int frameSize = 960) // 20ms at 48kHz
        {
            if (sampleRate != 8000 && sampleRate != 12000 && sampleRate != 16000 && sampleRate != 24000 && sampleRate != 48000)
                throw new ArgumentException("Sample rate must be one of: 8000, 12000, 16000, 24000, 48000");
            if (channels != 1 && channels != 2)
                throw new ArgumentException("Channel count must be 1 or 2");

            _sampleRate = sampleRate;
            _channels = channels;
            _frameSize = frameSize;
            _softClipMem = new float[channels];

            try
            {
                // 创建编码器
                int encodeError;
                _encoder = opus_encoder_create(sampleRate, channels, (int)Application.Voip, out encodeError);
                if (encodeError != (int)OpusErrors.Ok || _encoder == IntPtr.Zero)
                {
                    throw new Exception($"Opus编码器创建失败: {(OpusErrors)encodeError}");
                }

                // 设置编码器参数
                opus_encoder_ctl(_encoder, (int)Ctl.SetBitrateRequest, 24000); // 24kbps
                opus_encoder_ctl(_encoder, (int)Ctl.SetInbandFECRequest, 1);  // 启用FEC
                opus_encoder_ctl(_encoder, (int)Ctl.SetPacketLossPercRequest, 10); // 10%丢包率

                // 创建解码器
                int decodeError;
                _decoder = opus_decoder_create(sampleRate, channels, out decodeError);
                if (decodeError != (int)OpusErrors.Ok || _decoder == IntPtr.Zero)
                {
                    opus_encoder_destroy(_encoder);
                    throw new Exception($"Opus解码器创建失败: {(OpusErrors)decodeError}");
                }

                Debug.Log($"Opus编解码器初始化完成:\n" +
                         $"采样率: {sampleRate}Hz\n" +
                         $"通道数: {channels}\n" +
                         $"帧大小: {frameSize} samples");
            }
            catch (Exception e)
            {
                Debug.LogError($"Opus编解码器初始化失败: {e.Message}");
                throw;
            }
        }

        public byte[] Encode(float[] pcmData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusCodec));

            if (pcmData == null || pcmData.Length == 0)
            {
                Debug.LogError("PCM数据为空");
                return null;
            }

            try
            {
                // 检查输入数据长度
                if (pcmData.Length != _frameSize * _channels)
                {
                    Debug.LogWarning($"PCM数据长度不匹配：{pcmData.Length} vs {_frameSize * _channels}，将进行调整");
                    float[] resizedData = new float[_frameSize * _channels];
                    int copyLength = Math.Min(pcmData.Length, resizedData.Length);
                    Array.Copy(pcmData, resizedData, copyLength);
                    pcmData = resizedData;
                }

                // 应用软限幅
                unsafe
                {
                    fixed (float* pcmPtr = pcmData)
                    {
                        opus_pcm_soft_clip((IntPtr)pcmPtr, _frameSize, _channels, _softClipMem);
                    }
                }

                // 创建编码缓冲区
                byte[] encodedBuffer = new byte[1275]; // 最大Opus帧大小
                int encodedBytes = opus_encode_float(_encoder, pcmData, _frameSize, encodedBuffer, encodedBuffer.Length);

                if (encodedBytes < 0)
                {
                    throw new Exception($"编码失败: {(OpusErrors)encodedBytes}");
                }

                // 返回实际编码的数据
                byte[] result = new byte[encodedBytes];
                Array.Copy(encodedBuffer, result, encodedBytes);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"编码异常：{e.Message}");
                return null;
            }
        }

        public float[] Decode(byte[] opusData, bool decodeFEC = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusCodec));

            if (opusData == null || opusData.Length == 0)
            {
                Debug.LogError("Opus数据为空");
                return null;
            }

            try
            {
                // 创建解码缓冲区
                float[] decodedBuffer = new float[_frameSize * _channels];
                int decodedSamples = opus_decode_float(_decoder, opusData, opusData.Length, decodedBuffer, _frameSize, decodeFEC ? 1 : 0);

                if (decodedSamples < 0)
                {
                    throw new Exception($"解码失败: {(OpusErrors)decodedSamples}");
                }

                // 返回实际解码的数据
                if (decodedSamples != _frameSize)
                {
                    float[] result = new float[decodedSamples * _channels];
                    Array.Copy(decodedBuffer, result, result.Length);
                    return result;
                }

                return decodedBuffer;
            }
            catch (Exception e)
            {
                Debug.LogError($"解码异常：{e.Message}");
                return null;
            }
        }

        public void Reset()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusCodec));

            // 重置编码器和解码器状态
            opus_encoder_ctl(_encoder, (int)Ctl.ResetState, 0);
            opus_decoder_ctl(_decoder, (int)Ctl.ResetState, 0);
            Array.Clear(_softClipMem, 0, _softClipMem.Length);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_encoder != IntPtr.Zero)
                opus_encoder_destroy(_encoder);
            if (_decoder != IntPtr.Zero)
                opus_decoder_destroy(_decoder);

            _disposed = true;
        }
    }
} 