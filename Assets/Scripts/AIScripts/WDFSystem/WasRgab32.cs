using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;


    // 分段信息, 暂时只分4段, 不一定用完
    public struct Pal
    {
        public uint Segment1;
        public uint Segment2;
        public uint Segment3;
        public uint Segment4;
    }

    public unsafe class WasRgab32
    {
        private byte* _mAddonHead; // byte 附加头
        private ushort* _mPalette; // ushort  256色调色板(16bit)
        private uint* _mPalette32; // uint 256色调色板(32bit) ABGR 因为COLOR32的rgba的内存结构问题 
        private uint* _mFrameIndex; // uint 每帧的位置索引
        private uint* _mBmpBuffer; // uint 位图缓冲区

        private uint** _imageBuffer; // _mBmpBuffer 的指针 存整个was的数据



        private int _mOffset; // 偏移值
        private uint* _mFrameLine; // uint 图片每行的数据块位置索引
        private Header _header;
        private Frame _frame;
        private Extend _extend;
        private Pal _pal;
        private long _wasBytesLen = 0;

        // GD的对齐 GD为左上角原点

        /*
         * 人物与武器的对齐
         * 人物与武器AnimatedSprite均设置锚点为中心（Center）
         * 重新算人物的锚点的绝对位置
         * 1、找人物的左边缘绝对位置: (人物Position.x + 人物Offset.x - (人物宽/2)), 找人物锚点X的绝对位置 左边缘 + 人物.KeyX
         * 2、找人物的上边缘绝对位置：(人物Position.y + 人物Offset.y - (人物高/2)), 找人物锚点Y的绝对位置 上边缘 + 人物.KeyY
         * 先将武器Position设为人物 (Position.x, 15(10?) - 武器帧Height / 2)
         * 同样方法算出武器KeyX, KeyY在那里
         * 人物与武器锚点重合得出偏移，将偏移加到武器offset或者Position上
         */

        // U的对齐 U为左下角原点 对锚依然以左上角来对！！！这是因为WAS的背景就是这样的。
        // 所以：X轴方向一致不用理会，Y要反过来处理。
        // 并且注意 不是transform的东西要除unitpixel 比如说unitpixel = 20

        /*
         * 人物与武器的对齐
         * 人物与武器AnimatedSprite均设置锚点为中心（Center）
         * 重新算人物的锚点的绝对位置
         * 1、找人物的左边缘绝对位置: (人物Position.x - (人物宽/2/20)), 找人物锚点X的绝对位置 左边缘 + 人物.KeyX / 20
         * 2、找人物的上边缘绝对位置：(人物Position.y + (人物高/2/20)), 找人物锚点Y的绝对位置 上边缘 - 人物.KeyY /20 (Y方向是向上的）
         * 先将武器Position设为人物 (Position.x, 武器帧Height / 2 / 20 - 15 / 20(10 / 20?))
         * 同样方法算出武器KeyX, KeyY在那里
         * 人物与武器锚点重合得出偏移，将偏移加到武器offset或者Position上
         */

        public Header GetHeader()
        {
            return _header;
        }

        // Was左右上下帧偏移扩展
        public struct Extend
        {
            public int Left;
            public int Right;
            public int Up;
            public int Down;

            public override string ToString()
            {
                return $"Was Extend, Left: {Left}, Right: {Right}, Up: {Up}, Down: {Down}";
            }
        }

        // 取帧
        // group为方向 根据header得知
        // frame为帧 根据header得知
        // 取帧, unit* abgr
        public Texture2D GetFrameData(int group, int frame,Header header)
        {
            var ptr = _imageBuffer[group * _header.Frame + frame];
            var data = new NativeArray<Color32>(header.Width * header.Height, Allocator.Temp);
            UnsafeUtility.MemCpy(data.GetUnsafePtr(), ptr, header.Width * header.Height * 4);
            var t = new Texture2D(header.Width, header.Height, TextureFormat.RGBA32, false, false);
            t.SetPixelData(data, 0, 0);
            t.Apply();
            return t;
        }
        // 取帧
        // group为方向 根据header得知
        // frame为帧 根据header得知
        // 取帧, unit* abgr
        public uint* GetFrameDataPtr(int group, int frame)
        {
            return _imageBuffer[group * _header.Frame + frame];
        }

        public unsafe void Read(byte[] vs, byte[] palBytes = null, List<uint> pal = null)
        {
            fixed (byte* b = vs)
            {
                if (palBytes != null && pal.Count > 0)
                {
                    fixed (byte* c = palBytes)
                    {
                        //UnityEngine.Debug.Log("染色");
                        Parse(b, vs.Length, c, pal);
                        return;
                    }
                }
                //UnityEngine.Debug.Log("非染色");
                Parse(b, vs.Length, null, null);
            }
        }

        // 读was文件
        public void Parse(byte* wasBytes, long wasByteLen, byte* palBytes, List<uint> pal = null)
        {
            _wasBytesLen = wasByteLen;
            //读头
            var p = wasBytes;
            var header = new Header();
            var hPtr = &header;
            UnsafeUtility.MemCpy(hPtr, p, UnsafeUtility.SizeOf<Header>());
            _header = *hPtr;
            p += sizeof(Header);

            // 判断精灵文件标志是否为 "SP"
            if (_header.Flag != 0x5053)
            {
                throw new Exception("Sprite file flag error");
            }

            // 判断精灵文件头长度是否为12
            if (_header.HeadLen != 12)
            {
                var addonHeadLen = _header.HeadLen - 12;
                _mAddonHead = (byte*)UnsafeUtility.Malloc(addonHeadLen, 4, Allocator.Persistent);
                UnsafeUtility.MemCpy(_mAddonHead, p, addonHeadLen);
                p += addonHeadLen;
            }

            // 读取调色板数据
            _mPalette = (ushort*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<ushort>() * 256, 4, Allocator.Persistent); // 分配16bit调色板的空间
            UnsafeUtility.MemCpy(_mPalette, p, 256 * 2);
            p += 256 * 2;

            // 调色板转换
            // 分配32bit调色板的空间
            _mPalette32 = (uint*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<uint>() * 256, 4, Allocator.Persistent); // 分配32bit调色板的空间
            for (var i = 0; i < 256; i++) // 16to32调色板转换
            {
                _mPalette32[i] = Rgb565To888(_mPalette[i], 0xff);
            }

            // 如果有调色, 在这里调色

            if (palBytes != null && pal != null)
            {
                var pb = palBytes;
                if (*(uint*)pb == 0x6C617077) // WPAL
                {
                    var segments = *(uint*)(pb + 4); // 分段信息
                    if (segments > 4)
                    {
                        throw new Exception("segments > 4");
                    }

                    pb += (3 + segments) * 4;

                    var palettes = new List<Palette>();
                    for (var i = 0; i < segments; i++)
                    {
                        palettes.Add(new Palette()
                        {
                            ColorSolution = pb, // 方案地址
                            Color = pb + 4 // 方案列表地址
                        });
                        switch (i)
                        {
                            case 0:
                                _pal.Segment1 = *(uint*)pb;
                                break;
                            case 1:
                                _pal.Segment2 = *(uint*)pb;
                                break;
                            case 2:
                                _pal.Segment3 = *(uint*)pb;
                                break;
                            case 3:
                                _pal.Segment4 = *(uint*)pb;
                                break;
                        }

                        pb += *(uint*)pb * 3 * 3 * 4 + 4;
                    }

                    var n = 0;
                    var ptr = palBytes + 2 * 4;
                    var cp = palBytes;
                    for (uint i = 0; i < 255; i++)
                    {
                        if (i == *(uint*)ptr) // 有一个256用于隔离
                        {
                            // 检查 n 是否越界
                            if (n >= pal.Count || n >= palettes.Count)
                            {
                                break; // 退出循环，避免继续越界
                            }
                            // 检查 palettes[n].ColorSolution 是否为 null
                            if (palettes[n].ColorSolution == null)
                            {
                                break; // 退出循环，避免解引用 null 指针
                            }
                            // TODO: 要判断是否越界, 还有适配多段染色
                            if (pal[n] < *(uint*)palettes[n].ColorSolution) // 传出来配色方案小于文件里面的配色方案
                            {
                                cp = palettes[n].Color + pal[n] * 9 * 4; // 开始指针 + 跳多少个RGB(代表方案)  
                            }
                            else
                            {
                                cp = palettes[n].Color;
                            }

                            n++;
                            ptr += 4;
                        }
                        _mPalette32[i] = Rgb565To888_Pal(*(_mPalette + i), 255, cp);
                    }
                }
            }
            // 读取帧引索列表
            var frames = _header.Group * _header.Frame; // was总帧数
            // 分配帧索引列表的空间
            _mFrameIndex = (uint*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<uint>() * frames, 4, Allocator.Persistent); // 分配帧索引列表的空间
            UnsafeUtility.MemCpy(_mFrameIndex, p, frames * 4);

            _imageBuffer = (uint**)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<UIntPtr>() * frames, 4,
                Allocator.Persistent);
            UnsafeUtility.MemSet(_imageBuffer, 0, UnsafeUtility.SizeOf<UIntPtr>() * frames);
            // 循环帧处理 
            _mOffset = 2 + 2 + _header.HeadLen; // 相对偏移值

            _frame = new Frame();

            var left = 0;
            var right = 0;
            var up = 0;
            var down = 0;
            for (var i = 0; i < frames; i++)
            {
                // 跳转到帧的开始位置
                var framePtr = wasBytes;
                if (*(_mFrameIndex + i) == 0) continue;
                framePtr = framePtr + *(_mFrameIndex + i) + _mOffset;

                // 读取帧的头数据
                _frame.KeyX = *(int*)framePtr;
                framePtr += 4;
                _frame.KeyY = *(int*)framePtr;
                framePtr += 4;
                _frame.Width = *(uint*)framePtr;
                framePtr += 4;
                _frame.Height = *(uint*)framePtr;
                if (_frame.Width > _header.Width && _frame.Width - _header.Width > right)
                {
                    right = (int)_frame.Width - _header.Width;
                }

                // ...
                if (_header.KeyX > _frame.KeyX && _header.KeyX - _frame.KeyX + _frame.Width > right + _header.Width)
                {
                    if (_header.KeyX - _frame.KeyX > right) right = _header.KeyX - _frame.KeyX;
                }

                if (_frame.KeyX > _header.KeyX && _frame.KeyX - _header.KeyX > left)
                {
                    left = _frame.KeyX - _header.KeyX;
                    if (_frame.Width - left > _header.Width && _frame.Width - left - _header.Width > right)
                    {
                        right = (int)_frame.Width - left - _header.Width;
                    }
                }

                if (_frame.Height > _header.Height && _frame.Height - _header.Height > down)
                {
                    down = (int)(_frame.Height - _header.Height);
                }

                // ...
                if (_header.KeyY > _frame.KeyY && _header.KeyY - _frame.KeyY + _frame.Height > down + _header.Height)
                {
                    if (_header.KeyY - _frame.KeyY > down) down = _header.KeyY - _frame.KeyY;
                }

                if (_frame.KeyY > _header.KeyY && _frame.KeyY - _header.KeyY > up)
                {
                    up = _frame.KeyY - _header.KeyY;
                    if (_frame.Height - up > _header.Height && _frame.Height - up - _header.Height > down)
                    {
                        down = (int)_frame.Height - up - _header.Height;
                    }
                }
            }

            _extend = new Extend()
            {
                Left = left,
                Right = right,
                Up = up,
                Down = down,
            };
            ushort subFrames = 0;


            var maxHeight = header.Height + (ushort)(_extend.Up + _extend.Down);
            _mFrameLine = (uint*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<uint>() * maxHeight, 4, Allocator.Persistent);

            // 计算总像素值*/
            var pixels = (_header.Width + _extend.Left + _extend.Right) *
                         (_header.Height + _extend.Up + _extend.Down);

            for (var i = 0; i < frames; i++)
            {
                if (*(_mFrameIndex + i) == 0)
                {
                    // 分配位图空间
                    _mBmpBuffer = (uint*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<uint>() * pixels, 4, Allocator.Persistent); // 分配位图空间
                    UnsafeUtility.MemSet(_mBmpBuffer, 0, UnsafeUtility.SizeOf<uint>() * pixels);
                    _imageBuffer[i] = _mBmpBuffer;
                    subFrames += 1;
                    continue;
                }

                // 跳转到帧的开始位置
                var framePtr = wasBytes;
                framePtr = framePtr + *(_mFrameIndex + i) + _mOffset;

                // 读取帧的头数据
                _frame.KeyX = *(int*)framePtr;
                framePtr += 4;
                _frame.KeyY = *(int*)framePtr;
                framePtr += 4;
                _frame.Width = *(uint*)framePtr;
                framePtr += 4;
                _frame.Height = *(uint*)framePtr;
                framePtr += 4;



                // 分配位图空间
                _mBmpBuffer = (uint*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<uint>() * pixels, 4, Allocator.Persistent); // 分配位图空间
                UnsafeUtility.MemSet(_mBmpBuffer, 0, UnsafeUtility.SizeOf<uint>() * pixels);
                _imageBuffer[i] = _mBmpBuffer;

                // _mFrameLine
                // 已在for循环外面初始化 公用
                // 这里需要重新初始化一下

                UnsafeUtility.MemSet(_mFrameLine, 0, _frame.Height * 4);
                UnsafeUtility.MemCpy(_mFrameLine, framePtr, _frame.Height * 4);


                // 循环处理行的数据
                for (uint j = 0; j < _frame.Height; j++)
                {
                    framePtr = wasBytes;
                    DataHandler(framePtr, i, j, _mPalette32, _mBmpBuffer);
                }
            }

            // 改Width, Height, KeyX, KeyY, 
            _header.Width += (ushort)(_extend.Left + _extend.Right);
            _header.Height += (ushort)(_extend.Up + _extend.Down);
            _header.KeyX += (short)_extend.Left;
            _header.KeyY += (short)_extend.Up;
            _header.Frame -= subFrames;

            // 先释放一部分内存
            if (_header.HeadLen != 12)
            {
                UnsafeUtility.Free(_mAddonHead, Allocator.Persistent);
            }
            UnsafeUtility.Free(_mPalette, Allocator.Persistent);
            UnsafeUtility.Free(_mFrameIndex, Allocator.Persistent);
            UnsafeUtility.Free(_mFrameLine, Allocator.Persistent);
            UnsafeUtility.Free(_mPalette32, Allocator.Persistent);
        }

        public void Release()
        {
            for (int i = 0; i < _header.Group * _header.Frame; i++)
            {
                UnsafeUtility.Free(_imageBuffer[i], Allocator.Persistent);
            }
        }

        private void
            DataHandler(byte* pData, int frame, uint line, uint* palette,
                uint* buffer) // 文件指针， 帧索引，行索引
        {
            long pixelLen = 0;


            /*// 顺着算
            pos = (int) ((line + _extend.up + _header.KeyY - _frame.KeyY) *
                         (_header.Width + _extend.left + _extend.right + _extend.w));*/

            // 反转

            var pos = (int)((_extend.Up + _header.Height + _extend.Down - 1 -
                              (line + _extend.Up + _header.KeyY - _frame.KeyY)) *
                             (_header.Width + _extend.Left + _extend.Right));
            var origin_pos = pos;

            pos += _extend.Left + _header.KeyX - _frame.KeyX;

            pixelLen = pos + _frame.Width;

            var p = pData + _mOffset + _mFrameIndex[frame] + _mFrameLine[line];

            if (*p == 0)
            {
                if (line > 0 && *(pData + _mOffset + _mFrameIndex[frame] + _mFrameLine[line - 1]) != 0)
                {
                    var headerWidth = _header.Width + _extend.Left + _extend.Right;
                    UnsafeUtility.MemCpy(buffer + origin_pos, buffer + origin_pos - headerWidth,
                        headerWidth * UnsafeUtility.SizeOf<uint>());
                }
            }
            else
            {
                while (*p != 0) // {00000000} 表示像素行结束，如有剩余像素用透明色代替
                {
                    byte style = 0;
                    byte repeat = 0; // 重复次数
                    style = (byte)((*p & (0xc0)) >> 6);
                    switch (style)
                    {
                        case 0: // {00******}
                            byte alpha = 0; // Alpha层数
                            if ((*p & 0x20) == 0x20)
                            {
                                alpha = (byte)((*p++ & 0x1f) << 3);
                                if (pos <= pixelLen)
                                {
                                    var m = *(palette + *p++);
                                    // m.a = alpha;
                                    m = (m & 0xffffff) | (uint)(alpha << 24);
                                    *(buffer + pos++) = m;
                                }
                            }
                            else
                            {
                                repeat = (byte)(*p++ & 0x1f);
                                alpha = (byte)(*p++ << 3);
                                var m = *(palette + *p++);
                                m = (m & 0xffffff) | (uint)(alpha << 24);
                                // m.a = alpha;
                                for (var j = 1; j <= repeat; j++)
                                {
                                    if (pos > pixelLen) continue;
                                    *(buffer + pos++) = m;
                                }
                            }

                            break;
                        case 1:
                            repeat = (byte)(*p++ & 0x3f);
                            for (var j = 1; j <= repeat; j++)
                            {
                                if (pos > pixelLen) continue;
                                *(buffer + pos) = *(palette + *p++);
                                pos++;
                            }

                            break;
                        case 2:
                            repeat = (byte)(*p++ & 0x3f);
                            var c2 = *(palette + *p++);
                            for (var j = 1; j <= repeat; j++)
                            {
                                if (pos > pixelLen) continue;
                                *(buffer + pos++) = c2;
                            }

                            break;
                        case 3:
                            repeat = (byte)(*p++ & 0x3f);
                            if (repeat == 0)
                            {
                                var c = *(buffer + pos - 1);
                                if ((c | 0xffffff) == 0 && line > 0)
                                // if (c.r == 0 && c.g == 0 & c.b == 0 && line > 0)
                                {
                                    *(buffer + pos - 1) =
                                        *(buffer + pos +
                                          (_header.Width + _extend.Left + _extend.Right)); // TODO: 找到上一行的位置
                                }
                                else
                                {
                                    c |= 0xff000000;
                                    // c.a = 255;
                                    *(buffer + pos - 1) = c;
                                }

                                if (p - pData + 3 < _wasBytesLen)
                                {
                                    p += 2;
                                }

                                break;
                            }

                            if (pos + repeat < pixelLen)
                            {
                                pos += repeat;
                            }

                            break;
                        default:
                            throw new Exception("ERROR");
                    }
                }
            }
        }

        private static uint Rgb565To888(ushort color, byte alpha)
        {
            var r = (uint)(color >> 11) & 0x1f;
            var g = (uint)(color >> 5) & 0x3f;
            var b = (uint)(color) & 0x1f;

            var a = (uint)alpha << 24;
            var r1 = ((r << 3) | (r >> 2));
            var g1 = ((g << 2) | (g >> 4)) << 8;
            var b1 = ((b << 3) | (b >> 2)) << 16;

            // COLOR32 int rgba <-> ABGR
            return (a | b1 | g1 | r1);
        }

        private static uint Rgb565To888_Pal(ushort color, byte alpha, byte* pal)
        {
            var r = (uint)(color >> 11) & 0x1f;
            var g = (uint)(color >> 5) & 0x3f;
            var b = (uint)(color) & 0x1f;

            var palPtr = pal;

            var r2 = r * (*(uint*)palPtr) + g * (*(uint*)(palPtr + 4)) + b * (*(uint*)(palPtr + 8));

            palPtr += 12;

            var g2 = r * (*(uint*)palPtr) + g * (*(uint*)(palPtr + 4)) + b * (*(uint*)(palPtr + 8));

            palPtr += 12;

            var b2 = r * (*(uint*)palPtr) + g * (*(uint*)(palPtr + 4)) + b * (*(uint*)(palPtr + 8));

            r = r2 >> 8;
            g = g2 >> 8;
            b = b2 >> 8;

            r = r > 0x1F ? 0x1F : r;
            g = g > 0x3F ? 0x3F : g;
            b = b > 0x1F ? 0x1F : b;


            var a = (uint)alpha << 24;
            var r1 = ((r << 3) | (r >> 2));
            var g1 = ((g << 2) | (g >> 4)) << 8;
            var b1 = ((b << 3) | (b >> 2)) << 16;

            // COLOR32 int rgba <-> ABGR
            return (a | b1 | g1 | r1);
        }

        // 精灵动画的文件头
        public struct Header
        {
            public ushort Flag; // 精灵文件标志 SP 0x5053
            public ushort HeadLen; // 文件头的长度 默认为 12
            public ushort Group; // 精灵图片的组数，即方向数
            public ushort Frame; // 每组的图片数，即帧数
            public ushort Width; // 精灵动画的宽度，单位像素
            public ushort Height; // 精灵动画的高度，单位像素
            public short KeyX; // 精灵动画的关键位X
            public short KeyY; // 精灵动画的关键位Y

            public override string ToString()
            {
                return $"Was Header, Width: {Width}, Height: {Height}, KeyX: {KeyX}, KeyY: {KeyY}";
            }
        }


        // 帧的文件头
        private struct Frame
        {
            public int KeyX; // 图片的关键位X
            public int KeyY; // 图片的关键位Y
            public uint Width; // 图片的宽度，单位像素
            public uint Height; // 图片的高度，单位像素
        };

        private struct Palette
        {
            public byte* ColorSolution;
            public byte* Color;
        }
    }
