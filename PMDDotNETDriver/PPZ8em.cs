using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PMDDotNET.Driver
{
    //
    // ppz8l.cpp / ppz8l.h(by C60さん) を参考に作成
    //

    public class PPZ8em
    {
        public byte[][] pcmData = new byte[2][];
        private bool[] isPVI = new bool[2];
        private PPZChannelWork[] chWk = new PPZChannelWork[8]
        {
            new PPZChannelWork(),new PPZChannelWork(),new PPZChannelWork(),new PPZChannelWork(),
            new PPZChannelWork(),new PPZChannelWork(),new PPZChannelWork(),new PPZChannelWork()
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
        private double SamplingRate = 44100.0;
        private int PCM_VOLUME = 0;
        private int volume = 0;

        public PPZ8em(uint SamplingRate = 44100)
        {
            this.SamplingRate = (double)SamplingRate;
        }

        /// <summary>
        /// 0x00 初期化
        /// </summary>
        public void Initialize()
        {
            bank = 0;
            ptr = 0;
            interrupt = false;
            for (int i = 0; i < 8; i++)
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
            PCM_VOLUME = 0;
            volume = 0;
            SetAllVolume(12);
        }

        private void MakeVolumeTable(int vol)
        {
            int i, j;
            double temp;

            volume = vol;
            int AVolume = (int)(0x1000 * Math.Pow(10.0, vol / 40.0));

            for (i = 0; i < 16; i++)
            {
                temp = Math.Pow(2.0, (i + PCM_VOLUME) / 2.0) * AVolume / 0x18000;
                for (j = 0; j < 256; j++)
                {
                    VolumeTable[i][j] = (short)(Math.Max(Math.Min((j - 128) * temp, short.MaxValue), short.MinValue));
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
            chWk[al].num = num;

            if (pcmData[bank] != null)
            {
                chWk[al].ptr = pcmData[bank][num * 0x12 + 32]
                    + pcmData[bank][num * 0x12 + 1 + 32] * 0x100
                    + pcmData[bank][num * 0x12 + 2 + 32] * 0x10000
                    + pcmData[bank][num * 0x12 + 3 + 32] * 0x1000000
                    + 0x20 + 0x12 * 128;
                if (chWk[al].ptr >= pcmData[bank].Length)
                {
                    chWk[al].ptr = pcmData[bank].Length - 1;
                }
                chWk[al].end = chWk[al].ptr
                    + pcmData[bank][num * 0x12 + 4 + 32]
                    + pcmData[bank][num * 0x12 + 5 + 32] * 0x100
                    + pcmData[bank][num * 0x12 + 6 + 32] * 0x10000
                    + pcmData[bank][num * 0x12 + 7 + 32] * 0x1000000
                    ;
                if (chWk[al].end >= pcmData[bank].Length)
                {
                    chWk[al].end = pcmData[bank].Length - 1;
                }


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
                if (chWk[al].loopStartOffset == 0xffff && chWk[al].loopEndOffset == 0xffff)
                {
                    chWk[al].loopStartOffset = -1;
                    chWk[al].loopEndOffset = -1;
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
                if (isPVI[bank])
                {
                    ret = ConvertPviAdpcmToPziPcm(bank);
                }
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

            if(chWk[al]._loopStartOffset==0xffff || chWk[al]._loopStartOffset>= chWk[al]._loopEndOffset)
            {
                chWk[al]._loopStartOffset = -1;
                chWk[al]._loopEndOffset = -1;
            }
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
        /// 0x16 全体ボリューム
        /// </summary>
        public void SetAllVolume(int vol)
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("ppz8em: SetAllVolume: {0}", vol));
#endif
            if (vol < 16 && vol != PCM_VOLUME)
            {
                PCM_VOLUME = vol;
                MakeVolumeTable(volume);
            }
        }

        //-----------------------------------------------------------------------------
        //	音量調整用
        //-----------------------------------------------------------------------------
        public void SetVolume(int vol)
        {
            if (vol != volume)
            {
                MakeVolumeTable(vol);
            }
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
                if (pcmData[chWk[i].bank] == null) continue;
                if (!chWk[i].playing) continue;
                if (chWk[i].pan == 0) continue;

                if (i == 6)
                {
                    //Console.WriteLine(VolumeTable[chWk[i].volume][pcmData[chWk[i].bank][chWk[i].ptr]]
                    //* chWk[i].panL);
                }

                int n = chWk[i].ptr >= pcmData[chWk[i].bank].Length ? 0x80 : pcmData[chWk[i].bank][chWk[i].ptr];
                l += (int)(VolumeTable[chWk[i].volume][n] * chWk[i].panL);
                r += (int)(VolumeTable[chWk[i].volume][n] * chWk[i].panR);
                chWk[i].delta += ((ulong)chWk[i].srcFrequency * (ulong)chWk[i].frequency / (ulong)0x8000) / SamplingRate;
                chWk[i].ptr += (int)chWk[i].delta;
                chWk[i].delta -= (int)chWk[i].delta;

                if (chWk[i].ptr >= chWk[i].end)
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

            emuRenderBuf[0] = (short)Math.Max(Math.Min(emuRenderBuf[0] + l, short.MaxValue), short.MinValue);
            emuRenderBuf[1] = (short)Math.Max(Math.Min(emuRenderBuf[1] + r, short.MaxValue), short.MinValue);
        }



        private int ConvertPviAdpcmToPziPcm(byte bank)
        {
            int[] table1 = new int[16] {
                1,   3,   5,   7,   9,  11,  13,  15,
                -1,  -3,  -5,  -7,  -9, -11, -13, -15,
            };
            int[] table2 = new int[16] {
                57,  57,  57,  57,  77, 102, 128, 153,
                57,  57,  57,  57,  77, 102, 128, 153,
            };

            List<byte> o = new List<byte>();

            //ヘッダの生成
            o.Add((byte)'P'); o.Add((byte)'Z'); o.Add((byte)'I'); o.Add((byte)'1');
            for (int i = 4; i < 0x0b; i++) o.Add(0);
            byte instCount = pcmData[bank][0xb];
            o.Add(instCount);
            for (int i = 0xc; i < 0x20; i++) o.Add(0);

            //音色テーブルのコンバート
            ulong size2 = 0;
            for (int i = 0; i < instCount; i++)
            {
                uint startaddress = (uint)(pcmData[bank][i * 4 + 0x10] + pcmData[bank][i * 4 + 0x11] * 0x100) << (5 + 1);
                uint size = ((uint)(pcmData[bank][i * 4 + 0x12] + pcmData[bank][i * 4 + 0x13] * 0x100)
                    - (uint)(pcmData[bank][i * 4 + 0x10] + pcmData[bank][i * 4 + 0x11] * 0x100)+1)
                    << (5 + 1);// endAdr - startAdr
                size2 += size;
                short rate = 16000;   // 16kHz

                o.Add((byte)startaddress); o.Add((byte)(startaddress >> 8)); o.Add((byte)(startaddress >> 16)); o.Add((byte)(startaddress >> 24));
                o.Add((byte)size); o.Add((byte)(size >> 8)); o.Add((byte)(size >> 16)); o.Add((byte)(size >> 24));
                o.Add((byte)0xff); o.Add((byte)0xff); o.Add((byte)0); o.Add((byte)0);//loop_start
                o.Add((byte)0xff); o.Add((byte)0xff); o.Add((byte)0); o.Add((byte)0);//loop_end
                o.Add((byte)rate); o.Add((byte)(rate >> 8));//rate
            }

            for (int i = instCount; i < 128; i++)
            {
                o.Add((byte)0); o.Add((byte)0); o.Add((byte)0); o.Add((byte)0);
                o.Add((byte)0); o.Add((byte)0); o.Add((byte)0); o.Add((byte)0);
                o.Add((byte)0xff); o.Add((byte)0xff); o.Add((byte)0); o.Add((byte)0);//loop_start
                o.Add((byte)0xff); o.Add((byte)0xff); o.Add((byte)0); o.Add((byte)0);//loop_end
                short rate = 16000;   // 16kHz
                o.Add((byte)rate); o.Add((byte)(rate >> 8));//rate
            }

            //ADPCM > PCM に変換
            int psrcPtr = 0x10 + 4 * 128;
            for (int i = 0; i < instCount; i++)
            {
                int X_N = 0x80; // Xn     (ADPCM>PCM 変換用)
                int DELTA_N = 127; // DELTA_N(ADPCM>PCM 変換用)

                uint size = ((uint)(pcmData[bank][i * 4 + 0x12] + pcmData[bank][i * 4 + 0x13] * 0x100)
                    - (uint)(pcmData[bank][i * 4 + 0x10] + pcmData[bank][i * 4 + 0x11] * 0x100) + 1)
                    << (5 + 1);// endAdr - startAdr

                for (int j = 0; j < size / 2; j++)
                {
                    byte psrc = pcmData[bank][psrcPtr++];

                    int n = X_N + table1[(psrc >> 4) & 0x0f] * DELTA_N / 8;
                    //Console.WriteLine(n);
                    X_N = Math.Max(Math.Min(n, 32767), -32768);

                    n = DELTA_N * table2[(psrc >> 4) & 0x0f] / 64;
                    //Console.WriteLine(n);
                    DELTA_N = Math.Max(Math.Min(n, 24576), 127);

                    o.Add((byte)(X_N / (32768 / 128) + 128));


                    n = X_N + table1[psrc & 0x0f] * DELTA_N / 8;
                    //Console.WriteLine(n);
                    X_N = Math.Max(Math.Min(n, 32767), -32768);

                    n = DELTA_N * table2[psrc & 0x0f] / 64;
                    //Console.WriteLine(n);
                    DELTA_N = Math.Max(Math.Min(n, 24576), 127);

                    o.Add((byte)(X_N / (32768 / 128) + 128));


                }
            }

            pcmData[bank] = o.ToArray();
            System.IO.File.WriteAllBytes("a.raw", pcmData[bank]);
            return 0;
        }

    }

}
