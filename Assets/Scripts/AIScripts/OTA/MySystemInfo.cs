using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Vonweller
{
    public class MySystemInfo
    {
        private static System.Random random = new System.Random();

        /// <summary>
        /// 生成随机MAC地址
        /// </summary>
        private static string GenerateRandomMacAddress()
        {
            byte[] bytes = new byte[6];
            random.NextBytes(bytes);
            
            // 确保第一个字节的第二个最低有效位为0（表示这是一个单播地址）
            bytes[0] = (byte)(bytes[0] & 0xFE);
            
            // 确保第一个字节的最低有效位为0（表示这是一个全局管理的地址）
            bytes[0] = (byte)(bytes[0] & 0xFD);
            
            return string.Join(":", bytes.Select(b => b.ToString("X2"))).ToLower();
        }

        /// <summary>
        /// 获取 MAC 地址
        /// </summary>
        /// <returns></returns>
        public static string GetMacAddress()
        {
            string macAddresses = "";
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString("MacDevcID"))) 
            {
                macAddresses = PlayerPrefs.GetString("MacDevcID");
                Debug.Log("缓存本机账号MAC地址：" + PlayerPrefs.GetString("MacDevcID"));
                return macAddresses.ToLower();
            }

            macAddresses = GenerateRandomMacAddress();
            PlayerPrefs.SetString("MacDevcID", macAddresses);
            return macAddresses.ToLower();
        }
    }
}