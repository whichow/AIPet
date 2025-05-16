///=====================================================
/// - FileName:      WDFSystem.cs
/// - NameSpace:     vonweller
/// - Description:   高级定制脚本生成
/// - Creation Time: 2025/1/6 22:39:52
/// -  (C) Copyright 2008 - 2025
/// -  All Rights Reserved.
///=====================================================
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Vonweller;
using System.Threading.Tasks;
using UnityEditor;
namespace Vonweller
{
    public class WasDB
    {
        public string ResName; //当前加载的资源名字
        public int ResLoadCount;//当前资源被加载次数
        public Sprite[][]FramesSprites;//sprite数组
        public int FramesGroup;//组数
        public int FramesCount; //每组帧数
        public int FramesW; //帧宽
        public int FramesH; //帧高
        public int FramesKey_X;//关键帧偏移X
        public int FramesKey_Y;//关键帧偏移Y
    }
    public struct WDF_FILELIST
    {
        public UInt32 Hash; // 文件的名字散列
        public UInt32 Offset; // 文件的偏移
        public UInt32 Size; // 文件的大小
        public UInt32 Spaces; // 文件的空间
    }
    public interface IWDFSystem
    {
        /// <summary>
        /// 缓存所有的WDF资源列表索引
        /// </summary>
        /// <returns></returns>
        void ReLoadAllWDF(string WDFpath);
        /// <summary>
        /// WDF读取was为sprite
        /// </summary>
        /// <param name="names">WDF名称</param>
        /// <param name="wasHash">Was名称</param>
        /// <returns></returns>
        WasDB WasToSprite(string WDFnames, string wasHash);
        /// <summary>
        /// 精准卸载加载过的WasDB
        /// </summary>
        /// <param name="self"></param>
        void UnloadResource(WasDB self);
    }

    public class WDFSystem : IWDFSystem
    {
        public Dictionary<string, WasDB> WasDBs = new Dictionary<string, WasDB>(); //用于记录当前加载过的资源，用于缓存

        public Dictionary<string, WDF_FILELIST>WDFDBs= new Dictionary<string, WDF_FILELIST>();//用于记录当前加载过的WDF资源的索引与偏移大小


        void IWDFSystem.ReLoadAllWDF(string path)
        {
            GetWDFList(path);
        }
        WasDB IWDFSystem.WasToSprite(string names,string wasHash)
        {
            try
            {
                Debug.Log($"加载{names + wasHash}");
                if (WasDBs.TryGetValue(names + wasHash, out var temp))
                {
                    Debug.Log($"{names + wasHash}资源缓存已加载");
                    temp.ResLoadCount++;
                    return WasDBs[names + wasHash];
                }
                WasDB wasDB = new WasDB();
                //获取was数据
                var data = ExtractDataFromWDF(names, wasHash);
                //解析was数据
                WasRgab32 wasRgab32 = new WasRgab32();
                //非染色数据
                wasRgab32.Read(data, null, null);
                var h = wasRgab32.GetHeader();
                wasDB.ResName = names + wasHash;
                wasDB.ResLoadCount = 1;
                wasDB.FramesKey_X = h.KeyX;
                wasDB.FramesKey_Y = h.KeyY;
                wasDB.FramesW = h.Width;
                wasDB.FramesH = h.Height;
                wasDB.FramesGroup = h.Group;
                wasDB.FramesCount = h.Frame;
                wasDB.FramesSprites = new Sprite[h.Group][];
                Debug.Log($"{h.Group},{h.Frame} {h.Width},{h.Width}");
                for (int i = 0; i < h.Group; i++)
                {
                    wasDB.FramesSprites[i] = new Sprite[h.Frame]; // 初始化第二维度（每一行的数组）
                    for (int j = 0; j < h.Frame; j++)
                    {
                        wasDB.FramesSprites[i][j] = Sprite.Create(wasRgab32.GetFrameData(i, j, h), new Rect(0, 0, (int)h.Width, (int)h.Height), new Vector2((float)h.KeyX / (float)h.Width, 1 - ((float)h.KeyY / (float)h.Height)), 100f, 0, SpriteMeshType.FullRect);
                    }
                }
                WasDBs.Add(names + wasHash, wasDB);
                return wasDB;
            }
            catch (Exception E)
            {

                Debug.LogError(E.Message + "________ 真实堆栈:" + E.StackTrace);
                return null;
            }
            
        }

        void IWDFSystem.UnloadResource(WasDB self)
        {
            if (WasDBs.ContainsKey(self.ResName))
            {
                var wasDB = WasDBs[self.ResName];
                wasDB.ResLoadCount--;
                if (wasDB.ResLoadCount <= 0)
                {
                    // 卸载资源
                    foreach (var group in wasDB.FramesSprites)
                    {
                        foreach (var sprite in group)
                        {
                            UnityEngine.Object.Destroy(sprite);
                        }
                    }
                    WasDBs.Remove(self.ResName);
                }
            }
        }

      
        
        /// <summary>
        /// 获取所有WDF文件列表并缓存
        /// </summary>
        /// <param name="path">WDF名称</param>
        private  void GetWDFList(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // 读取文件头标识
                uint header = reader.ReadUInt32();
               // Debug.Log(header);

                // 读取文件数量
                uint number = reader.ReadUInt32();
                //Debug.Log(number);

                // 读取文件列表偏移量
                uint offset = reader.ReadUInt32();
                //Debug.Log(offset);

                // 移动到文件列表的起始位置
                fs.Seek(offset, SeekOrigin.Begin);

                // 读取文件列表
                for (int i = 0; i < number; i++)
                {
                    WDF_FILELIST w = new WDF_FILELIST
                    {
                        Hash = reader.ReadUInt32(),
                        Offset = reader.ReadUInt32(),
                        Size = reader.ReadUInt32(),
                        Spaces = reader.ReadUInt32()
                    };

                    Debug.Log($"w.Hash :{path} {w.Hash.ToString("X8")}");
                    //Debug.Log($"w.Offset   {w.Offset}");
                    //Debug.Log($"w.Size  {w.Size}");
                    WDFDBs.Add(path+w.Hash.ToString("X8"), w);
                }
            }
        }

        /// <summary>
        /// 通过缓存WDF文件列表中提取偏移与大小并获取原始数据
        /// </summary>
        ///<param name="path">WDF的名称</param>
        /// <param name="wasHash">was的16进制名称</param>
        /// <returns></returns>
        private byte[] ExtractDataFromWDF(string path, string wasHash)
        {
            if (WDFDBs.TryGetValue(path + wasHash, out var data))
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader reader = new BinaryReader(fs)) 
                    { 
                        fs.Seek(data.Offset, SeekOrigin.Begin);
                        byte[] buffer = reader.ReadBytes((int)data.Size);
                        Debug.Log("WDF中读取Was成功");
                        return buffer;
                    }
                }
            }
            return null;

        }


        private void UnloadAllResources()
        {
            foreach (var pair in WasDBs)
            {
                foreach (var group in pair.Value.FramesSprites)
                {
                    foreach (var sprite in group)
                    {
                        UnityEngine.Object.Destroy(sprite);
                    }
                }
            }
            WasDBs.Clear();
            WDFDBs.Clear();//值类型直接清空即可
        }

        public void Destroy()
        {
            Debug.Log("WDFSystem Destroy");
        
            UnloadAllResources();
        }


    }
}
