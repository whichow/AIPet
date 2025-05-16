using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Vonweller;

namespace XiaozhiAI.Models.IoT.Things
{
    public class Camera : Thing
    {
        private bool isOn;
        private bool isRecording;

        private CameraSystem cameraSystem;

        public Camera() : base("camera", "智能摄像头", "一个可控制开关和录像的智能摄像头") // 添加描述
        {
            isOn = false;
            isRecording = false;

            // 更新状态
            States["power"] = isOn;
            States["recording"] = isRecording;

            // 定义属性
            Properties["power"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "摄像头开关状态",
                ["readable"] = true,
                ["writable"] = true
            };

            Properties["recording"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "识别画面内容",
                ["readable"] = true,
                ["writable"] = true
            };
        }

        protected override Dictionary<string, object> GetMethods()
        {
            var methods = base.GetMethods();          
            // 添加摄像头特有的方法
            methods["Startrecording"] = new Dictionary<string, object>
            {
                ["description"] = "识别画面",
                ["parameters"] = new Dictionary<string, object>()
            };

            return methods;
        }

        public override async Task<string> Invoke(string actionId, JObject parameters)
        {
            switch (actionId)
            {
                case "Startrecording":
                    try
                    {
                        var txt = cameraSystem.CaptureFrameToBase64();
                        var data = await GameObject.Find("VL").GetComponent<VL>().StartStreamingSession(txt);
                        States["recording"] = "画面识别内容:" + data;
                        return $"画面已识别{data}";
                    }
                    catch (Exception e)
                    {
                        return $"当前错误：{e.Message}";
                    }

                case "turn_off":
                    States["power"] = false;
                    cameraSystem.StopCamera();
                    return "摄像头已经关闭";
                case "turn_on":
                 var cameradata= await cameraSystem.StartCamera();
                 States["power"] = cameradata.Item1;
                    return $"{cameradata.Item1}::{cameradata.Item2}";
                default:
                    return $"未知动作: {actionId}";
            }
        }
    }
}