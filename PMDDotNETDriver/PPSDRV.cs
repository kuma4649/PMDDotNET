﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    //
    //PMDWin ppsdrv.cppから移植
    //



    public class PPSDRV
    {
        private double SamplingRate = 44100.0;
        private int MAX_PPS = 14;
        private byte[] ppsDt = null;
        private PPSHeader[] ppsHd = null;
        private bool single_flag; // 単音モードか？
        private bool low_cpu_check_flag;// 周波数半分で再生か？
        private bool keyon_flag; // Keyon 中か？
        private int data_offset1;
        private int data_offset2;
        private int data_xor1;                              // 現在の位置(小数部)
        private int data_xor2;                              // 現在の位置(小数部)
        private int tick1;
        private int tick2;
        private int tick_xor1;
        private int tick_xor2;
        private int data_size1;
        private int data_size2;
        private int volume1;
        private int volume2;
        private int keyoff_vol;
        private int[] EmitTable=new int[16];


        public PPSDRV(uint SamplingRate = 44100)
        {
            this.SamplingRate = (double)SamplingRate;
            SetVolume(10);

            data_offset1 = data_offset2 = -1;
            data_size1 = data_size2 = -1;
        }

        //-----------------------------------------------------------------------------
        //	音量設定
        //-----------------------------------------------------------------------------
        public void SetVolume(int vol)                 // 音量設定
        {
            //	psg.SetVolume(vol);

            double _base = 0x4000 * 2 / 3.0 *Math.Pow(10.0, vol / 40.0);
            for (int i = 15; i >= 1; i--)
            {
                EmitTable[i] = (int)(_base);
                _base /= 1.189207115;
            }
            EmitTable[0] = 0;


        }

        public void Play(byte al, byte bh, byte bl)
        {
            int num = al;
            int shift = (sbyte)bh;
            //Console.WriteLine(bh);
            int volshift = (sbyte)bl;

            if (ppsHd[num].address < 0) return;

            int a = 225 + ppsHd[num].toneofs;
            a %= 256;
            a += shift;
            a = Math.Min(Math.Max(a, 1), 255);

            if ((byte)(ppsHd[num].volumeofs + volshift) >= 15) return;
            // 音量が０以下の時は再生しない

            if (single_flag == false && keyon_flag)
            {
                //	２重発音処理
                volume2 = volume1;                  // １音目を２音目に移動
                data_offset2 = data_offset1;
                data_size2 = data_size1;
                data_xor2 = data_xor1;
                tick2 = tick1;
                tick_xor2 = tick_xor1;
            }
            else
            {
                //	１音目で再生
                data_size2 = -1;                     // ２音目は停止中
            }

            volume1 = ppsHd[num].volumeofs + volshift;
            data_offset1 = ppsHd[num].address;
            data_size1 = ppsHd[num].length;    // １音目を消して再生
            data_xor1 = 0;
            if (low_cpu_check_flag)
            {
                tick1 = (int)(((8000 * a / 225) << 16) / SamplingRate);
                tick_xor1 = tick1 & 0xffff;
                tick1 >>= 16;
            }
            else
            {
                tick1 = (int)(((16000 * a / 225) << 16) / SamplingRate);
                tick_xor1 = tick1 & 0xffff;
                tick1 >>= 16;
            }

            //	psg.SetReg(0x07, psg.GetReg(0x07) | 0x24);	// Tone/Noise C off
            keyon_flag = true;                      // 発音開始
            return;
        }



        public void Stop()
        {
            keyon_flag = false;
            data_offset1 = data_offset2 = -1;
            data_size1 = data_size2 = -1;
        }

        public bool SetParam(byte paramno, byte data)
        {
            switch (paramno & 1)
            {
                case 0:
                    single_flag = data != 0;
                    return true;
                case 1:
                    low_cpu_check_flag = data != 0;
                    return true;
                default: return false;
            }
        }

        public void int04()
        {
            //TODO: 未実装
        }

        public void Update(short[] emuRenderBuf)
        {
            int i, al1, al2, ah1, ah2;
            int data;

            if (keyon_flag == false && keyoff_vol == 0)
            {
                return;
            }

            //for (i = 0; i < nsamples; i++)
            {
                if (data_size1 > 1)
                {
                    al1 = ppsDt[data_offset1] - volume1;
                    al2 = ppsDt[data_offset1 + 1] - volume1;
                    if (al1 < 0) al1 = 0;
                    if (al2 < 0) al2 = 0;
                }
                else
                {
                    al1 = al2 = 0;
                }

                if (data_size2 > 1)
                {
                    ah1 = ppsDt[data_offset2] - volume2;
                    ah2 = ppsDt[data_offset2 + 1] - volume2;
                    if (ah1 < 0) ah1 = 0;
                    if (ah2 < 0) ah2 = 0;
                }
                else
                {
                    ah1 = ah2 = 0;
                }

                //		al1 = table[(al1 << 4) + ah1];
                //		psg.SetReg(0x0a, al1);
                //if (interpolation)
                //{
                    //data =
                        //(EmitTable[al1] * (0x10000 - data_xor1) + EmitTable[al2] * data_xor1 +
                         //EmitTable[ah1] * (0x10000 - data_xor2) + EmitTable[ah2] * data_xor2) / 0x10000;
                //}
                //else
                {
                    data = EmitTable[al1] + EmitTable[ah1];
                }

                keyoff_vol = (keyoff_vol * 255) / 256;
                data += keyoff_vol;
                data = Math.Min(Math.Max(data, short.MinValue), short.MaxValue);
                emuRenderBuf[0] = (short)Math.Min(Math.Max(emuRenderBuf[0] + data, short.MinValue), short.MaxValue);
                emuRenderBuf[1] = (short)Math.Min(Math.Max(emuRenderBuf[1] + data, short.MinValue), short.MaxValue);

                //		psg.Mix(dest, 1);
                //		dest += 2;

                if (data_size2 > 1)
                {   // ２音合成再生
                    data_xor2 += tick_xor2;
                    if (data_xor2 >= 0x10000)
                    {
                        data_size2--;
                        data_offset2++;
                        data_xor2 -= 0x10000;
                    }
                    data_size2 -= tick2;
                    data_offset2 += tick2;

                    if (low_cpu_check_flag)
                    {
                        data_xor2 += tick_xor2;
                        if (data_xor2 >= 0x10000)
                        {
                            data_size2--;
                            data_offset2++;
                            data_xor2 -= 0x10000;
                        }
                        data_size2 -= tick2;
                        data_offset2 += tick2;
                    }
                }

                data_xor1 += tick_xor1;
                if (data_xor1 >= 0x10000)
                {
                    data_size1--;
                    data_offset1++;
                    data_xor1 -= 0x10000;
                }
                data_size1 -= tick1;
                data_offset1 += tick1;

                if (low_cpu_check_flag)
                {
                    data_xor1 += tick_xor1;
                    if (data_xor1 >= 0x10000)
                    {
                        data_size1--;
                        data_offset1++;
                        data_xor1 -= 0x10000;
                    }
                    data_size1 -= tick1;
                    data_offset1 += tick1;
                }

                if (data_size1 <= 1 && data_size2 <= 1)
                {       // 両方停止
                    if (keyon_flag)
                    {
                        keyoff_vol += EmitTable[ppsDt[data_size1 - 1]];
                    }
                    keyon_flag = false;     // 発音停止
                                            //			psg.SetReg(0x0a, 0);	// Volume を0に
                                            //			return;
                }
                else if (data_size1 <= 1 && data_size2 > 1)
                {   // １音目のみが停止
                    volume1 = volume2;
                    data_size1 = data_size2;
                    data_offset1 = data_offset2;
                    data_xor1 = data_xor2;
                    tick1 = tick2;
                    tick_xor1 = tick_xor2;
                    data_size2 = -1;
                    /*
                                if(data_offset2 != NULL) {
                                    keyoff_vol += EmitTable[data_offset1[data_size1-1]];
                                }
                    */
                }
                else if (data_size1 > 1 && data_size2 < 1)
                {   // ２音目のみが停止
                    if (data_offset2 != -1)
                    {
                        keyoff_vol += EmitTable[ppsDt[data_size2 - 1]];
                        data_offset2 = -1;
                    }
                }
            }
        }

        public void Load(byte[] pcmData)
        {
            if (pcmData == null || pcmData.Length <= MAX_PPS * 6) return;

            List<byte> o = new List<byte>();

            //仮バッファに読み込み
            for (int i = MAX_PPS * 6; i < pcmData.Length; i++)
            {
                o.Add((byte)((pcmData[i] >> 4) & 0xf));
                o.Add((byte)((pcmData[i] >> 0) & 0xf));
            }

            //データの作成
            //	PPS 補正(プチノイズ対策）／160 サンプルで減衰させる
            for (int i = 0; i < MAX_PPS; i++)
            {
                int address = pcmData[i * 6 + 0] + pcmData[i * 6 + 1] * 0x100 - MAX_PPS * 6;
                int leng = pcmData[i * 6 + 2] + pcmData[i * 6 + 3] * 0x100;

                //仮バッファは２倍の大きさにしている為。
                address *= 2;
                leng *= 2;

                int end_pps = address + leng;
                int start_pps = end_pps - 160;//160サンプル
                if (start_pps < address) start_pps = address;

                for (int j = start_pps; j < end_pps; j++)
                {
                    o[j] = (byte)(o[j] - (j - start_pps) * 16 / (end_pps - start_pps));
                    if ((sbyte)o[j] < 0)
                        o[j] = 0;
                }

            }
            ppsDt = o.ToArray();

            //ヘッダの作成
            List<PPSHeader> h = new List<PPSHeader>();
            for (int i = 0; i < MAX_PPS; i++)
            {
                PPSHeader p = new PPSHeader();
                p.address = (pcmData[i * 6 + 0] + pcmData[i * 6 + 1] * 0x100 - MAX_PPS * 6) * 2;
                p.length = (pcmData[i * 6 + 2] + pcmData[i * 6 + 3] * 0x100) * 2;
                p.toneofs = pcmData[i * 6 + 4];
                p.volumeofs = pcmData[i * 6 + 5];

                h.Add(p);
            }
            ppsHd = h.ToArray();

        }
    }

    public class PPSHeader
    {
        public int address;
        public int length;
        public byte toneofs;
        public byte volumeofs;
    }
}
