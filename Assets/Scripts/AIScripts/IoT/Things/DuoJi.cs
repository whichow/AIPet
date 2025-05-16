using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;
using Vonweller;
using System.Threading.Tasks;

namespace XiaozhiAI.Models.IoT.Things
{
    public class DuoJi : Thing
    {
        private int dushu0;
        private int dushu1;

        public DuoJi() : base("DuoJi", "云台舵机", "一个可以控制云台转到的设备") // 添加描述
        {
            dushu0 = 0;
            dushu1 = 0;

            // 更新状态
            States["dushu0"] = dushu0;
            States["recording"] = dushu1;

            // 定义属性
            Properties["dushu"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "舵机1号当前度数",
                ["readable"] = true,
                ["writable"] = true
            };

            Properties["dushu1"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "舵机2号当前度数",
                ["readable"] = true,
                ["writable"] = true
            };
        }

        protected override Dictionary<string, object> GetMethods()
        {
            var methods = base.GetMethods();
            // 添加云台特有的方法
            methods["set_dushu0"] = new Dictionary<string, object>
            {
                ["description"] = "设置左右转动舵机的度数",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["dushu0"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "旋转角度值(0-180)",
                        ["min"] = 0,
                        ["max"] = 180
                    }
                }
            };
            methods["set_dushu1"] = new Dictionary<string, object>
            {
                ["description"] = "设置上下转动舵机的度数",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["dushu1"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "旋转角度值(0-180)",
                        ["min"] = 0,
                        ["max"] = 180
                    }
                }
            };

            return methods;
        }

        public override async Task<string> Invoke(string actionId, JObject parameters)
        {
            switch (actionId)
            {
                default:
                    return $"未知动作: {actionId}";
            }
        }
    }
}