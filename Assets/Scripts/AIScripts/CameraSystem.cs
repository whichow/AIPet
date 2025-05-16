///=====================================================
/// - FileName:      CameraSystem.cs
/// - NameSpace:     Vonweller
/// - Description:   高级定制脚本生成
/// - Creation Time: 2025/3/8 12:35:57
/// -  (C) Copyright 2008 - 2025
/// -  All Rights Reserved.
///=====================================================
using UnityEngine;
using System;
using UnityEngine.UI;
using System.Threading.Tasks;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

namespace Vonweller
{
    public class CameraSystem
    {
        public int cameraIndex = 0;
        public int frameWidth = 320;
        public int frameHeight = 240;
        public int fps = 30;
        private WebCamTexture webCamTexture;
        private bool cameraPermissionGranted = false;


        /// <summary>
        /// 请求摄像头权限
        /// </summary>
        public async Task RequestCameraPermission()
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
                    cameraPermissionGranted = true;
                }
                else
                {
                    Debug.LogWarning("用户拒绝了摄像头权限");
                    cameraPermissionGranted = false;
                }
            }
            else
            {
                Debug.Log("已有摄像头权限");
                cameraPermissionGranted = true;
            }
#elif PLATFORM_IOS
            // iOS平台上，我们需要在Info.plist中添加NSCameraUsageDescription
            // 尝试访问摄像头来触发系统权限请求
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
                    cameraPermissionGranted = true;
                }
                else
                {
                    Debug.LogWarning("iOS 用户可能拒绝了摄像头权限");
                    cameraPermissionGranted = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"iOS 摄像头权限请求失败: {ex.Message}");
                cameraPermissionGranted = false;
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
                    cameraPermissionGranted = true;
                }
                else
                {
                    Debug.LogWarning("未检测到摄像头设备");
                    cameraPermissionGranted = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"PC平台摄像头检查失败: {ex.Message}");
                cameraPermissionGranted = false;
            }
#else
            // 其他平台上，我们假设已经有权限
            Debug.Log("其他平台，假设已有摄像头权限");
            cameraPermissionGranted = true;
#endif
        }

        /// <summary>
        /// 启动摄像头
        /// </summary>
        public async Task<(bool,string)> StartCamera()
        {
            try
            {
                // 先请求权限
                await RequestCameraPermission();

                if (!cameraPermissionGranted)
                {
                    Debug.LogError("无法启动摄像头：未获得权限");
                    return (false, "无法启动摄像头：未获得权限");
                }

                if (webCamTexture != null && webCamTexture.isPlaying)
                {
                    Debug.LogWarning("摄像头已在运行");
                    return (false, "摄像头已在运行");
                }

                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length > 0)
                {
                    if (cameraIndex >= devices.Length)
                        cameraIndex = 0;

                    webCamTexture = new WebCamTexture(devices[cameraIndex].name, frameWidth, frameHeight, fps);
                    webCamTexture.Play();
                    Debug.Log("摄像头启动");

                    // 查找UI元素并设置纹理
                    // var rawImage = GameObject.Find("AI_(Clone)").GetComponentInChildren<Getcmaer>().UICamera;
                    // if (rawImage != null)
                    // {
                    //     rawImage.transform.gameObject.SetActive(true);
                    //     if (rawImage != null)
                    //     {
                    //         rawImage.texture = webCamTexture;
                    //     }
                    //     else
                    //     {
                    //         Debug.LogWarning("未找到RawImage组件");
                    //         return (false, "未找到RawImage组件");
                    //     }
                    // }
                    // else
                    // {
                    //     Debug.LogWarning("未找到'摄像头画面'对象");
                    // }
                }
                else
                {
                    Debug.LogError("未找到摄像头设备");
                    return (false, "未找到摄像头设备");
                }
                return (true, "摄像头启动成功");
            }
            catch (Exception e)
            {
                Debug.LogError("摄像头启动失败：" + e.Message);
                return (false, "摄像头启动失败" + e.Message);
            }
        }

        /// <summary>
        /// 停止摄像头
        /// </summary>
        public void StopCamera()
        {
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                // GameObject.Find("AI_(Clone)").GetComponentInChildren<Getcmaer>().UICamera.gameObject.SetActive(false);
                webCamTexture.Stop();
                Debug.Log("摄像头停止");
            }
        }

        /// <summary>
        /// 捕捉当前帧，并转换为 Base64 字符串
        /// </summary>
        public string CaptureFrameToBase64()
        {
            if (webCamTexture == null || !webCamTexture.isPlaying)
            {
                Debug.LogError("摄像头未启动");
                return null;
            }
            Texture2D snap = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            snap.SetPixels(webCamTexture.GetPixels());
            snap.Apply();
            byte[] jpgBytes = snap.EncodeToJPG();
            UnityEngine.Object.Destroy(snap);
            return Convert.ToBase64String(jpgBytes);
        }

        /// <summary>
        /// 切换摄像头
        /// </summary>
        public void SwitchCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length > 1)
            {
                StopCamera();
                cameraIndex = (cameraIndex + 1) % devices.Length;
                StartCamera();
                Debug.Log($"切换到摄像头 {cameraIndex}: {devices[cameraIndex].name}");
            }
            else
            {
                Debug.LogWarning("没有其他摄像头可切换");
            }
        }
    }
}
