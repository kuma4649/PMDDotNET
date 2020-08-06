﻿using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PPZ8em
    {
        public byte[][] pcmData = new byte[2][];
        private bool[] isPVI = new bool[2];
        private channelWork[] chWk = new channelWork[8]
        {
            new channelWork(),new channelWork(),new channelWork(),new channelWork(),
            new channelWork(),new channelWork(),new channelWork(),new channelWork()
        };
        public int bank = 0;
        public int ptr = 0;
        private bool interrupt = false;
        private byte adpcmEmu;
        private short[][] VolumeTable = new short[16][]{
            new short[256], new short[256], new short[256], new short[256],
            new short[256], new short[256], new short[256], new short[256],
            new short[256], new short[256], new short[256], new short[256],
            new short[256], new short[256], new short[256], new short[256]
        };
        private static double SamplingRate = 44100.0;

        public PPZ8em(uint SamplingRate)
        {
            PPZ8em.SamplingRate = (double)SamplingRate;
        }

    /// <summary>
    /// 0x00 初期化
    /// </summary>
    public void Initialize()
        {
            bank = 0;
            ptr = 0;
            interrupt = false;
            MakeVolumeTable(8);
            for(int i = 0; i < 8; i++)
            {
                chWk[i].srcFrequency = 16000;
                chWk[i].pan = 5;
                chWk[i].panL = 1.0;
                chWk[i].panR = 1.0;
                chWk[i].volume = 8;
                //chWk[i]._frequency = 0;
                chWk[i]._loopStartOffset = -1;
                chWk[i]._loopEndOffset = -1;
            }
        }

        private void MakeVolumeTable(int vol)
        {
            int i, j;
            double temp;

            int volume = vol;
            int AVolume = (int)(0x1000 * Math.Pow(10.0, vol / 40.0));
            int PCM_VOLUME = 12;

            for (i = 0; i < 16; i++)
            {
                temp = Math.Pow(2.0, (i + PCM_VOLUME) / 2.0) * AVolume / 0x18000;
                for (j = 0; j < 256; j++)
                {
                    VolumeTable[i][j] = (short)((j - 128) * temp);
                }
            }
        }

        /// <summary>
        /// 0x01 PCM発音
        /// </summary>
        /// <param name="al">PCMチャンネル(0-7)</param>
        /// <param name="dx">PCMの音色番号</param>
        public void PlayPCM(byte al, ushort dx)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: PlayPCM: ch:{0} @:{1}", al, dx));
#endif

            int bank = (dx & 0x8000) != 0 ? 1 : 0;
            int num = dx & 0x7fff;
            chWk[al].bank = bank;
            if (pcmData[bank] != null)
            {
                chWk[al].ptr = pcmData[bank][num * 0x12 + 32]
                    + pcmData[bank][num * 0x12 + 1 + 32] * 0x100
                    + pcmData[bank][num * 0x12 + 2 + 32] * 0x10000
                    + pcmData[bank][num * 0x12 + 3 + 32] * 0x1000000
                    + 0x20 + 0x12 * 128;
                chWk[al].end = chWk[al].ptr
                    + pcmData[bank][num * 0x12 + 4 + 32]
                    + pcmData[bank][num * 0x12 + 5 + 32] * 0x100
                    + pcmData[bank][num * 0x12 + 6 + 32] * 0x10000
                    + pcmData[bank][num * 0x12 + 7 + 32] * 0x1000000
                    ;

                chWk[al].loopStartOffset = chWk[al]._loopStartOffset;
                if (chWk[al]._loopStartOffset == -1)
                {
                    chWk[al].loopStartOffset = 0
                        + pcmData[bank][num * 0x12 + 8 + 32]
                        + pcmData[bank][num * 0x12 + 9 + 32] * 0x100
                        + pcmData[bank][num * 0x12 + 10 + 32] * 0x10000
                        + pcmData[bank][num * 0x12 + 11 + 32] * 0x1000000
                        ;
                }
                chWk[al].loopEndOffset = chWk[al]._loopEndOffset;
                if (chWk[al]._loopEndOffset == -1)
                {
                    chWk[al].loopEndOffset = 0
                    + pcmData[bank][num * 0x12 + 12 + 32]
                    + pcmData[bank][num * 0x12 + 13 + 32] * 0x100
                    + pcmData[bank][num * 0x12 + 14 + 32] * 0x10000
                    + pcmData[bank][num * 0x12 + 15 + 32] * 0x1000000
                    ;
                }

                //不要っぽい?
                //chWk[al].srcFrequency = (ushort)(chWk[al].ptr
                //    + pcmData[bank][num * 0x12 + 16 + 32]
                //    + pcmData[bank][num * 0x12 + 17 + 32] * 0x100
                //    );

                //chWk[al].frequency = chWk[al]._frequency;
                chWk[al].srcFrequency = chWk[al]._srcFrequency;
            }

            interrupt = false;
            chWk[al].playing = true;
        }


        /// <summary>
        /// 0x02 PCM停止
        /// </summary>
        /// <param name="al">PCMチャンネル(0-7)</param>
        public void StopPCM(byte al)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: StopPCM: ch:{0}", al));
#endif

            chWk[al].playing = false;
        }

        /// <summary>
        /// 0x03 PVIファイルの読み込み＆PCMへの変換
        /// </summary>
        /// <param name="bank">0:PCMバッファ0  1:PCMバッファ1</param>
        /// <param name="mode">0:.PVI (ADPCM)  1:.PZI(PCM)</param>
        /// <param name="pcmData">ファイル内容</param>
        /// <returns></returns>
        public int LoadPcm(byte bank,byte mode, byte[] pcmData)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: LoadPCM: bank:{0} mode:{1}", bank,mode));
#endif

            bank &= 1;
            mode &= 1;
            int ret;
            this.pcmData[bank] = null;

            if (mode == 0) //PVI形式
                ret = CheckPVI(pcmData);
            else //PZI形式
                ret = CheckPZI(pcmData);

            if (ret == 0)
            {
                this.pcmData[bank] = new byte[pcmData.Length];
                Array.Copy(pcmData, this.pcmData[bank], pcmData.Length);
                isPVI[bank] = mode == 0;
            }

            return ret;
        }

        /// <summary>
        /// 0x04 ステータスの読み込み
        /// </summary>
        /// <param name="al"></param>
        public void ReadStatus(byte al)
        {
            switch(al)
            {
                case 0xd:
#if DEBUG
                    Log.WriteLine(LogLevel.TRACE, "ppz8em: ReadStatus: PCM0のテーブルアドレス");
#endif
                    bank = 0;
                    ptr = 0;
                    break;
                case 0xe:
#if DEBUG
                    Log.WriteLine(LogLevel.TRACE, "ppz8em: ReadStatus: PCM1のテーブルアドレス");
#endif
                    bank = 1;
                    ptr = 0;
                    break;
            }
        }

        /// <summary>
        /// 0x07 ボリュームの変更
        /// </summary>
        /// <param name="al">PCMチャネル(0~7)</param>
        /// <param name="dx">ボリューム(0-15 / 0-255)</param>
        public void SetVolume(byte al, ushort dx)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetVolume: Ch:{0} vol:{1}", al, dx));
#endif
            chWk[al].volume = dx;
        }

        /// <summary>
        /// 0x0B PCMの音程周波数の指定
        /// </summary>
        /// <param name="al">PCMチャネル(0~7)</param>
        /// <param name="dx">PCMの音程周波数DX</param>
        /// <param name="cx">PCMの音程周波数CX</param>
        public void SetFrequency(byte al, ushort dx, ushort cx)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetFrequency: 0x{0:x8}", dx * 0x10000 + cx));
#endif
            chWk[al].frequency = (uint)(dx * 0x10000 + cx);
        }

        /// <summary>
        /// 0x0e ループポインタの設定
        /// </summary>
        /// <param name="al">PCMチャネル(0~7)</param>
        /// <param name="lpStOfsDX">ループ開始オフセットDX</param>
        /// <param name="lpStOfsCX">ループ開始オフセットCX</param>
        /// <param name="lpEdOfsDI">ループ終了オフセットDI</param>
        /// <param name="lpEdOfsSI">ループ終了オフセットSI</param>
        public void SetLoopPoint(byte al, ushort lpStOfsDX, ushort lpStOfsCX, ushort lpEdOfsDI, ushort lpEdOfsSI)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetLoopPoint: St:0x{0:x8} Ed:0x{1:x8}"
                , lpStOfsDX * 0x10000 + lpStOfsCX, lpEdOfsDI * 0x10000 + lpEdOfsSI));
#endif
            al &= 7;
            chWk[al]._loopStartOffset = lpStOfsDX * 0x10000 + lpStOfsCX;
            chWk[al]._loopEndOffset = lpEdOfsDI * 0x10000 + lpEdOfsSI;
        }

        /// <summary>
        /// 0x12 PCMの割り込みを停止
        /// </summary>
        public void StopInterrupt()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "ppz8em: StopInterrupt");
#endif
            interrupt = true;
        }

        /// <summary>
        /// 0x13 PAN指定
        /// </summary>
        /// <param name="al">PCMチャネル(0~7)</param>
        /// <param name="dx">PAN(0~9)</param>
        public void SetPan(byte al, ushort dx)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetPan: {0}", dx));
#endif
            chWk[al].pan = dx;
            chWk[al].panL = (chWk[al].pan < 6 ? 1.0 : (0.25 * (9 - chWk[al].pan)));
            chWk[al].panR = (chWk[al].pan > 4 ? 1.0 : (0.25 * chWk[al].pan));
        }

        /// <summary>
        /// 0x15 元データ周波数設定
        /// </summary>
        /// <param name="al">PCMチャネル(0~7)</param>
        /// <param name="dx">元周波数</param>
        public void SetSrcFrequency(byte al, ushort dx)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetSrcFrequency: {0}", dx));
#endif
            chWk[al]._srcFrequency = dx;
        }

        /// <summary>
        /// 0x18  ﾁｬﾈﾙ7のADPCMのエミュレート設定
        /// </summary>
        /// <param name="al">0:ﾁｬﾈﾙ7でADPCMのエミュレートしない  1:する</param>
        public void SetAdpcmEmu(byte al)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetAdpcmEmu: {0}", al));
#endif
            adpcmEmu = al;
        }

        /// <summary>
        /// 0x19 常駐解除許可、禁止設定
        /// </summary>
        /// <param name="v">0:常駐解除許可 1:常駐解除禁止</param>
        public void SetReleaseFlag(int v)
        {
            ;//なにもしない
        }



        private int CheckPZI(byte[] pcmData)
        {
            if (pcmData == null)
                return 5;
            if(!(pcmData[0] == 'P' && pcmData[1] == 'Z' && pcmData[2] == 'I'))
                return 2;

            return 0;
        }

        private int CheckPVI(byte[] pcmData)
        {
            if (pcmData == null)
                return 5;
            if (!(pcmData[0] == 'P' && pcmData[1] == 'V' && pcmData[2] == 'I'))
                return 2;

            return 0;
        }


        public void Update(short[] emuRenderBuf)
        {
            if (interrupt) return;

            int l = 0, r = 0;
            for (int i = 0; i < 8; i++)
            {
                if (!chWk[i].playing) continue;
                if (chWk[i].pan==0) continue;
                if (pcmData[chWk[i].bank] == null) continue;

                l += (int)(VolumeTable[chWk[i].volume][pcmData[chWk[i].bank][chWk[i].ptr]] 
                    * chWk[i].panL);
                r += (int)(VolumeTable[chWk[i].volume][pcmData[chWk[i].bank][chWk[i].ptr]]
                    * chWk[i].panR);
                chWk[i].delta += ((ulong)chWk[i].srcFrequency * (ulong)chWk[i].frequency / (ulong)0x8000) / SamplingRate;
                chWk[i].ptr+=(int)chWk[i].delta;
                chWk[i].delta -= (int)chWk[i].delta;

                if(chWk[i].ptr>= chWk[i].end)
                {
                    if (chWk[i].loopStartOffset != -1)
                    {
                        chWk[i].ptr -= chWk[i].loopEndOffset - chWk[i].loopStartOffset;
                    }
                    else
                    {
                        chWk[i].playing = false;
                    }
                }
            }

            //emuRenderBuf[0] = 0;
            //emuRenderBuf[1] = 0;
            emuRenderBuf[0] = (short)Math.Max(Math.Min(emuRenderBuf[0] + l, short.MaxValue), short.MinValue);
            emuRenderBuf[1] = (short)Math.Max(Math.Min(emuRenderBuf[1] + r, short.MaxValue), short.MinValue);
        }

    }

    public class channelWork
    {
        public int loopStartOffset;
        public int loopEndOffset;
        public bool playing;
        public ushort pan;
        public double panL;
        public double panR;
        public uint srcFrequency;
        public ushort volume;
        public uint frequency;

        public int _loopStartOffset;
        public int _loopEndOffset;
        //public uint _frequency;
        public uint _srcFrequency;

        public int bank;
        public int ptr;
        public int end;
        public double delta;
    }
}