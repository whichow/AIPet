using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;
using Vonweller;
using System.Threading.Tasks;

namespace XiaozhiAI.Models.IoT.Things
{
    public class Screenctr : Thing
    {
        private bool sleepTimeout;
        private float brightness;

        public Screenctr() : base("Screenctr", "设备屏幕", "用于控制屏幕亮度") // 添加描述
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.brightness = 1f;

            // 更新状态
            States["sleepTimeout"] = sleepTimeout;
            States["brightness"] = brightness;

            // 定义属性
            Properties["sleepTimeout"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "屏幕是否常亮",
                ["readable"] = true,
                ["writable"] = true
            };

            Properties["brightness"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "屏幕亮度",
                ["readable"] = true,
                ["writable"] = true
            };
        }

        protected override Dictionary<string, object> GetMethods()
        {
            var methods = base.GetMethods();
            // 添加云台特有的方法
            methods["set_sleepTimeout"] = new Dictionary<string, object>
            {
                ["description"] = "设置屏幕是否常亮",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["sleepTimeout"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "屏幕是否常量",
                    }
                }
            };
            methods["set_brightness"] = new Dictionary<string, object>
            {
                ["description"] = "设置屏幕亮度",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["brightness"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "亮度值(0-100)",
                        ["min"] = 0,
                        ["max"] = 100
                    }
                }
            };

            return methods;
        }

        public override async Task<string> Invoke(string actionId, JObject parameters)
        {
            switch (actionId)
            {
                case "set_sleepTimeout":
                    sleepTimeout = parameters["sleepTimeout"].ToObject<bool>();
                    Screen.sleepTimeout = sleepTimeout ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
                    States["sleepTimeout"] = sleepTimeout;
                    return $"屏幕常亮设置为: {sleepTimeout}";
                case "set_brightness":
                    brightness = parameters["brightness"].ToObject<float>();
                    Screen.brightness = brightness / 100f;
                    States["brightness"] = brightness;
                    return $"屏幕亮度设置为: {brightness}%";
                default:
                    return $"未知动作: {actionId}";
            }
        }
    }
}