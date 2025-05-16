using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;
using Vonweller;
using System.Threading.Tasks;

namespace XiaozhiAI.Models.IoT.Things
{
    public class OpenApp : Thing
    {
        private bool ISopen;
        private string APPnames;

        public OpenApp() : base("OpenApp", "打开应用程序", "用于控制打开应用程序") // 添加描述
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.brightness = 1f;

            // 更新状态
            States["ISopen"] = ISopen;
            States["APPnames"] = APPnames;

            // 定义属性
            Properties["ISopen"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "应用是否打开了",
                ["readable"] = true,
                ["writable"] = true
            };

            Properties["APPnames"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "打开的APP名称",
                ["readable"] = true,
                ["writable"] = true
            };
            Properties["Numbers"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "要拨打的电话号码",
                ["readable"] = true,
                ["writable"] = true
            };
            Properties["content"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "发送短信的内容",
                ["readable"] = true,
                ["writable"] = true
            };
        }

        protected override Dictionary<string, object> GetMethods()
        {
            var methods = base.GetMethods();
            // 添加云台特有的方法
            methods["open_APPnames"] = new Dictionary<string, object>
            {
                ["description"] = "设置屏幕是否常亮",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["APPnames"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "要打开的APP名称",
                    },
                    ["Numbers"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "要拨打的电话号码",
                    },
                    ["content"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "发送短信的内容",
                    }
                }
            };
            return methods;
        }

        public override async Task<string> Invoke(string actionId, JObject parameters)
        {
            switch (actionId)
            {
                case "open_APPnames":
                    APPnames = parameters["APPnames"].ToObject<string>();
                    ISopen = true;
#if UNITY_ANDROID 
                    if (APPnames.Contains("网易云音乐"))
                    {

                        OpenPackage("com.netease.cloudmusic");
                            return $"打开:{APPnames}成功";
                    }
                    else if (APPnames.Contains("QQ音乐"))
                    {
                        OpenPackage("com.tencent.qqmusic");
                        return $"打开:{APPnames}成功";
                    }
                    else if (APPnames.Contains("微信"))
                    {
                        OpenPackage("com.tencent.mm");
                        return $"打开:{APPnames}成功";
                    }
                    else if (APPnames.Contains("QQ"))
                    {
                        OpenPackage("com.tencent.mobileqq");
                        return $"打开:{APPnames}成功";
                    }
                    else if (APPnames.Contains("支付宝"))
                    {
                        OpenPackage("com.eg.android.AlipayGphone");
                        return $"打开:{APPnames}成功";
                    }
                    else if (APPnames.Contains("抖音"))
                    {
                        OpenPackage("com.ss.android.ugc.aweme");
                        return $"打开:{APPnames}成功";
                    }
                    else if (APPnames.Contains("拨号"))
                    {
                        var Numbers = parameters["Numbers"].ToObject<string>();
                        OpenDialer(Numbers);
                        return $"拨号:{Numbers}成功";
                    }
                    else if (APPnames.Contains("短信"))
                    {
                        var Numbers = parameters["Numbers"].ToObject<string>();
                        var content = parameters["content"].ToObject<string>();
                        OpenSMS(Numbers, content);
                        return $"短信发送:{Numbers}:{content}成功";
                    }
#endif
                    return $"未注册应用无法打开: {APPnames}";

                default:
                    return $"未知动作: {actionId}";
            }
        }

        public void OpenSMS(string phoneNumber, string message)
        {
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    Debug.LogWarning("电话号码不能为空");
                    return;
                }
                string url = "sms:" + phoneNumber;

                if (!string.IsNullOrEmpty(message))
                {
                    url += "?body=" + System.Uri.EscapeDataString(message);
                }
                Application.OpenURL(url);
        }

        public void OpenDialer(string phoneNumber)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // 获取Android活动
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            // 创建Intent对象
            AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.DIAL");
            
            // 设置电话号码
            AndroidJavaClass uri = new AndroidJavaClass("android.net.Uri");
            AndroidJavaObject phoneUri = uri.CallStatic<AndroidJavaObject>("parse", "tel:" + phoneNumber);
            intent.Call<AndroidJavaObject>("setData", phoneUri);
            
            // 启动电话应用
            currentActivity.Call("startActivity", intent);
        }
        catch (System.Exception e)
        {
            Debug.LogError("拨打电话时出错: " + e.Message);
            // 回退到简单方法
            Application.OpenURL("tel:" + phoneNumber);
        }
#else
            Debug.Log("模拟拨打电话: " + phoneNumber);
            // 在编辑器或其他平台上，使用简单方法
            Application.OpenURL("tel:" + phoneNumber);
#endif
        }

        public  void OpenPackage(string pkgName)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass jcPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject joActivity = jcPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        using (AndroidJavaObject joPackageManager = joActivity.Call<AndroidJavaObject>("getPackageManager"))
                        {
                            using (AndroidJavaObject joIntent = joPackageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", pkgName))
                            {
                                if (null != joIntent)
                                {
                                    AndroidJavaObject joNIntent = joIntent.Call<AndroidJavaObject>("addFlags", joIntent.GetStatic<int>("FLAG_ACTIVITY_REORDER_TO_FRONT"));
                                    joActivity.Call("startActivity", joNIntent);
                                    joIntent.Dispose();
                                    Debug.Log($"成功启动应用: {pkgName}");
                                }
                                else
                                {
                                    string msg = $"应用 <{pkgName}> 未安装";
                                    Debug.LogWarning(msg);
                                    //ShowToast(joActivity, msg);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"启动应用失败: {ex.Message}\n{ex.StackTrace}");
            }
#endif
        }

    }
}