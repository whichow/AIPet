using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;
using Vonweller;
using UnityEngine.Rendering;
using UnityEngine.Audio;
using System.Threading.Tasks;

namespace XiaozhiAI.Models.IoT.Things
{
    public class Speaker : Thing
    {
        private bool isOn;
        private float volume;
        private bool isMuted;

        public Speaker() : base("speaker", "音量控制", "用来控制设备音量大小") // 添加描述
        {
            isOn = false;
            volume = 0.5f;
            isMuted = false;

            // 更新状态
            States["power"] = isOn;
            States["volume"] = volume;
            States["muted"] = isMuted;

            // 定义属性
            Properties["power"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "音量设备当前开关状态",
                ["readable"] = true,
                ["writable"] = true
            };

            Properties["volume"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "当前音量",
                ["readable"] = true,
                ["writable"] = true,
                ["min"] = 0,
                ["max"] = 1
            };

            Properties["muted"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "是否静音",
                ["readable"] = true,
                ["writable"] = true
            };
        }

        protected override Dictionary<string, object> GetMethods()
        {
            var methods = base.GetMethods();
            
            // 添加音箱特有的方法
            methods["set_volume"] = new Dictionary<string, object>
            {
                ["description"] = "设置音量",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["volume"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "音量值(0-100)",
                        ["min"] = 0,
                        ["max"] = 100
                    }
                }
            };
            
            methods["mute"] = new Dictionary<string, object>
            {
                ["description"] = "静音",
                ["parameters"] = new Dictionary<string, object>()
            };
            
            methods["unmute"] = new Dictionary<string, object>
            {
                ["description"] = "取消静音",
                ["parameters"] = new Dictionary<string, object>()
            };
            
            return methods;
        }

        public override async Task<string> Invoke(string actionId, JObject parameters)
        {
            switch (actionId)
            {
                case "set_volume":
                    
                    volume =parameters["volume"].ToObject<float>()/100;
                    GameObject.Find("AudioSource").GetComponent<AudioSource>().volume = volume;
                    return $"音量已调整到{parameters["volume"].ToObject<float>()}";
                case "mute":
                     GameObject.Find("AudioSource").GetComponent<AudioSource>().volume = 0;
                    return $"已静音";
                case "unmute":
                    GameObject.Find("AudioSource").GetComponent<AudioSource>().volume = volume;
                    return $"已静音";
                default:
                    return $"未知动作: {actionId}";
            }
        }
    }
}