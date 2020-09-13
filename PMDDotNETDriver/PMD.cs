using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net.Cache;
using System.Resources;
using System.Text;
using musicDriverInterface;
using PMDDotNET.Common;

//;==============================================================================
//;	Professional Music Driver[P.M.D.] version 4.8
//;					FOR PC98(+ Speak Board)
//; By M.Kajihara
//;==============================================================================

namespace PMDDotNET.Driver
{
    public class PMD
    {
        public PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;
        private PPZDRV ppzdrv = null;
        private PCMDRV pcmdrv = null;
        private PCMDRV86 pcmdrv86 = null;
        private EFCDRV efcdrv = null;
        private Func<ChipDatum,int> ppz8em = null;
        private Func<ChipDatum, int> ppsdrv = null;
        private Func<ChipDatum, int> p86em = null;
        public PCMLOAD pcmload = null;
        public Action<ChipDatum> WriteOPNARegister = null;


        public PMD(
            MmlDatum[] mmlData,
            Action<ChipDatum> WriteOPNARegister,
            PW pw,
            Func<string, Stream> appendFileReaderCallback,
            Func<ChipDatum,int> ppz8em,
            Func<ChipDatum, int> ppsdrv,
            Func<ChipDatum, int> p86em)
        {
            this.pw = pw;
            pw.md = mmlData;
            this.ppz8em = ppz8em;
            this.ppsdrv = ppsdrv;
            this.p86em = p86em;

            //pw.md = new MmlDatum[mmlData.Length + 256];
            //Array.Copy(mmlData, 0, pw.md, 0, mmlData.Length);
            //for (int i = 0; i < 256; i++) pw.md[mmlData.Length + i] = new MmlDatum(0);

            pw.board = 1;//音源あり
            //ポート番号の指定
            pw.fm1_port1 = 0x188;//レジスタ
            pw.fm1_port2 = 0x18a;//データ
            pw.fm2_port1 = 0x18c;//レジスタ(拡張)
            pw.fm2_port2 = 0x18e;//データ(拡張)

            r = new x86Register();
            pc98 = new Pc98(WriteOPNARegister, pw);
            pcmload = new PCMLOAD(this, pw, r, pc98, ppz8em, ppsdrv, p86em, appendFileReaderCallback);

            ppzdrv = new PPZDRV(this, pw, r, pc98, ppz8em, pcmload.ppzPcmData);
            pcmdrv = new PCMDRV(this, pw, r, pc98, ppzdrv);
            ppzdrv.pcmdrv = pcmdrv;
            ppzdrv.init();
            pcmdrv86 = new PCMDRV86(this, pw, r, pc98, p86em, pcmload.p86PcmData);
            efcdrv = new EFCDRV(this, pw, r, ppsdrv);
            this.WriteOPNARegister = WriteOPNARegister;

            Set_int60_jumptable();
            Set_n_int60_jumptable();
            SetupCmdtbl();
            SetupCmdtblp();
            SetupCmdtblr();
            SetupComtbl0c0h();

            comstart();
        }

        public void Rendering()
        {
            if (pw.Status == 0) return;

            lock (pw.SystemInterrupt)
            {
                pw.timer.timer();
                pw.timeCounter++;
                if ((pw.timer.StatReg & 3) != 0)
                {
                    lock (r.lockobj)
                    {
                        FM_Timer_main();
                    }
                }
                //work.SystemInterrupt = false;
            }
        }


        //PMD.ASM 127-259
        //;==============================================================================
        //;	ＭＳ－ＤＯＳコールのマクロ
        //;==============================================================================

        private void resident_exit()
        {
            //特に何もしない
        }

        private void resident_cut()
        {
            //特に何もしない
        }

        private void get_psp()
        {
            //特に何もしない
        }

        public void msdos_exit()
        {
            //プログラム終了(エラーコード0)
            throw new Common.PmdDosExitException("msdos_exit");
        }

        public void error_exit(int qq)
        {
            //プログラム終了(エラーコードqq)
            throw new Common.PmdErrorExitException(string.Format("error code:{0}", qq));
        }

        public void print_mes(string qq)
        {
            //コンソールへメッセージ表示
            string[] a = qq.Split(new string[] { "" + (char)13 + (char)10 }, StringSplitOptions.None);
            foreach (string s in a)
                Log.WriteLine(LogLevel.INFO, s);
        }

        public void print_chr(string qq)
        {
            //コンソールへ文字表示
            Log.WriteLine(LogLevel.INFO, qq);
        }

        public void print_line(string bx)
        {
            //コンソールへメッセージ表示(bx位置から0まで)
            Log.WriteLine(LogLevel.INFO, bx);
        }

#if DEBUG
        private PMDDotNET.Common.AutoExtendList<byte> debugBuff = new Common.AutoExtendList<byte>();
        private PMDDotNET.Common.AutoExtendList<byte> debug2Buff = new Common.AutoExtendList<byte>();
#endif

        public void debug(int adr)
        {
#if DEBUG
            debugBuff.Set(adr, (byte)(debugBuff.Get(adr) + 1));
#endif
        }

        public void debug2(int adr, byte dat)
        {
#if DEBUG
            debug2Buff.Set(adr * 2, dat);
#endif
        }

        public void debug_pcm(int adr)
        {
#if DEBUG
            r.al = pc98.InPort(0xa468);//86音源FIFO
            if ((r.al & 0x10) != 0)
            {
                debug(adr);
            }
#endif
        }

        public void _wait()
        {
            //r.cx = (ushort)pw.wait_clock;
            //do
            //{
            //    r.cx--;
            //} while (r.cx > 0);
        }

        public void _waitP()
        {
            //ushort p = r.cx;
            //r.cx = (ushort)pw.wait_clock;
            //do
            //{
            //    r.cx--;
            //}
            //while (r.cx > 0);
            //r.cx = p;
        }

        public void _rwait()//リズム連続出力用wait
        {
            //ushort p = r.cx;
            //r.cx = (ushort)(pw.wait_clock * 32);
            //do
            //{
            //    r.cx--;
            //}
            //while (r.cx > 0);
            //r.cx = p;
        }

        public void rdychk()//Address out時用	break:ax
        {
            r.al = pc98.InPort(r.dx);//無駄読み
            do
            {
                r.al = pc98.InPort(r.dx);
            } while ((r.al & 0x80) != 0);
        }

        public void _ppz()
        {
            //local exit
            if (pw.ppz != 0)
            {
                if (pw.ppz_call_seg >= 2)
                {
                    //		call dword ptr[ppz_call_ofs]
                    throw new NotImplementedException();
                }
            }
        }



        public void int60_main(ushort ax)
        {
            lock (r.lockobj)
            {
                r.ax = ax;

                pw.int60flag++;
                if (r.ah >= int60_max + 1)
                {
                    int60_error();
                    return;
                }

                if (pw.board != 0)
                {
                    int60_start();
                    return;
                }

                int60_start_not_board();
                int60_exit();
            }
        }

        private void int60_exit()
        {
            pw.int60flag--;
            pw.int60_result = 0;
        }

        private void int60_error()
        {
            if (pw.sync != 0)
            {
                if (r.ah == 0xff)//-1
                {
                    opnint_sub();
                    pw.int60flag--;
                    return;
                }
            }
            pw.int60flag--;
            pw.int60_result = 0xff;//-1
        }



        //377-380
        private void getss()
        {
            r.ax = (ushort)pw.syousetu;
        }



        //381-385
        private void getst()
        {
            r.ah = pw.status;
            r.al = pw.status2;//KUMA: 0xff : 演奏終了?
        }



        //386-388
        private void fout()
        {
            pw.fadeout_speed = r.al;
        }

        public void resetOption(string[] pmdOption)
        {
            set_option(pmdOption);
        }



        //389-429
        //;==============================================================================
        //;	ＦＭ効果音演奏メイン
        //;==============================================================================
        private void fm_efcplay()
        {
            r.bx = (ushort)pw.efcdat;
            r.ax = (ushort)(pw.md[r.bx + 254].dat + pw.md[r.bx + 254 + 1].dat * 0x100);
            r.ax += r.bx;
            pw.prgdat_adr2 = r.ax;
            r.di = (ushort)pw.part_e;//offset part_e

            if (pw.board2 != 0)
            {
                r.stack.Push(pw.fm_port1);//;mmainでsel44状態でTimerA割り込みが来た時用の
                r.stack.Push(pw.fm_port2);//;対策
                r.ah = pw.partb;
                r.al = pw.fmsel;
                r.stack.Push(r.ax);
                pw.partb = 3;
                sel46();// ; ここでmmainが来てもsel46のまま
                fmmain();
                r.ax = r.stack.Pop();
                pw.partb = r.ah;
                pw.fmsel = r.al;
                pw.fm_port2 = r.stack.Pop();
                pw.fm_port1 = r.stack.Pop();
            }
            else
            {
                r.al = pw.partb;
                r.stack.Push(r.ax);
                pw.partb = 3;
                fmmain();
                r.ax = r.stack.Pop();
                pw.partb = r.ah;
            }

            if (pw.md[r.si].dat != 0x80)
                goto not_end_fmefc;
            if (pw.partWk[r.di].leng != 0)
                goto not_end_fmefc;

            fm_effect_off();

            not_end_fmefc:;
            return;
        }



        //430-501
        //;==============================================================================
        //;	演奏開始
        //;==============================================================================
        private void mstart_f()
        {
            r.al = pw.TimerAflag;
            r.al |= pw.TimerBflag;
            if (r.al == 0)
            {
                mstart();
                return;
            }

            pw.music_flag |= 1;//TA/TB処理中は 実行しない
            pw.ah_push = 0xff;// -1;
        }

        private void mstart()
        {
            //;------------------------------------------------------------------------------
            //;	演奏停止
            //;------------------------------------------------------------------------------
            //	pushf
            //  cli
            pw.music_flag &= 0xfe;
            mstop();
            //  popf

            //;------------------------------------------------------------------------------
            //;	演奏準備
            //;------------------------------------------------------------------------------
            data_init();
            play_init();

            pw.fadeout_volume = 0;

            if ((pw.board2 | pw.adpcm) != 0)
            {
                if (pw.ademu != 0)
                {
                    r.ax = 0x1800;
                    pw.adpcm_emulate = r.al;
                    ChipDatum cd = new ChipDatum(0x18, r.al, 0);
                    ppz8em(cd);//.SetAdpcmEmu(r.al);// ADPCMEmulate OFF
                    r.bx = (ushort)pw.part10;//offset part10	//PCMを
                    pw.partWk[r.bx].partmask |= 0x10;//Mask(bit4)
                }
                else
                {
                    //;------------------------------------------------------------------------------
                    //;	NECなYM2608なら PCMパートをMASK
                    //;------------------------------------------------------------------------------
                    if (pw.pcm_gs_flag != 0)
                    {
                        r.bx = (ushort)pw.part10;//offset part10	;PCMを
                        pw.partWk[r.bx].partmask |= 4;//Mask(bit2)
                    }
                    //not_mask_pcm:
                }
            }

            if (pw.ppz != 0)
            {
                //;------------------------------------------------------------------------------
                //;	PPZ8初期化
                //;------------------------------------------------------------------------------
                if (pw.ppz_call_seg != 0)
                {
                    r.ax = 0x1901;
                    ChipDatum cd = new ChipDatum(0x19, 0, 0x01);
                    ppz8em(cd);//.SetReleaseFlag(0x01);// 常駐解除禁止
                    r.ah = 0;
                    cd = new ChipDatum(0x00, 0, 0);
                    ppz8em(cd);//.Initialize();
                    //r.ah = 6;
                    //ppz8em.Reserve();
                }
                //not_init_ppz8:
            }

            //;------------------------------------------------------------------------------
            //;	OPN初期化
            //;------------------------------------------------------------------------------
            opn_init();

            //;------------------------------------------------------------------------------
            //;	音楽の演奏を開始
            //;------------------------------------------------------------------------------
            setint();

            pw.play_flag = 1;
            pw.mstart_flag++;
        }



        //502-622
        //;==============================================================================
        //;	各パートのスタートアドレス及び初期値をセット
        //;==============================================================================
        private void play_init()
        {
            r.si = (ushort)pw.mmlbuf;

            r.al = (byte)pw.md[r.si - 1].dat;
            pw.x68_flg = r.al;

            //	;２．６追加分
            pw.prg_flg = 0;
            if (pw.md[r.si].dat != (pw.max_part2 + 1)*2)
            {
                r.bx = Common.Common.GetLe16(pw.md, r.si + (2 * (pw.max_part2 + 1)));
                r.bx += r.si;
                pw.prgdat_adr = r.bx;
                pw.prg_flg = 1;
            }

            //not_prg:
            //prg:

            r.cx = (ushort)pw.max_part2;
            r.dl = 0;
            r.bx = 0;//offset part_data_table
            pw.part_data_table = new int[22];
            for (int i = 0; i < pw.part_data_table.Length; i++) pw.part_data_table[i] = i;//KUMA:順に並んだ配列なので。

            // din0:	
            do
            {
                r.di = (ushort)pw.part_data_table[r.bx];//; di = part workarea//KUMA: 各partのIndexです
                r.bx++;
                r.ax = Common.Common.GetLe16(pw.md, r.si);// ax = part start addr
                r.si += 2;

                r.ax += (ushort)pw.mmlbuf;
                if (pw.md[r.ax].dat == 0x80)//;先頭が80hなら演奏しない
                {
                    r.ax = 0;
                }
                //din1:

                pw.partWk[r.di].address = r.ax;

                pw.partWk[r.di].leng = 1;//; あと１カウントで演奏開始
                r.al = 0xff;//-1
                pw.partWk[r.di].keyoff_flag = r.al;//; 現在keyoff中
                pw.partWk[r.di].mdc = r.al;//; MDepth Counter(無限)
                pw.partWk[r.di].mdc2 = r.al;//
                pw.partWk[r.di]._mdc = r.al;//
                pw.partWk[r.di]._mdc2 = r.al;//
                pw.partWk[r.di].onkai = r.al;// rest
                pw.partWk[r.di].onkai_def = r.al;//; rest
                if (r.dl >= 6) goto din_not_fm;

                //; Part 0,1,2,3,4,5(FM1～6)の時
                pw.partWk[r.di].volume = 108;//; FM VOLUME DEFAULT= 108
                pw.partWk[r.di].fmpan = 0xc0;//; FM PAN = Middle
                if (pw.board2 != 0)
                {
                    pw.partWk[r.di].slotmask = 0xf0;//; FM SLOT MASK
                    pw.partWk[r.di].neiromask = 0xff;//; FM Neiro MASK
                }
                else
                {
                    if (r.dl >= 3) goto din_fm_mask;//OPN 3,4,5 はneiro/slotmaskは0のまま
                    pw.partWk[r.di].slotmask = 0xf0;//; FM SLOT MASK
                    pw.partWk[r.di].neiromask = 0xff;//; FM Neiro MASK
                    goto init_exit;
                din_fm_mask:
                    pw.partWk[r.di].partmask |= 0x20;//; s0の時FMマスク
                }

                goto init_exit;

            din_not_fm:;
                if (r.dl >= 9) goto din_not_psg;
                //; Part 6,7,8(PSG1～3)の時
                pw.partWk[r.di].volume = 8;//; PSG VOLUME DEFAULT= 8
                pw.partWk[r.di].psgpat = 7;//; PSG = TONE
                pw.partWk[r.di].envf = 3;//; PSG ENV = NONE / normal
                goto init_exit;

            din_not_psg:;
                if (r.dl != 9) goto din_not_pcm;
                if (pw.board2 != 0)
                {
                    if (pw.adpcm != 0)
                    {
                        //;	Part 9(OPNA/ADPCM)の時
                        pw.partWk[r.di].volume = 128;//; PCM VOLUME DEFAULT= 128
                        pw.partWk[r.di].fmpan = 0xc0;//; PCM PAN = Middle
                    }
                    if (pw.pcm != 0)
                    {
                        //;	Part 9(OPNA/PCM)の時
                        pw.partWk[r.di].volume = 128;//; PCM VOLUME DEFAULT= 128
                        pw.partWk[r.di].fmpan = 0x00;
                        pw.pcm86_pan_flag = 0;//;Mid
                        pw.revpan = 0;// 逆相off
                    }
                }
                goto init_exit;

            din_not_pcm:;
                if (r.dl != 10) goto not_rhythm;
                //; Part 10(Rhythm) の時
                pw.partWk[r.di].volume = 15;//; PPSDRV volume
                goto init_exit;

            not_rhythm:;
            init_exit:;
                r.dl++;
                r.cx--;

            } while (r.cx > 0);

            //;------------------------------------------------------------------------------
            //;	Rhythm のアドレステーブルをセット
            //;------------------------------------------------------------------------------
            r.ax = Common.Common.GetLe16(pw.md, r.si);// ax = part start addr
            r.si += 2;
            r.ax += (ushort)pw.mmlbuf;
            pw.radtbl = r.ax;

            pw.rhyadr = 0;//offset rhydmy
            pw.rd = pw.rdDmy;//rhyadrはrdDmy(ダミー向け演奏データ配列)を参照する
        }




        //623-714
        //;==============================================================================
        //;	DATA AREA の イニシャライズ
        //;==============================================================================
        private void data_init()
        {
            r.al = 0;
            pw.fadeout_volume = r.al;
            pw.fadeout_speed = r.al;
            pw.fadeout_flag = r.al;
            data_init2();
        }

        private void data_init2()
        { 
            r.cx = (ushort)pw.max_part1;
            r.di = (ushort)pw.part1;//offset part1

            // di_loop:
            do
            {
                r.stack.Push(r.cx);
                r.bx = r.di++;//KUMA:多分、インクリメントしないとだめ。。。
                r.dh = pw.partWk[r.bx].partmask;
                r.dl = pw.partWk[r.bx].keyon_flag;
                r.cx = 0;// type qq//KUMA:partworkの大きさが入る?

                //pushf
                //cli

                r.al = 0;
                pw.partWk[r.bx].Clear();

                r.dh &= 0xf;//0dh;一時,s,m,ADE以外の
                pw.partWk[r.bx].partmask = r.dh;//partmaskのみ保存
                pw.partWk[r.bx].keyon_flag = r.dl;//keyon_flag保存
                pw.partWk[r.bx].onkai = 0xff;// -1;//onkaiを休符設定
                pw.partWk[r.bx].onkai_def = 0xff;// -1;//onkaiを休符設定

                //    popf

                r.cx = r.stack.Pop();
                r.cx--;
            } while (r.cx > 0);

            r.ax = 0;
            pw.tieflag = r.al;
            pw.status = r.al;
            pw.status2 = r.al;
            pw.syousetu = r.ax;
            pw.opncount = r.al;
            pw.TimerAtime = r.al;
            pw.lastTimerAtime = r.al;
            pw.fmKeyOnDataTbl = new byte[6] { 0, 0, 0, 0, 0, 0 };
            //pw.omote_key = new byte[] { 0, 0, 0 };
            pw.omote_key1Ptr = 0;
            pw.omote_key2Ptr = 1;
            pw.omote_key3Ptr = 2;
            //pw.ura_key = new byte[] { 0, 0, 0 };
            pw.ura_key1Ptr = 3;
            pw.ura_key2Ptr = 4;
            pw.ura_key3Ptr = 5;
            pw.fm3_alg_fb = r.al;
            pw.af_check = r.al;
            pw.pcmstart = r.ax;
            pw.pcmstop = r.ax;
            pw.pcmrepeat1 = r.ax;
            pw.pcmrepeat2 = r.ax;
            pw.pcmrelease = 0x8000;
            pw.kshot_dat = r.ax;
            pw.rshot_dat = r.al;
            pw.last_shot_data = r.al;
            pw.slotdetune_flag = r.al;
            pw.slot_detune1 = 0;
            pw.slot_detune2 = 0;
            pw.slot_detune3 = 0;
            pw.slot_detune4 = 0;
            pw.slot3_flag = r.al;
            pw.ch3mode = 0x03f;
            pw.fmsel = r.al;
            pw.syousetu_lng = 96;
            r.ax = (ushort)pw.fm1_port1;
            pw.fm_port1 = r.ax;
            r.ax = (ushort)pw.fm1_port2;
            pw.fm_port2 = r.ax;
            r.al = pw._fm_voldown;
            pw.fm_voldown = r.al;
            r.al = pw._ssg_voldown;
            pw.ssg_voldown = r.al;
            r.al = pw._pcm_voldown;
            pw.pcm_voldown = r.al;
            if (pw.ppz != 0)
            {
                r.al = pw._ppz_voldown;
                pw.ppz_voldown = r.al;
            }
            r.al = pw._rhythm_voldown;
            pw.rhythm_voldown = r.al;
            r.al = pw._pcm86_vol;
            pw.pcm86_vol = r.al;
        }



        //715-870
        //;==============================================================================
        //;	OPN INIT
        //;==============================================================================
        private void opn_init()
        {
            r.dx = 0x2983;
            opnset44();

            pw.psnoi = 0;
            if (pw.effon != 0) goto no_init_psnoi;

            r.dx = 0x0600;//PSG Noise
            opnset44();

            pw.psnoi_last = 0;
        no_init_psnoi:;

            //;==============================================================================
            //; SSG - EG RESET(4.8s)
            //;==============================================================================
            if (pw.board2 != 0)
            {
                r.bx = 2;
                sel44();
            }

        sr01:;

            r.cx = 15;//; 効果音とか気にしない、でいいや…仕様で。
            r.dx = 0x9000;//; SSG - EG = 0
                          //sr02:
            do
            {
                r.al = r.cl;
                r.al &= 3;
                if (r.al == 0) goto sr03;
                opnset();
            sr03:;
                r.dh++;
                r.cx--;
            } while (r.cx > 0);

            if (pw.board2 != 0)
            {
                sel46();
                r.bx--;
                if (r.bx != 0) goto sr01;
                sel44();
            }

            //;==============================================================================
            //; YM2203ならここでおしまい
            //;==============================================================================
            if (pw.board2 == 0)
            {
                if (pw.ongen == 0)//; 2203 ?
                {
                    return;
                }
            }

            //;==============================================================================
            //; 以下YM2608用
            //; PAN / HARDLFO DEFAULT
            //;==============================================================================
            //init_2608:
            //endif

            if (pw.board2 != 0)
            {
                r.bx = 2;
                //;; call sel44; mmainには飛ばない状況下なので大丈夫
            }
        pd01:;

            r.dx = 0xb4c0;//; PAN = MID / HARDLFO = OFF
            r.cx = 3;

            //pd00:
            do
            {
                if (pw.fm_effec_flag != 0)
                {
                    //; ここbugってたわ…(4.8s) //KUMA:ややこしかったので整理。。。
                    if (pw.board2 == 0)
                    {
                        if (r.cx == 1) goto pd03;
                    }
                    else
                    {
                        if (r.cx == 1 && r.bx == 2) goto pd03;
                    }
                }
                //pd02:;
                opnset();
            pd03:;
                r.dh++;

                r.cx--;
            } while (r.cx != 0);

            if (pw.board2 != 0)
            {
                sel46();// mmainには飛ばない状況下なので大丈夫
                r.bx--;
                if (r.bx != 0) goto pd01;
                sel44();// mmainには飛ばない状況下なので大丈夫
            }

            r.dx = 0x2200;//; HARDLFO = OFF
            pw.port22h = r.dl;
            opnset44();

            if (pw.board2 != 0)
            {
                //;==============================================================================
                //; Rhythm Default = Pan : Mid , Vol: 15
                //;==============================================================================
                r.di = 0;//offset rdat
                r.cx = 6;
                r.al = 0b1100_1111;
                do
                {
                    pw.rdat[r.di++] = r.al;
                    r.cx--;
                } while (r.cx != 0);

                r.dx = 0x10ff;
                opnset44();// ; Rhythm All Dump

                //;==============================================================================
                //; リズムトータルレベル セット
                //;==============================================================================
                //rtlset:	
                r.dl = 48;
                r.al = pw.rhythm_voldown;
                if (r.al == 0) goto rtlset2r;

                r.dl <<= 2;//; 0 - 63 > 0 - 255
                r.al = (byte)-r.al;
                r.ax = (ushort)(r.al * r.dl);
                r.dl = r.ah;
                r.dl >>= 2;//; 0 - 255 > 0 - 63
            rtlset2r:;
                pw.rhyvol = r.dl;
                r.dh = 0x11;
                opnset44();

                //;==============================================================================
                //; ＰＣＭ reset &ＬＩＭＩＴ ＳＥＴ
                //;==============================================================================
                if (pw.ademu == 0)
                {
                    if (pw.pcm_gs_flag != 1)
                    {
                        r.dx = 0xcff;
                        opnset46();
                        r.dx = 0xdff;
                        opnset46();
                    }
                    //pr_non_pcm:;
                }

                //;==============================================================================
                //; PPZ Pan Init.
                //;==============================================================================
                if (pw.ppz + pw.ademu != 0)
                {
                    r.dx = 5;
                    r.ax = 0x1300;
                    r.cx = 8;
                    //ppz_pan_init_loop:
                    do
                    {
                        r.al = r.cl;
                        r.al--;
                        ChipDatum cd = new ChipDatum(0x13, r.al, r.dx);
                        ppz8em(cd);//.SetPan(r.al, r.dx);
                        r.cx--;
                    } while (r.cx != 0);
                }
            }
            else
            {
                r.dx = (ushort)pw.fm2_port1;
                //    pushf
                //    cli
                rdychk();
                r.al = 0x10;
                pc98.OutPort(r.dx, r.al);
                r.dx = (ushort)pw.fm2_port2;
                _wait();
                r.al = 0x80;
                pc98.OutPort(r.dx, r.al);
                _wait();
                r.al = 0x18;
                pc98.OutPort(r.dx, r.al);
                //    popf
            }
        }



        //871-896
        //;==============================================================================
        //;	ＭＵＳＩＣ ＳＴＯＰ
        //;==============================================================================
        private void mstop_f()
        {
            r.al = pw.TimerAflag;
            r.al |= pw.TimerBflag;
            if (r.al != 0)
            {
                pw.music_flag |= 2;//TA/TB処理中は 実行しない
                pw.ah_push = 0xff;//-1
                return;
            }
            //_mstop:	
            pw.fadeout_flag = 0;//外部からmstopさせた場合は0にする
            
            //mstop();//KUMA:外部からの場合も同じ処理をさせる(別スレッドから音源を操作させない)
            pw.music_flag |= 2;
            pw.ah_push = 0xff;
        }

        private void mstop()
        {
            //    pushf
            //    cli
            pw.music_flag &= 0xfd;
            r.ax = 0;
            pw.play_flag = r.al;
            pw.pause_flag = r.al;
            pw.fadeout_speed = r.al;
            r.al--;
            pw.status2 = r.al;
            pw.fadeout_volume = r.al;
            //    popf
            silence();
        }



        //897-1024
        //;==============================================================================
        //;	MUSIC PLAYER MAIN[FROM TIMER - B]
        //;==============================================================================
        private void mmain()
        {
            pw.loop_work = 3;

            if (pw.x68_flg != 0) goto mmain_fm;


            r.di = (ushort)pw.part7;//offset part7
            pw.partb = 1;
            psgmain();//; SSG1

            r.di = (ushort)pw.part8;//offset part8
            pw.partb = 2;
            psgmain();//; SSG2

            r.di = (ushort)pw.part9;//offset part9
            pw.partb = 3;
            psgmain();//; SSG3


        mmain_fm:;
            if (pw.board2 != 0)
            {
                sel46();

                r.di = (ushort)pw.part4;//offset part4
                pw.partb = 1;
                fmmain();//; FM4 OPNA

                r.di = (ushort)pw.part5;//offset part5
                pw.partb = 2;
                fmmain();//; FM5 OPNA

                r.di = (ushort)pw.part6;//offset part6
                pw.partb = 3;
                fmmain();//; FM6 OPNA

                sel44();
            }


            r.di = (ushort)pw.part1;//offset part1
            pw.partb = 1;
            fmmain();//; FM1

            r.di = (ushort)pw.part2;//offset part2
            pw.partb = 2;
            fmmain();//; FM2

            r.di = (ushort)pw.part3;//offset part3
            pw.partb = 3;
            fmmain();//; FM3


            r.di = (ushort)pw.part3b;//offset part3b
            fmmain();//; FM3 拡張１

            r.di = (ushort)pw.part3c;//offset part3c
            fmmain();//; FM3 拡張２

            r.di = (ushort)pw.part3d;//offset part3d
            fmmain();//; FM3 拡張３


            if (pw.x68_flg != 0) goto mmain_exit;


            r.di = (ushort)pw.part11;//offset part11
            rhythmmain();//; RHYTHM


            if (pw.board2 != 0)
            {
                r.di = (ushort)pw.part10;//offset part10
                if (pw.useP86DRV)
                {
                    pcmdrv86.pcmmain();//; ADPCM/PCM(IN "pcmdrv.asm"/"pcmdrv86.asm")
                }
                else
                {
                    pcmdrv.pcmmain();
                }

                if (pw.ppz != 0)
                {
                    r.di = (ushort)pw.part10a;//offset part10a
                    pw.partb = 0;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10b;//offset part10b
                    pw.partb = 1;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10c;//offset part10c
                    pw.partb = 2;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10d;//offset part10d
                    pw.partb = 3;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10e;//offset part10e
                    pw.partb = 4;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10f;//offset part10f
                    pw.partb = 5;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10g;//offset part10g
                    pw.partb = 6;
                    ppzdrv.ppzmain();

                    r.di = (ushort)pw.part10h;//offset part10h
                    pw.partb = 7;
                    ppzdrv.ppzmain();
                }

            }


        mmain_exit:;

            if (pw.loop_work != 0) goto mmain_loop;
            return;

        mmain_loop:;

            r.cx = (ushort)pw.max_part1;
            r.bx = 0;//offset part_data_table

            //mm_din0:;
            do
            {
                r.di = (ushort)pw.part_data_table[r.bx];//[bx]; di = part workarea
                r.bx++;

                if (pw.partWk[r.di].loopcheck == 3) goto mm_notset;
                pw.partWk[r.di].loopcheck = 0;

            mm_notset:;
                r.cx--;
            } while (r.cx != 0);

            if (pw.loop_work == 3) goto mml_fin;

            pw.status2++;
            if (pw.status2 != 0xff)//; -1にはさせない
                goto mml_ret;

            pw.status2 = 1;
        mml_ret:;
            return;

        mml_fin:;
            pw.status2 = 0xff;// -1;
        }



        //1026-1035
        //;==============================================================================
        //;	裏ＦＭセレクト
        //;==============================================================================
        private void sel46()
        {
            r.ax = (ushort)pw.fm2_port1;
            pw.fm_port1 = r.ax;
            r.ax = (ushort)pw.fm2_port2;
            pw.fm_port2 = r.ax;
            pw.fmsel = 1;
        }



        //1036-1046
        //;==============================================================================
        //;	表に戻す
        //;==============================================================================
        private void sel44()
        {
            r.ax = (ushort)pw.fm1_port1;
            pw.fm_port1 = r.ax;
            r.ax = (ushort)pw.fm1_port2;
            pw.fm_port2 = r.ax;
            pw.fmsel = 0;
        }



        //1047-1210
        //;==============================================================================
        //;	ＦＭ音源演奏メイン
        //;==============================================================================
        //private void fmmain_ret()
        //{
        //	ret
        //}

        private void fmmain()
        {
            r.si = (ushort)pw.partWk[pw.part_data_table[r.di]].address; //; si = PART DATA ADDRESS
            if (r.si == 0) return;

            Func<object> ret = null;
            if (pw.partWk[r.di].partmask != 0)
                ret = fmmain_nonplay;
            else
                ret = fmmain_c_1;

            if (ret != null)
            {
                do
                {
                    ret = (Func<object>)ret();
                } while (ret != null);
            }
        }

        private Func<object> fmmain_c_1()
        {
            //; 音長 -1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //; KEYOFF CHECK & Keyoff
            if ((pw.partWk[r.di].keyoff_flag & 3) != 0)//; 既にkeyoffしたか？
                goto mp0;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                goto mp0;

            keyoff();//; ALは壊さない
            pw.partWk[r.di].keyoff_flag = 0xff;//-1

        mp0:;//; LENGTH CHECK
            if (r.al != 0) return mpexit;
            return mp10;
        }

        private Func<object> mp10()
        {
            pw.partWk[r.di].lfoswi &= 0xf7;//; Porta off
            return mp1;
        }

        private Func<object> mp1()//; DATA READ
        {
            do
            {
                pw.cmd = pw.md[r.si];

                r.al = (byte)pw.md[r.si++].dat;
                if (r.al < 0x80) 
                    goto mp2;
                if (r.al == 0x80) goto mp15;

                //; ELSE COMMANDS
                object o = commands();
                while (o != null && (Func<object>)o != mp1)
                {
                    o = ((Func<object>)o)();
                    if ((Func<object>)o == mnp_ret)
                        return mnp_ret;
                    if ((Func<object>)o == porta_return)
                        return porta_return;
                }
            } while (true);

        //; END OF MUSIC["L"があった時はそこに戻る]
        mp15:;
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) return mpexit;

            //; "L"があった時
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            return mp1;

        mp2:;//; F-NUMBER SET
            lfoinit();
            oshift();
            fnumset();

            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = pw.cmd;
            WriteOPNARegister(cd);

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            calc_q();
            return porta_return();
        }
        private Func<object> porta_return()
        { 
            if (pw.partWk[r.di].volpush == 0) goto mp_new;
            if (pw.partWk[r.di].onkai == 0xff) goto mp_new;
            pw.volpush_flag--;
            if (pw.volpush_flag == 0) goto mp_new;
            pw.volpush_flag = 0;
            pw.partWk[r.di].volpush = 0;
        mp_new:;
            volset();
            otodasi();
            keyon();
            pw.partWk[r.di].keyon_flag++;
            pw.partWk[r.di].address = r.si;
            r.al = 0;
            pw.tieflag = r.al;
            pw.volpush_flag = r.al;
            pw.partWk[r.di].keyoff_flag = r.al;
            if (pw.md[r.si].dat != 0xfb)//; '&'が直後にあったらkeyoffしない
                return mnp_ret;
            pw.partWk[r.di].keyoff_flag = 2;
            return mnp_ret;
        }

        private Func<object> mpexit()//; LFO & Portament & Fadeout 処理 をして終了
        { 
            if (pw.board2 != 0)
            {
                if (pw.partWk[r.di].hldelay_c != 0)
                {
                    pw.partWk[r.di].hldelay_c--;
                    if (pw.partWk[r.di].hldelay_c == 0)
                    {
                        r.dh = pw.partb;
                        r.dh += 0xb4 - 1;
                        r.dl = pw.partWk[r.di].fmpan;
                        opnset();
                    }
                }
                //not_hldelay:;
            }
            if (pw.partWk[r.di].sdelay_c != 0)
            {
                pw.partWk[r.di].sdelay_c--;
                if (pw.partWk[r.di].sdelay_c == 0)
                {
                    if ((pw.partWk[r.di].keyoff_flag & 1) == 0)//; 既にkeyoffしたか？
                    {
                        keyon();
                    }
                }
            }
            //not_sdelay:;
            r.cl = pw.partWk[r.di].lfoswi;
            if ((r.cl & r.cl) == 0)
            {
                //goto nolfosw;//飛ばずに処理
                if (pw.fadeout_speed != 0)
                {
                    volset();
                }
                return mnp_ret;
            }
            r.al = r.cl;
            r.al &= 8;
            pw.lfo_switch = r.al;
            if ((r.cl & 3) != 0)
            {
                lfo();
                if (r.carry)
                {
                    r.al = r.cl;
                    r.al &= 3;
                    pw.lfo_switch |= r.al;
                }
            }
            //not_lfo:
            if ((r.cl & 0x30) != 0)
            {
                //pushf
                //cli
                lfo_change();
                lfo();
                if (r.carry)
                {
                    lfo_change();
                    //popf
                    r.al = pw.partWk[r.di].lfoswi;
                    r.al &= 0x30;
                    pw.lfo_switch |= r.al;
                }
                else
                {
                    //not_lfo1:
                    lfo_change();
                    //    popf
                }
            }
            //not_lfo2:
            if ((pw.lfo_switch & 0x19) != 0)
            {
                if ((pw.lfo_switch & 8) != 0)
                {
                    porta_calc();
                }
                //not_porta:
                otodasi();
            }
            //vols:
            if ((pw.lfo_switch & 0x22) == 0)
            {
                //nolfosw:
                if (pw.fadeout_speed == 0) return mnp_ret;
            }
            //vol_set:
            volset();
            return mnp_ret;
        }



        public Func<object> mnp_ret()
        {
            r.al = pw.loop_work;
            r.al &= pw.partWk[r.di].loopcheck;
            pw.loop_work = r.al;
            _ppz();
            return null;
        }



        //1211-1267
        //;==============================================================================
        //;	Q値の計算
        //;		break	dx
        //;==============================================================================
        public void calc_q()
        {
            if (pw.md[r.si].dat == 0xc1) //&&
                goto cq_sular;

            r.dl = pw.partWk[r.di].qdata;
            if (pw.partWk[r.di].qdatb == 0)
                goto cq_set;

            r.stack.Push(r.ax);
            r.al = pw.partWk[r.di].leng;
            r.ax = (ushort)(r.al * pw.partWk[r.di].qdatb);
            r.dl += r.ah;
            r.ax = r.stack.Pop();

        cq_set:;
            if (pw.partWk[r.di].qdat3 == 0)
                goto cq_set2;

            //; Random-Q
            r.stack.Push(r.ax);
            r.stack.Push(r.cx);
            r.al = pw.partWk[r.di].qdat3;
            r.al &= 0x7f;
            r.ax = (ushort)(sbyte)r.al; // cbw
            r.ax++;

            r.stack.Push(r.dx);
            rnd();
            r.dx = r.stack.Pop();

            if ((pw.partWk[r.di].qdat3 & 0x80) != 0)
                goto cqr_minus;

            r.dl += r.al;
            goto cqr_exit;

        cqr_minus:;
            r.carry = (r.dl - r.al) < 0;
            r.dl -= r.al;
            if (!r.carry) goto cqr_exit;
            r.dl = 0;

        cqr_exit:;
            r.cx = r.stack.Pop();
            r.ax = r.stack.Pop();

        cq_set2:;
            if (pw.partWk[r.di].qdat2 == 0)
                goto cq_sete;

            r.dh = pw.partWk[r.di].leng;
            r.carry = (r.dh - pw.partWk[r.di].qdat2) < 0;
            r.dh -= pw.partWk[r.di].qdat2;
            if (r.carry)
                goto cq_zero;
            if (r.dl - r.dh < 0)
                goto cq_sete;
            r.dl = r.dh;//; 最低保証gate値設定

        cq_sete:;
            pw.partWk[r.di].qdat = r.dl;
            return;

        cq_sular:;
            r.si++;//; スラー命令
        cq_zero:;
            pw.partWk[r.di].qdat = 0;
            return;
        }



        //1268-1322
        //;==============================================================================
        //;	ＦＭ音源演奏メイン：パートマスクされている時
        //;==============================================================================
        // false : goto mnp_ret
        // true : goto mp10
        private Func<object> fmmain_nonplay()
        {
            pw.partWk[r.di].keyoff_flag = 0xff;// -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return mnp_ret;

            if ((pw.partWk[r.di].partmask & 2) != 0)//; bit1(FM効果音中？)をcheck
            {
                if (pw.fm_effec_flag == 0)//	; 効果音終了したか？
                {
                    pw.partWk[r.di].partmask &= 0xfd;//;bit1をclear
                    if (pw.partWk[r.di].partmask == 0) return mp10;//;partmaskが0なら復活させる
                }
            }
            return fmmnp_1;
        }

        private Func<object> fmmnp_1()
        { 
            do
            {
                do
                {
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;
                    if (r.al < 0x80) return fmmnp_3;

                    object o = commands();
                    while (o != null && (Func<object>)o != fmmnp_1)
                    {
                        o = ((Func<object>)o)();
                        if ((Func<object>)o == mnp_ret) return mnp_ret;
                    }

                } while (true);

                //fmmnp_2:
                //	; END OF MUSIC["L"があった時はそこに戻る]
                r.si--;
                pw.partWk[r.di].address = r.si;
                pw.partWk[r.di].loopcheck = 3;
                pw.partWk[r.di].onkai = 0xff;//-1
                r.bx = pw.partWk[r.di].partloop;
                if ((r.bx & r.bx) == 0) return fmmnp_4;
                //    ; "L"があった時
                r.si = r.bx;
                pw.partWk[r.di].loopcheck = 1;
            } while (true);
        }

        public Func<object> fmmnp_3()
        {
            pw.partWk[r.di].fnum = 0;//; 休符に設定
            pw.partWk[r.di].onkai = 0xff;//-1
            pw.partWk[r.di].onkai_def = 0xff;//-1

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;//;音長設定
            pw.partWk[r.di].keyon_flag++;
            pw.partWk[r.di].address = r.si;

            pw.volpush_flag--;
            if (pw.volpush_flag != 0)
            {
                pw.partWk[r.di].volpush = 0;
            }

            return fmmnp_4;
        }

        public Func<object> fmmnp_4()
        {
            pw.tieflag = 0;
            pw.volpush_flag = 0;
            return mnp_ret;
        }



        //1323-1459
        //;==============================================================================
        //;	ＳＳＧ音源 演奏 メイン
        //;==============================================================================
        //psgmain_ret:
        //	ret

        private void psgmain()
        {
            r.si = (ushort)pw.partWk[pw.part_data_table[r.di]].address; //; si = PART DATA ADDRESS
            if (r.si == 0) return;

            Func<object> ret = null;
            if (pw.partWk[r.di].partmask != 0)
                ret = psgmain_nonplay;
            else
                ret = psgmain_c_1;

            if (ret != null)
            {
                do
                {
                    ret = (Func<object>)ret();
                } while (ret != null);
            }
        }

        private Func<object> psgmain_c_1()
        {
            //; 音長 -1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //; KEYOFF CHECK & Keyoff
            if ((pw.partWk[r.di].keyoff_flag & 3) != 0)//; 既にkeyoffしたか？
                return mp0p;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                return mp0p;

            keyoffp();//; ALは壊さない
            pw.partWk[r.di].keyoff_flag = 0xff;//-1

            return mp0p;
        }

        private Func<object> mp0p()//; LENGTH CHECK
        {
            if (r.al != 0) return mpexitp;

            pw.partWk[r.di].lfoswi &= 0xf7;//; Porta off

            return mp1p;
        }

        private Func<object> mp1p()//; DATA READ
        {
            pw.cmd = pw.md[r.si];
            r.al = (byte)pw.md[r.si++].dat;
            if (r.al < 0x80) return mp2p;
            if (r.al == 0x80) return mp15p;
            return mp1cp;
        }
        //; ELSE COMMANDS
        private Func<object> mp1cp()
        {
            object o = commandsp();
            while (o != null && (Func<object>)o != mp1cp && (Func<object>)o != mp1p)
            {
                o = ((Func<object>)o)();
                if ((Func<object>)o == mnp_ret) return mnp_ret;
            }

            return mp1p;
        }

        //; END OF MUSIC["L"があった時はそこに戻る]
        private Func<object> mp15p()
        { 
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) return mpexitp;

            //; "L"があった時
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            return mp1p;
        }

        private Func<object> mp2p()//; TONE SET
        {
            lfoinitp();
            oshiftp();
            fnumsetp();

            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = pw.cmd;
            WriteOPNARegister(cd);

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            calc_q();
            return porta_returnp();
        }

        private Func<object> porta_returnp()
        { 
            if (pw.partWk[r.di].volpush != 0)
            {
                if (pw.partWk[r.di].onkai != 0xff)
                {
                    pw.volpush_flag--;
                    if (pw.volpush_flag != 0)
                    {
                        pw.volpush_flag = 0;
                        pw.partWk[r.di].volpush = 0;
                    }
                }
            }
            //mp_newp:;
            volsetp();
            otodasip();
            keyonp();
            pw.partWk[r.di].keyon_flag++;
            pw.partWk[r.di].address = r.si;
            r.al = 0;
            pw.tieflag = r.al;
            pw.volpush_flag = r.al;
            pw.partWk[r.di].keyoff_flag = r.al;
            if (pw.md[r.si].dat != 0xfb)//; '&'が直後にあったらkeyoffしない
            {
                return mnp_ret;
            }
            pw.partWk[r.di].keyoff_flag = 2;
            return mnp_ret;
        }

        private Func<object> mpexitp()
        { 
            r.cl = pw.partWk[r.di].lfoswi;
            r.al = r.cl;
            r.al &= 8;
            pw.lfo_switch = r.al;

            if ((r.cl & r.cl) == 0) goto volsp;

            if ((r.cl & 3) != 0)
            {
                lfop();
                if (r.carry)
                {
                    r.al = r.cl;
                    r.al &= 3;
                    pw.lfo_switch |= r.al;
                }
            }
            //not_lfop:
            if ((r.cl & 0x30) != 0)
            {
                // pushf
                //    cli
                lfo_change();
                lfop();
                if (r.carry)
                {
                    lfo_change();
                    //    popf
                    r.al = pw.partWk[r.di].lfoswi;
                    r.al &= 0x30;
                    pw.lfo_switch |= r.al;
                }
                else
                {
                    //not_lfo1:
                    lfo_change();
                    //    popf
                }
            }
            //not_lfop2:
            if ((pw.lfo_switch & 0x19) != 0)
            {
                if ((pw.lfo_switch & 8) != 0)
                {
                    porta_calc();
                }
                //not_porta:
                otodasip();
            }
        volsp:;
            soft_env();
            if (!r.carry)
            {
                if ((pw.lfo_switch & 0x22) == 0)
                {
                    if (pw.fadeout_speed == 0)
                    {
                        return mnp_ret();
                    }
                }
            }
            //volsp2:;
            volsetp();
            return mnp_ret();
        }




        //1460-1502
        //;==============================================================================
        //;	ＳＳＧ音源演奏メイン：パートマスクされている時
        //;==============================================================================
        private Func<object> psgmain_nonplay()
        {
            pw.partWk[r.di].keyoff_flag = 0xff;// -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return mnp_ret;

            pw.partWk[r.di].lfoswi &= 0xf7;//;Porta off
            return psgmnp_1;
        }

        private Func<object> psgmnp_1()
        { 
            do
            {
                do
                {
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;
                    if (r.al < 0x80) goto psgmnp_4;

                    if (r.al != 0xda) goto psgmnp_3;//;Portament?
                    ssgdrum_check();//;の場合だけSSG復活Check
                    if (r.carry) return mp1cp;//;復活の場合はメインの処理へ
                    psgmnp_3:;

                    object o = commandsp();
                    while (o != null && (Func<object>)o != psgmnp_1)
                    {
                        o = ((Func<object>)o)();
                        if ((Func<object>)o == mnp_ret) 
                            return mnp_ret;
                        if ((Func<object>)o == porta_returnp) 
                            return porta_returnp;
                    }

                } while (true);

                //    ; END OF MUSIC["L"があった時はそこに戻る]
                //psgmnp_2:
                r.si--;
                pw.partWk[r.di].address = r.si;
                pw.partWk[r.di].loopcheck = 3;
                pw.partWk[r.di].onkai = 0xff;//-1
                r.bx = pw.partWk[r.di].partloop;

                if ((r.bx & r.bx) == 0) return fmmnp_4;

                //    ; "L"があった時
                r.si = r.bx;
                pw.partWk[r.di].loopcheck = 1;
            } while (true);

        psgmnp_4:;
            ssgdrum_check();
            if (!r.carry) return fmmnp_3;

            return mp2p;//; SSG復活
        }



        //1503-1532
        //;==============================================================================
        //;	SSGドラムを消してSSGを復活させるかどうかcheck
        //;		input AL<- Command
        //; output cy = 1 : 復活させる
        //;==============================================================================
        private void ssgdrum_check()
        {
            if ((pw.partWk[r.di].partmask & 1) != 0)//; bit0(SSGマスク中？)をcheck
                goto sdrchk_2; //;SSGマスク中はドラムを止めない
            if ((pw.partWk[r.di].partmask & 2) == 0)//;bit1(SSG効果音中？)をcheck
                goto sdrchk_2; //;SSGドラムは鳴ってない
            if (pw.effon >= 2)//; SSGドラム以外の効果音が鳴っているか？
                goto sdrchk_2; //;普通の効果音は消さない
            r.ah = r.al;       //;ALは壊さない
            r.ah &= 0xf;//;0DAH(portament)の時は0AHなので大丈夫
            if (r.ah == 0xf)//;休符？
                goto sdrchk_2;// ; 休符の時はドラムは止めない
            if (pw.effon != 1)//; SSGドラムはまだ再生中か？
                goto sdrchk_1;//; 既に消されている
            r.stack.Push(r.ax);
            efcdrv.effend();//;SSGドラムを消す
            r.ax = r.stack.Pop();

        sdrchk_1:;
            pw.partWk[r.di].partmask &= 0xfd;//;bit1をclear
            if (pw.partWk[r.di].partmask != 0)
                goto sdrchk_2;//;まだ何かでマスクされている
            r.carry = true;
            return;//;partmaskが0なら復活させる

        sdrchk_2:;
            r.carry = false;
            return;
        }



        //1533-1601
        //;==============================================================================
        //;	リズムパート 演奏 メイン
        //;==============================================================================
        //private void rhythmmain_ret(){
        //	return;
        //}

        private void rhythmmain()
        {
            r.si = (ushort)pw.partWk[pw.part_data_table[r.di]].address; //; si = PART DATA ADDRESS
            if (r.si == 0) return;

            //; 音長 -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0)
            {
                mnp_ret();
                return;
            }

            //rhyms0:	
            r.bx = (ushort)pw.rhyadr;
            rhyms00();
        }

        private void rhyms00()
        {
        rhyms00:;
            r.al = (byte)pw.rd[r.bx].dat;//rdにはmd(正規の演奏データ)或いはrdDmy(ダミーの演奏データ)のどちらかがセットされている
            r.bx++;

            if (r.al == 0xff)
            {
                reom();
                return;
            }
            if ((r.al & 0x80) != 0)
            {
                int r = rhythmon();
                if (r == 1) goto rhyms00;
                return;
            }

            pw.kshot_dat = 0;//; rest
            rlnset();
        }

        private void rlnset()
        {
            r.al = (byte)pw.rd[r.bx].dat;// mov al,[bx]
            r.bx++;

            pw.rhyadr = r.bx;
            pw.partWk[r.di].leng = r.al;
            pw.partWk[r.di].keyon_flag++;

            fmmnp_4();
            mnp_ret();
        }

        //private void mnp_ret()
        //{ 
        //    r.al = pw.loop_work;
        //    r.al &= pw.partWk[r.di].loopcheck;
        //    pw.loop_work = r.al;
        //    _ppz();
        //    return;
        //}

        private void reom()
        {
        reom:;
            do
            {
                r.al = (byte)pw.md[r.si++].dat;
                if (r.al == 0x80) goto rfin;
                if (r.al < 0x80) break;

                object o = commandsr();
                while (o != null)
                {
                    o = ((Func<object>)o)();
                }
            } while (true);

            //re00:
            pw.partWk[r.di].address = r.si;
            r.ah = 0;
            r.ax += r.ax;
            r.ax += (ushort)pw.radtbl;
            r.bx = r.ax;
            r.ax = (ushort)(pw.md[r.bx].dat + pw.md[r.bx + 1].dat * 0x100);// mov ax,[bx]

            r.ax += (ushort)pw.mmlbuf;
            pw.rhyadr = r.ax;
            r.bx = r.ax;
            pw.rd = pw.md;

            rhyms00:;
            {
                r.al = (byte)pw.rd[r.bx].dat;//	mov al,[bx]
                r.bx++;

                if (r.al == 0xff)
                {
                    goto reom;
                }
                if ((r.al & 0x80) != 0)
                {
                    int r = rhythmon();
                    if (r == 1) goto rhyms00;
                    return;
                }

                pw.kshot_dat = 0;//; rest
                rlnset();
                return;
            }


        rfin:;
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) goto rf00;

            //    ; "L"があった時
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            goto reom;

        rf00:;
            r.bx = 0;//offset rhydmy
            pw.rhyadr = r.bx;
            pw.rd = pw.rdDmy;

            fmmnp_4();
            mnp_ret();
        }



        //1602-1709
        //;==============================================================================
        //;	PSGﾘｽﾞﾑ ON
        //;==============================================================================
        private int rhythmon()
        {
            if ((r.al & 0b0100_0000) == 0)
                goto rhy_shot;
            ushort a = r.si;
            r.si = r.bx;
            r.bx = a;
            r.stack.Push(r.bx);

            object o = commandsr();
            while (o != null)
            {
                o = ((Func<object>)o)();
            }

            r.bx = r.stack.Pop();
            a = r.si;
            r.si = r.bx;
            r.bx = a;
            //rhyms00();
            return 1;

        rhy_shot:;
            if (pw.partWk[r.di].partmask == 0)
                goto r_nonmask;
            pw.kshot_dat = 0;
            r.bx++;
            rlnset();//; maskされている場合
            return 0;
        r_nonmask:;
            r.ah = r.al;
            r.al = (byte)pw.rd[r.bx].dat;
            r.bx++;
            r.ax &= 0x3fff;
            pw.kshot_dat = r.ax;
            if (r.ax == 0)
            {
                rlnset();
                return 0;
            }
            pw.rhyadr = r.bx;
            if (pw.board2 != 0)
            {
                if (pw.kp_rhythm_flag == 0)
                    goto rsb210;
                //KUMA:SB2でkp_rhythm_flagなら、リズム音源もならす
                r.stack.Push(r.ax);
                r.bx = 0;//offset rhydat
                r.cx = 11;
            //rsb2lp:;
                do
                {
                    r.carry = ((r.ax & 1) != 0);
                    r.ax = (ushort)((r.ax >> 1) | (r.carry ? 0x8000 : 0));
                    if (r.carry)
                    {
                        rshot();
                        goto rsb200;
                    }
                    r.bx++;
                    r.bx++;
                rsb200:;
                    r.bx++;
                    r.cx--;
                } while (r.cx != 0);
                r.ax = r.stack.Pop();
            }
        rsb210:;
            r.bx = r.ax;
            if (pw.fadeout_volume == 0)
                goto rpsg;
            if (pw.board2 != 0)
            {
                if (pw.kp_rhythm_flag == 0)
                    goto rpps_check;
                r.dl = pw.rhyvol;
                volset2rf();
            rpps_check:;
            }
            if (pw.ppsdrv_flag == 0)
                goto roret;//; fadeout時ppsdrvでなら発音しない
            rpsg:;
            r.al = 0xff;//-1
        rolop:;
            r.al++;
            r.carry = ((r.bx & 1) != 0);
            r.bx >>= 1;
            if (r.carry) goto rhygo;
            goto rolop;
        rhygo:;
            r.stack.Push(r.di);
            r.stack.Push(r.si);
            r.stack.Push(r.bx);
            r.stack.Push(r.ax);
            efcdrv.effgo();
            r.ax = r.stack.Pop();
            r.bx = r.stack.Pop();
            r.si = r.stack.Pop();
            r.di = r.stack.Pop();

            if (pw.ppsdrv_flag == 0)
                goto roret;
            if (r.bx == 0)
                goto roret;
            goto rolop;//; PPSDRVなら２音目以上も鳴らしてみる
        roret:;
            r.bx = (ushort)pw.rhyadr;
            rlnset();
            return 0;
        }

        private void rshot()
        { 
            if (pw.board2 != 0)
            {
            //rshot:;
                r.dx = (ushort)(pw.rhydat[r.bx] + pw.rhydat[r.bx + 1] * 0x100);
                byte x = r.dh;
                r.dh = r.dl;
                r.dl = x;
                r.bx++;
                r.bx++;
                opnset44();
                r.dh = 0x10;
                r.dl = pw.rhydat[r.bx];
                r.dl &= pw.rhythmmask;
                if (r.dl == 0)
                    return;// goto rsb200;
                if ((r.dl & 0x80) == 0)
                    goto rshot00;
                r.dl = 0b1000_0100;
                opnset44();
                r.dl = 0b0000_1000;
                r.dl &= pw.rhythmmask;
                if (r.dl == 0)
                    return;// goto rsb200;
                _rwait();
            rshot00:;
                opnset44();
                return;// goto rsb200;
            }
        }



        //1710-1742
        //;==============================================================================
        //;	各種特殊コマンド処理
        //;==============================================================================
        private Func<object> commands()
        {
            pw.currentCommandTable = cmdtbl;
            pw.currentWriter = 0;
            r.bx = 0;//offset cmdtbl
            return command00();
        }

        private Func<object> commandsr()
        {
            pw.currentCommandTable = cmdtblr;
            pw.currentWriter = 1;
            r.bx = 0;//offset cmdtblr
            return command00();
        }

        private Func<object> commandsp()
        {
            pw.currentCommandTable = cmdtblp;
            pw.currentWriter = 2;
            r.bx = 0;//offset cmdtblp
            return command00();
        }

        public Func<object> command00()
        {
            if (r.al < pw.com_end)
            {
                return out_of_commands();
            }

            r.bx = (byte)~r.al;
            if (pw.ppz != 0)
            {
                r.stack.Push(r.ax);
                _ppz();
                r.ax = r.stack.Pop();
            }

#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("bx:{0} di:{1}", r.bx, r.di));
#endif

            object o = pw.currentCommandTable[r.bx];

#if DEBUG
            if (o == null)
                Log.WriteLine(LogLevel.ERROR, string.Format("bx:{0} di:{1}", r.bx, r.di));
#endif
            return (Func<object>)o;
        }

        private Func<object> out_of_commands()
        { 
            r.si--;
            pw.md[r.si].dat = 0x80; //;Part END
            return null;
        }



        //1745-2033
        private Func<object>[] cmdtbl;
        private void SetupCmdtbl()
        {
            cmdtbl = new Func<object>[] {
            comAt                     // 0xff(0)
            ,comq                     // 0xfe(1)
            ,comv                     // 0xfd(2)
            ,comt                     // 0xfc(3)
            ,comtie                   // 0xfb(4)
            ,comd                     // 0xfa(5)
            ,comstloop                // 0xf9(6)
            ,comedloop                // 0xf8(7)
            ,comexloop                // 0xf7(8)
            ,comlopset                // 0xf6(9)
            ,comshift                 // 0xf5(10)
            ,comvolup                 // 0xf4(11)
            ,comvoldown               // 0xf3(12)
            ,lfoset                   // 0xf2(13)
            ,lfoswitch_f              // 0xf1(14)
            ,jump4                    // 0xf0(15)
            ,comy                     // 0xef(16)
            ,jump1                    // 0xee(17)
            ,jump1                    // 0xed(18)
            //FOR SB2                 
            ,panset                   // 0xec(19)
            ,rhykey                   // 0xeb(20)
            ,rhyvs                    // 0xea(21)
            ,rpnset                   // 0xe9(22)
            ,rmsvs                    // 0xe8(23)
            //追加 for V2.0         
            ,comshift2                // 0xe7(24)
            ,rmsvs_sft                // 0xe6(25)
            ,rhyvs_sft                // 0xe5(26)
            //                      
            ,hlfo_delay               // 0xe4(27)
            //追加 for V2.3         
            ,comvolup2                // 0xe3(28)
            ,comvoldown2              // 0xe2(29)
            //追加 for V2.4         
            ,hlfo_set                 // 0xe1(30)
            ,hlfo_onoff               // 0xe0(31)
            //                      
            ,syousetu_lng_set         // 0xdf(32)
            //                      
            ,vol_one_up_fm            // 0xde(33)
            ,vol_one_down             // 0xdd(34)
            //                      
            ,status_write             // 0xdc(35)
            ,status_add               // 0xdb(36)
            //                      
            ,porta                    // 0xda(37)
            //                      
            ,jump1                    // 0xd9(38)
            ,jump1                    // 0xd8(39)
            ,jump1                    // 0xd7(40)
            //                      
            ,mdepth_set               // 0xd6(41)
            //                      
            ,comdd                    // 0xd5(42)
            //                      
            ,ssg_efct_set             // 0xd4(43)
            ,fm_efct_set              // 0xd3(44)
            ,fade_set                 // 0xd2(45)
            //                      
            ,jump1                    // 0xd1(46)
            //                      
            ,jump1                    // 0xd0(47)
            //                    
            ,slotmask_set             // 0xcf(48) 
            ,jump6                    // 0xce(49) 
            ,jump5                    // 0xcd(50) 
            ,jump1                    // 0xcc(51) 
            ,lfowave_set              // 0xcb(52) 
            ,lfo_extend               // 0xca(53) 
            ,jump1                    // 0xc9(54) 
            ,slotdetune_set           // 0xc8(55) 
            ,slotdetune_set2          // 0xc7(56) 
            ,fm3_extpartset           // 0xc6(57) 
            ,volmask_set              // 0xc5(58) 
            ,comq2                    // 0xc4(59) 
            ,panset_ex                // 0xc3(60) 
            ,lfoset_delay             // 0xc2(61) 
            ,jump0                    // 0xc1(62) ,sular
            ,fm_mml_part_mask         // 0xc0(63) 
            ,_lfoset     		      // 0xbf(64)
            ,_lfoswitch_f        	  // 0xbe(65)
            ,_mdepth_set     	      // 0xbd(66)
            ,_lfowave_set        	  // 0xbc(67)
            ,_lfo_extend     	      // 0xbb(68)
            ,_volmask_set       	  // 0xba(69)
            ,_lfoset_delay      	  // 0xb9(70)
            ,tl_set     		      // 0xb8(71)
            ,mdepth_count        	  // 0xb7(72)
            ,fb_set		              // 0xb6(73)
            ,slot_delay      	      // 0xb5(74)
            ,jump16		              // 0xb4(75)
            ,comq3		              // 0xb3(76)
            ,comshift_master	      // 0xb2(77)
            ,comq4      		      // 0xb1(78)
            };
        }

        //com_end equ	0b1h

        private Func<object>[] cmdtblp;
        private void SetupCmdtblp()
        {
            cmdtblp = new Func<object>[] {
            jump1                       //(0xff)0
            ,comq                       //(0xfe)1
            ,comv                       //(0xfd)2
            ,comt                       //(0xfc)3
            ,comtie                     //(0xfb)4
            ,comd                       //(0xfa)5
            ,comstloop                  //(0xf9)6
            ,comedloop                  //(0xf8)7
            ,comexloop                  //(0xf7)8
            ,comlopset                  //(0xf6)9
            ,comshift                   //(0xf5)10
            ,comvolupp                  //(0xf4)11
            ,comvoldownp                //(0xf3)12
            ,lfoset                     //(0xf2)13
            ,lfoswitch                  //(0xf1)14
            ,psgenvset                  //(0xf0)15
            ,comy                       //(0xef)16
            ,psgnoise                   //(0xee)17
            ,psgsel                     //(0xed)18
            ////                        
            ,jump1                      //(0xec)19
            ,rhykey                     //(0xeb)20
            ,rhyvs                      //(0xea)21
            ,rpnset                     //(0xe9)22
            ,rmsvs                      //(0xe8)23
            ////                        
            ,comshift2                  //(0xe7)24
            ,rmsvs_sft                  //(0xe6)25
            ,rhyvs_sft                  //(0xe5)26
            ////
            ,jump1                      //(0xe4)27
            ////追加 for V2.3
            ,comvolupp2                 //0E3H 28
            ,comvoldownp2	            //0E2H 29
            ////
            ,jump1                      //0E1H 30
            ,jump1		                //0E0H 31
            ////
            ,syousetu_lng_set           //0DFH 32
            ////
            ,vol_one_up_psg             //0DEH 33 
            ,vol_one_down        	    //0DDH 34
            ////
            ,status_write               //0DCH 35
            ,status_add                 //0DBH 36
            ////
            ,portap                     //0DAH 37
            ////
            ,jump1                      //0D9H 38
            ,jump1		                //0D8H 39
            ,jump1        		        //0D7H 40
            ////
            ,mdepth_set                 //0D6H 41
            ////
            ,comdd                      //0d5h 42
            ////
            ,ssg_efct_set               //0d4h 43
            ,fm_efct_set	            //0d3h 44
            ,fade_set       	        //0d2h 45
            ////
            ,jump1                      //(0xd1)46
            ,psgnoise_move              //0d0h 47
            ////
            ,jump1                      //(0xcf) 48
            ,jump6                      //0ceh 49
            ,extend_psgenvset           //0cdh 50
            ,detune_extend	            //0cch 51
            ,lfowave_set    	        //0cbh 52
            ,lfo_extend	                //0cah 53
            ,envelope_extend	        //0c9h 54
            ,jump3		                //0c8h 55
            ,jump3		                //0c7h 56
            ,jump6		                //0c6h 57
            ,jump1		                //0c5h 58
            ,comq2       		        //0c4h 59
            ,jump2		                //0c3h 60
            ,lfoset_delay	            //0c2h 61
            ,jump0       		        //0c1h,sular 62
            ,ssg_mml_part_mask	        //0c0h 63
            ,_lfoset    		        //0bfh 64
            ,_lfoswitch     	        //0beh 65
            ,_mdepth_set     	        //0bdh 66
            ,_lfowave_set       	    //0bch 67
            ,_lfo_extend	            //0bbh 68
            ,jump1       		        //0bah 69
            ,_lfoset_delay	            //0b9h 70
            ,jump2                      //0b8h 71
            ,mdepth_count        	    //0b7h 72
            ,jump1
            ,jump2
            ,jump16		                //0b4h
            ,comq3		                //0b3h
            ,comshift_master    	    //0b2h
            ,comq4       		        //0b1h
            };
        }

        private Func<object>[] cmdtblr;
        private void SetupCmdtblr()
        {
            cmdtblr = new Func<object>[] {
             jump1                      //0xff 0
            ,jump1                      //0xfe 1
            ,comv                       //0xfd 2
            ,comt                       //0xfc 3
            ,comtie                     //0xfb 4
            ,comd                       //0xfa 5
            ,comstloop                  //0xf9 6
            ,comedloop                  //0xf8 7
            ,comexloop                  //0xf7 8
            ,comlopset                  //0xf6 9
            ,jump1                      //0xf5 10
            ,comvolupp                  //0xf4 11
            ,comvoldownp                //0xf3 12
            ,jump4                      //0xf2 13
            ,pdrswitch                  //0xf1 14
            ,jump4                      //0xf0 15
            ,comy                       //0xef 16
            ,jump1                      //0xee 17
            ,jump1                      //0xed 18
            //
            ,jump1                      //0xec 19
            ,rhykey                     //0xeb 20
            ,rhyvs                      //0xea 21
            ,rpnset                     //0xe9 22
            ,rmsvs                      //0xe8 23
            //
            ,jump1                      //0xe7 24
            ,rmsvs_sft                  //0xe6 25
            ,rhyvs_sft                  //0xe5 26
            //
            ,jump1                      //0E4H 27
            //
            ,comvolupp2                 //0E3H 28 
            ,comvoldownp2	            //0E2H 29
            //
            ,jump1                      //0E1H 30
            ,jump1		                //0E0H 31
            //
            ,syousetu_lng_set           //0DFH 32
            //
            ,vol_one_up_psg             //0DEH 33
            ,vol_one_down               //0DDH 34
            //
            ,status_write               //0DCH 35
            ,status_add	                //0DBH 36
            //                          
            ,jump1                      // ポルタメント＝通常音程コマンドに 0xda 37
            //                          
            ,jump1                      //0D9H 38
            ,jump1		                //0D8H 39
            ,jump1		                //0D7H 40
            //                          
            ,jump2                      //0D6H 41
            //                          
            ,comdd                      //0d5h 42
            //
            ,ssg_efct_set               //0d4h 43
            ,fm_efct_set                //0d3h 44
            ,fade_set	                //0d2h 45
            //                  
            ,jump1                      //0xd1 46
            ,jump1                      //0d0h 47
            //                  
            ,jump1                      //0xcf 48
            ,jump6                      //0ceh 49
            ,jump5		                //0cdh 50
            ,jump1		                //0cch 51
            ,jump1                      //0xcb 52
            ,jump1                      //0xca 53
            ,jump1                      //0xc9 54
            ,jump3                      //0xc8 55
            ,jump3                      //0xc7 56
            ,jump6                      //0xc6 57
            ,jump1		                //0c5h 58
            ,jump1                      //0xc4 59
            ,jump2		                //0c3h 60
            ,jump1                      //0xc2 61
            ,jump0		                //0c1h,sular 62
            ,rhythm_mml_part_mask       //0c0h 63
            ,jump4		                //0bfh 64
            ,jump1		                //0beh 65
            ,jump2		                //0bdh 66
            ,jump1		                //0bch 67
            ,jump1		                //0bbh 68
            ,jump1		                //0bah 69
            ,jump1		                //0b9h 70
            ,jump2                      //0xb8 71
            ,jump1                      //0xb7 72
            ,jump1                      //0xb6 73
            ,jump2                      //0xb5 74
            ,jump16		                //0b4h 75
            ,jump1                      //0xb3 76
            ,jump1		                //0b2h 77
            ,jump1		                //0b1h 78
            };
        }



        //2035-2051
        public Func<object> jump16()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump16");
#endif
            
            r.si += 16;
            return null;
        }

        public Func<object> jump6()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump6");
#endif
            
            r.si += 6;
            return null;
        }

        private Func<object> jump5()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump5");
#endif
            
            r.si += 5;
            return null;
        }

        public Func<object> jump4()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump4");
#endif
            
            r.si += 4;
            return null;
        }

        public Func<object> jump3()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump3");
#endif
            
            r.si += 3;
            return null;
        }

        public Func<object> jump2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump2");
#endif
            
            r.si += 2;
            return null;
        }

        public Func<object> jump1()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump1");
#endif
            
            r.si ++;
            return null;
        }

        public Func<object> jump0()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "jump0");
#endif
            
            return null;
        }


        //2052-2064
        //;==============================================================================
        //;	0c0hの追加special命令
        //;==============================================================================
        public Func<object> special_0c0h()
        {
            if (r.al < pw.com_end_0c0h)
            {
                return out_of_commands;
            }

            r.al = (byte)~r.al;
            r.al += r.al;
            r.ah = 0;
            r.bx = r.ax;

            return comtbl0c0h[r.bx / 2];
        }

        private Func<object>[] comtbl0c0h;
        private void SetupComtbl0c0h()
        {
            comtbl0c0h = new Func<object>[] {
                vd_fm // 0ffh
                ,_vd_fm
                ,vd_ssg
                ,_vd_ssg
                ,vd_pcm
                ,_vd_pcm
                ,vd_rhythm
                ,_vd_rhythm	//0f8h
                ,pmd86_s
                ,vd_ppz
                ,_vd_ppz //0f5h
            };
        }



        //2079-2087
        //;==============================================================================
        //;	/s option制御
        //;==============================================================================
        private Func<object> pmd86_s()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 1;
            pw.pcm86_vol = r.al;
            return null;
        }



        //2088-2155
        //;==============================================================================
        //;	各種Voldown
        //;==============================================================================
        private Func<object> vd_fm()
        {
            r.bx = 0;//offset fm_voldown

        //vd_main:;
            r.al = (byte)pw.md[r.si++].dat;
            pw.fm_voldown = r.al;
            return null;
        }

        private Func<object> vd_ssg()
        {
            r.bx = 0;//offset ssg_voldown
            r.al = (byte)pw.md[r.si++].dat;
            pw.ssg_voldown = r.al;
            return null;
        }

        private Func<object> vd_pcm()
        {
            r.bx = 0;//offset pcm_voldown
            r.al = (byte)pw.md[r.si++].dat;
            pw.pcm_voldown = r.al;
            return null;
        }

        private Func<object> vd_rhythm()
        {
            r.bx = 0;//offset rhythm_voldown
            r.al = (byte)pw.md[r.si++].dat;
            pw.rhythm_voldown = r.al;
            return null;
        }

        private Func<object> vd_ppz()
        {
            r.bx = 0;//offset ppz_voldown
            r.al = (byte)pw.md[r.si++].dat;
            pw.ppz_voldown = r.al;
            return null;
        }

        private Func<object> _vd_fm()
        {
            _vd_main(ref pw.fm_voldown, pw._fm_voldown);
            return null;
        }

        private void _vd_main(ref byte a, byte b)
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (r.al == 0)
                goto _vd_reset;
            if ((r.al & 0x80) != 0)
                goto _vd_sign;

            r.carry = a + r.al > 0xff;
            a += r.al;
            if (!r.carry)
                goto _vd_ret;
            a = 255;
        _vd_ret:;
            return;

        _vd_sign:;
            r.carry = a + r.al > 0xff;
            a += r.al;
            if (r.carry)
                goto _vd_ret;
            a = 0;
            return;

        _vd_reset:;
            a = b;
            return;
        }

        private Func<object> _vd_ssg()
        {
            _vd_main(ref pw.ssg_voldown, pw._ssg_voldown);
            return null;
        }
        private Func<object> _vd_pcm()
        {
            _vd_main(ref pw.pcm_voldown, pw._pcm_voldown);
            return null;
        }

        private Func<object> _vd_rhythm()
        {
            _vd_main(ref pw.rhythm_voldown, pw._rhythm_voldown);
            return null;
        }

        private Func<object> _vd_ppz()
        {
            _vd_main(ref pw.ppz_voldown, pw._ppz_voldown);
            return null;
        }



        //2156-2172
        //;==============================================================================
        //;	slot keyon delay
        //;==============================================================================
        private Func<object> slot_delay()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0xf;
            r.al ^= 0xf;
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            pw.partWk[r.di].sdelay_m = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].sdelay = r.al;
            pw.partWk[r.di].sdelay_c = r.al;
            return null;
        }



        //2173-2244
        //;==============================================================================
        //;	FB変化
        //;==============================================================================
        private Func<object> fb_set()
        {
            r.dh = 0xb0 - 1;
            r.dh += pw.partb;//; dh=ALG/FB port address
            r.al = (byte)pw.md[r.si++].dat;
            if ((r.al & 0x80) != 0)
                goto _fb_set;
        fb_set2:;//		;in	al 00000xxx 設定するFB
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
        fb_set3:;//		;in	al 00xxx000 設定するFB
            if (pw.partb != 3)
                goto fb_notfm3;
            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto fb_notfm3;
            }
            else
            {
                if (r.di == pw.part_e)//offset part_e
                    goto fb_notfm3;
            }
            if ((pw.partWk[r.di].slotmask & 0x10) == 0)//;slot1を使用していなければ
                goto fb_ret;//;出力しない
            r.dl = pw.fm3_alg_fb;
            r.dl &= 7;
            r.dl |= r.al;
            pw.fm3_alg_fb = r.dl;
            goto fb_exit;

        fb_notfm3:;
            r.dl = pw.partWk[r.di].alg_fb;
            r.dl &= 0x7;
            r.dl |= r.al;

        fb_exit:;
            opnset();
            pw.partWk[r.di].alg_fb = r.dl;
        fb_ret:;
            return null;

        _fb_set:;
            if ((r.al & 0b0100_0000) != 0)
                goto _fb_sign;
            r.al &= 7;

        _fb_sign:;
            if (pw.partb != 3)
                goto _fb_notfm3;

            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto _fb_notfm3;
            }
            else
            {
                if (r.di == pw.part_e)//offset part_e
                    goto _fb_notfm3;
            }
            r.dl = pw.fm3_alg_fb;
            goto _fb_next;

        _fb_notfm3:;
            r.dl = pw.partWk[r.di].alg_fb;

        _fb_next:;
            r.dl = r.rol(r.dl, 1);
            r.dl = r.rol(r.dl, 1);
            r.dl = r.rol(r.dl, 1);
            r.dl &= 7;
            r.al += r.dl;
            if ((r.al & 0x80) != 0)
                goto _fb_zero;
            if (r.al < 8)
                goto fb_set2;
            r.al = 0b0011_1000;
            goto fb_set3;

        _fb_zero:;
            r.al = 0;
            goto fb_set3;
        }



        //2245-2353
        //;==============================================================================
        //;	TL変化
        //;==============================================================================
        private Func<object> tl_set()
        {
            r.dh = 0x40 - 1;
            r.dh += pw.partb;//; dh=TL FM Port Address
            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;
            r.ah &= 0xf;
            r.ch = pw.partWk[r.di].slotmask;//;ch=slotmask 43210000
            r.ch = r.ror(r.ch, 1);
            r.ch = r.ror(r.ch, 1);
            r.ch = r.ror(r.ch, 1);
            r.ch = r.ror(r.ch, 1);
            r.ah &= r.ch;//; ah=変化させるslot 00004321
            r.dl = (byte)pw.md[r.si].dat;//; dl=変化値
            r.si++;
            r.bx = 0;//offset opnset
            if (pw.partWk[r.di].partmask == 0)//; パートマスクされているか？
                goto ts_00;
            r.bx = 1;//offset dummy_ret
        ts_00:;
            if ((r.al & 0x80) != 0)
                goto tl_slide;
            r.dl &= 127;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto ts_01;
            pw.partWk[r.di].slot1 = r.dl;
            if (r.bx == 0) opnset();
            ts_01:;
            r.dh += 8;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto ts_02;
            pw.partWk[r.di].slot2 = r.dl;
            if (r.bx == 0) opnset();
            ts_02:;
            r.dh -= 4;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto ts_03;
            pw.partWk[r.di].slot3 = r.dl;
            if (r.bx == 0) opnset();
            ts_03:;
            r.dh += 8;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto ts_04;
            pw.partWk[r.di].slot4 = r.dl;
            if (r.bx == 0) opnset();
            //dummy_ret:;
        ts_04:;
            return null;

        //;	相対変化
        tl_slide:;
            r.al = r.dl;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto tls_01;
            r.dl = pw.partWk[r.di].slot1;
            r.dl += r.al;
            if ((r.dl & 0x80) == 0) goto tls_0b;
            r.dl = 0;
            if ((r.al & 0x80) != 0) goto tls_0b;
            r.dl = 127;
        tls_0b:;
            if (r.bx == 0) opnset();
            pw.partWk[r.di].slot1 = r.dl;

        tls_01:;
            r.dh += 8;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto tls_02;
            r.dl = pw.partWk[r.di].slot2;
            r.dl += r.al;
            if ((r.dl & 0x80) == 0) goto tls_1b;
            r.dl = 0;
            if ((r.al & 0x80) != 0) goto tls_1b;
            r.dl = 127;
        tls_1b:;
            if (r.bx == 0) opnset();
            pw.partWk[r.di].slot2 = r.dl;

        tls_02:;
            r.dh -= 4;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto tls_03;
            r.dl = pw.partWk[r.di].slot3;
            r.dl += r.al;
            if ((r.dl & 0x80) == 0) goto tls_2b;
            r.dl = 0;
            if ((r.al & 0x80) != 0) goto tls_2b;
            r.dl = 127;
        tls_2b:;
            if (r.bx == 0) opnset();
            pw.partWk[r.di].slot3 = r.dl;

        tls_03:;
            r.dh += 8;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto tls_04;
            r.dl = pw.partWk[r.di].slot4;
            r.dl += r.al;
            if ((r.dl & 0x80) == 0) goto tls_3b;
            r.dl = 0;
            if ((r.al & 0x80) != 0) goto tls_3b;
            r.dl = 127;
        tls_3b:;
            if (r.bx == 0) opnset();
            pw.partWk[r.di].slot4 = r.dl;

        tls_04:;
            return null;
        }



        //2354-2377
        //;==============================================================================
        //;	演奏中パートのマスクon/off
        //;==============================================================================
        private Func<object> fm_mml_part_mask()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "fm_mml_part_mask");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 2)
                return special_0c0h;

            if (r.al == 0)
                goto fm_mml_part_maskoff;

            pw.partWk[r.di].partmask |= 0x40;
            if (pw.partWk[r.di].partmask != 0x40)
                goto fmpm_ret;

            silence_fmpart();//;音消去

        fmpm_ret:;

            //r.ax = r.stack.Pop();//; commands
            return fmmnp_1;//;パートマスク時の処理に移行

        fm_mml_part_maskoff:;

            pw.partWk[r.di].partmask &= 0xbf;
            if (pw.partWk[r.di].partmask != 0)
                goto fmpm_ret;
            neiro_reset();//;音色再設定
            //r.ax = r.stack.Pop();//; commands
            return mp1;//;パート復活
        }



        //2378-2401
        private Func<object> ssg_mml_part_mask()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "ssg_mml_part_mask");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 2)
                return special_0c0h;

            if (r.al == 0)
                goto ssg_part_maskoff_ret;

            pw.partWk[r.di].partmask |= 0x40;
            if (pw.partWk[r.di].partmask != 0x40)
                goto smpm_ret;

            psgmsk();//;AL=07h AH = Maskdata
            r.dh = 7;
            r.dl = r.al;
            r.dl |= r.ah;
            opnset44();// PSG keyoff

        smpm_ret:;

            //r.ax = r.stack.Pop();//; commandsp
            return psgmnp_1;

        ssg_part_maskoff_ret:;

            pw.partWk[r.di].partmask &= 0xbf;
            if (pw.partWk[r.di].partmask != 0)
                goto smpm_ret;
            //r.ax = r.stack.Pop();//; commandsp
            return mp1p;//;パート復活
        }



        //2402-2413
        private Func<object> rhythm_mml_part_mask()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 2)
                return special_0c0h;

            if (r.al == 0)
                goto rhythm_part_maskoff_ret;

            pw.partWk[r.di].partmask |= 0x40;
            return null;

        rhythm_part_maskoff_ret:;
            pw.partWk[r.di].partmask &= 0xbf;
            return null;
        }



        //2414-2474
        //;==============================================================================
        //;	FM音源の音色を再設定
        //;==============================================================================
        private void neiro_reset()
        {
            if (pw.partWk[r.di].neiromask == 0)
                goto nr_ret;

            r.dl = pw.partWk[r.di].voicenum;
            r.bl = pw.partWk[r.di].slot1;//    mov bx, word ptr slot1[di]; bh=s3 bl = s1
            r.bh = pw.partWk[r.di].slot3;
            r.cl = pw.partWk[r.di].slot2;//    mov cx, word ptr slot2[di]; ch=s4 cl = s2
            r.ch = pw.partWk[r.di].slot4;
            r.stack.Push(r.bx);
            r.stack.Push(r.cx);
            pw.af_check = 1;
            neiroset();//; 音色復帰
            pw.af_check = 0;
            r.cx = r.stack.Pop();
            r.bx = r.stack.Pop();
            pw.partWk[r.di].slot1=r.bl;
            pw.partWk[r.di].slot3=r.bh;
            pw.partWk[r.di].slot2=r.cl;
            pw.partWk[r.di].slot4=r.ch;
            r.al = pw.partWk[r.di].carrier;
            r.al = (byte)~r.al;
            r.al &= pw.partWk[r.di].slotmask;//; al<- TLを再設定していいslot 4321xxxx
            if (r.al == 0) goto nr_exit;
            r.dh = 0x4c - 1;
            r.dh += pw.partb;//;dh=TL FM Port Address
            r.al = r.rol(r.al, 1);
            if (!r.carry) goto nr_s3;
            r.dl = r.ch;//; slot 4
            opnset();

        nr_s3:;
            r.dh -= 8;
            r.al = r.rol(r.al, 1);
            if (!r.carry) goto nr_s2;
            r.dl = r.bh;//; slot 3
            opnset();

        nr_s2:;
            r.dh += 4;
            r.al = r.rol(r.al, 1);
            if (!r.carry) goto nr_s1;
            r.dl = r.cl;//; slot 2
            opnset();

        nr_s1:;
            r.dh -= 8;
            r.al = r.rol(r.al, 1);
            if (!r.carry) goto nr_exit;
            r.dl = r.bl;//; slot 1
            opnset();

        nr_exit:;
            if (pw.board2 != 0)
            {
                r.dh = pw.partb;
                r.dh += 0xb4 - 1;
                calc_panout();
                opnset();//; パン復帰
            }
        nr_ret:;
            return;
        }



        //2475-2489
        //;==============================================================================
        //;	PDRのswitch
        //;==============================================================================
        private Func<object> pdrswitch()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (pw.ppsdrv_flag == 0)
                goto pdrsw_ret;

            r.dl = r.al;
            r.dl &= 1;
            r.al >>= 1;
            r.ah = 5;
            ChipDatum cd = new ChipDatum(0x03, r.al, r.dl);
            ppsdrv(cd);//.SetParam(r.al, r.dl);//int ppsdrv
        pdrsw_ret:;
            return null;
        }



        //2490-2509
        //;==============================================================================
        //;	音量マスクslotの設定
        //;==============================================================================
        private Func<object> volmask_set()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0xf;
            if (r.al == 0)
                goto vms_zero;

            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);//; 上位4BITに移動

            r.al |= 0xf;//;０以外を指定した=下位4BITを１にする
            pw.partWk[r.di].volmask = r.al;
            return ch3_setting;

        vms_zero:;
            r.al = pw.partWk[r.di].carrier;
            pw.partWk[r.di].volmask = r.al;//; キャリア位置を設定

            return ch3_setting;
        }



        //2510-2524
        public Func<object> _volmask_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "_volmask_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0xf;
            if (r.al == 0)
                goto _vms_zero;

            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);//; 上位4BITに移動
            r.al |= 0xf;//;０以外を指定した=下位4BITを１にする
            pw.partWk[r.di]._volmask = r.al;
            return ch3_setting;

        _vms_zero:;
            r.al = pw.partWk[r.di].carrier;
            pw.partWk[r.di]._volmask = r.al;//; キャリア位置を設定

            return ch3_setting;
        }



        //2525-2545
        //;==============================================================================
        //;	パートを判別してch3ならmode設定
        //;==============================================================================
        private Func<object> ch3_setting()
        {
            if (pw.partb != 3)
                goto vms_not_p3;

            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto vms_not_p3;
            }
            else
            {
                if (r.di == pw.part_e)
                    goto vms_not_p3;
            }

            ch3mode_set();//FM3chの場合のみ ch3modeの変更処理

            r.carry = true;
            return null;

        vms_not_p3:
            r.carry = false;
            return null;
        }



        //2546-2593
        //;==============================================================================
        //;	FM3ch 拡張パートセット
        //;==============================================================================
        private Func<object> fm3_extpartset()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "fm3_extpartset");
#endif

            r.stack.Push(r.di);

            r.ax = (ushort)((byte)pw.md[r.si].dat + (byte)pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if (r.ax == 0)
                goto fm3ext_part3c;
            r.ax += (ushort)pw.mmlbuf;
            r.di = (ushort)pw.part3b;//offset part3b
            fm3_partinit();

        fm3ext_part3c:;

            r.ax = (ushort)((byte)pw.md[r.si].dat + (byte)pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if (r.ax == 0)
                goto fm3ext_part3d;
            r.ax += (ushort)pw.mmlbuf;
            r.di = (ushort)pw.part3c;//offset part3c
            fm3_partinit();

        fm3ext_part3d:;

            r.ax = (ushort)((byte)pw.md[r.si].dat + (byte)pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if (r.ax == 0)
                goto fm3ext_exit;

            r.ax += (ushort)pw.mmlbuf;
            r.di = (ushort)pw.part3d;//offset part3d
            fm3_partinit();

        fm3ext_exit:;
            r.di = r.stack.Pop();
            return null;
        }

        private void fm3_partinit()
        {
            pw.partWk[r.di].address = r.ax;
            pw.partWk[r.di].leng = 1;//; ｱﾄ 1ｶｳﾝﾄ ﾃﾞ ｴﾝｿｳ ｶｲｼ
            r.al = 0xff;//-1
            pw.partWk[r.di].keyoff_flag = r.al;//; 現在keyoff中
            pw.partWk[r.di].mdc = r.al;//; MDepth Counter(無限)
            pw.partWk[r.di].mdc2 = r.al;
            pw.partWk[r.di]._mdc = r.al;
            pw.partWk[r.di]._mdc2 = r.al;
            pw.partWk[r.di].onkai = r.al;//rest
            pw.partWk[r.di].onkai_def = r.al;//rest
            pw.partWk[r.di].volume = 108;//; FM VOLUME DEFAULT= 108
            r.bx = (ushort)pw.part3;//offset part3
            r.al = pw.partWk[r.bx].fmpan;
            pw.partWk[r.di].fmpan = r.al;//; FM PAN = CH3と同じ
            pw.partWk[r.di].partmask |= 0x20;//; s0用 partmask
            return;
        }



        //2594-2603
        //;==============================================================================
        //;	Detune Extend Set
        //;==============================================================================
        private Func<object> detune_extend()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "detune_extend");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 1;
            pw.partWk[r.di].extendmode &= 0xfe;
            pw.partWk[r.di].extendmode |= r.al;
            return null;
        }



        //2604-2614
        //;==============================================================================
        //;	LFO Extend Set
        //;==============================================================================
        public Func<object> lfo_extend()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "lfo_extend");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 1;
            r.al <<= 1;

            pw.partWk[r.di].extendmode &= 0xfd;
            pw.partWk[r.di].extendmode |= r.al;
            return null;
        }



        //2615-2626
        //;==============================================================================
        //;	Envelope Extend Set
        //;==============================================================================
        public Func<object> envelope_extend()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "envelope_extend");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 1;
            r.al <<= 1;
            r.al <<= 1;

            pw.partWk[r.di].extendmode &= 0xfb;
            pw.partWk[r.di].extendmode |= r.al;
            return null;
        }



        //2627-2634
        //;==============================================================================
        //;	LFOのWave選択
        //;==============================================================================
        public Func<object> lfowave_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "lfowave_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].lfo_wave = r.al;

            return null;
        }



        //2635-2670
        //;==============================================================================
        //;	PSG Envelope set(Extend)
        //;==============================================================================
        public Func<object> extend_psgenvset()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "extend_psgenvset");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0x1f;
            pw.partWk[r.di].eenv_ar = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0x1f;
            pw.partWk[r.di].eenv_dr = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0x1f;
            pw.partWk[r.di].eenv_sr = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;

            r.al &= 0x0f;
            pw.partWk[r.di].eenv_rr = r.al;

            r.ah = r.rol(r.ah, 1);
            r.ah = r.rol(r.ah, 1);
            r.ah = r.rol(r.ah, 1);
            r.ah = r.rol(r.ah, 1);

            r.ah &= 0xf;
            r.ah ^= 0xf;
            pw.partWk[r.di].eenv_sl = r.ah;

            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 0x0f;
            pw.partWk[r.di].eenv_al = r.al;

            if (pw.partWk[r.di].envf == 0xff)
                goto not_set_count;// ノーマル＞拡張に移行したか？

            pw.partWk[r.di].envf = 0xff;

            pw.partWk[r.di].eenv_count = 4;//; RR
            pw.partWk[r.di].eenv_volume = 0;//;Volume

        not_set_count:;
            return null;
        }



        //2671-2700
        //;==============================================================================
        //;	Slot Detune Set(相対)
        //;==============================================================================
        private Func<object> slotdetune_set2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "slotdetune_set2");
#endif

            if (pw.partb != 3)//;ＦＭ3CH目しか指定出来ない
                return jump3;
            if (pw.board2 != 0)
            {
                if (pw.fmsel == 1)//; 裏では指定出来ない
                    return jump3;
            }

            r.al = (byte)pw.md[r.si++].dat;
            r.bl = r.al;
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;

            r.bl = r.ror(r.bl, 1);
            if (!r.carry) goto sds2_slot2;
            pw.slot_detune1 += r.ax;

        sds2_slot2:;

            r.bl = r.ror(r.bl, 1);
            if (!r.carry) goto sds2_slot3;
            pw.slot_detune2 += r.ax;

        sds2_slot3:;

            r.bl = r.ror(r.bl, 1);
            if (!r.carry) goto sds2_slot4;
            pw.slot_detune3 += r.ax;

        sds2_slot4:;

            r.bl = r.ror(r.bl, 1);
            if (!r.carry) return sds_check;
            pw.slot_detune4 += r.ax;
            return sds_check;
        }



        //2701-2741
        //;==============================================================================
        //;	Slot Detune Set
        //;==============================================================================
        private Func<object> slotdetune_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "slotdetune_set");
#endif

            if (pw.partb != 3)//;ＦＭ3CH目しか指定出来ない
                return jump3;
            if (pw.board2 != 0)
            {
                if (pw.fmsel == 1)//; 裏では指定出来ない
                    return jump3;
            }
            else
            {
                if (r.di == pw.part_e)
                    return jump3;
            }

            r.al = (byte)pw.md[r.si++].dat;
            r.bl = r.al;
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;

            r.carry = ((r.bl & 0x01) != 0);
            r.bl = (byte)(((r.bl >> 1) & 0x7f) | (r.bl << 7));
            if (!r.carry) goto sds_slot2;
            pw.slot_detune1 = r.ax;

        sds_slot2:;
            r.carry = ((r.bl & 0x01) != 0);
            r.bl = (byte)(((r.bl >> 1) & 0x7f) | (r.bl << 7));
            if (!r.carry) goto sds_slot3;
            pw.slot_detune2 = r.ax;

        sds_slot3:;
            r.carry = ((r.bl & 0x01) != 0);
            r.bl = (byte)(((r.bl >> 1) & 0x7f) | (r.bl << 7));
            if (!r.carry) goto sds_slot4;
            pw.slot_detune3 = r.ax;

        sds_slot4:;
            r.carry = ((r.bl & 0x01) != 0);
            r.bl = (byte)(((r.bl >> 1) & 0x7f) | (r.bl << 7));
            if (!r.carry) return sds_check;
            pw.slot_detune4 = r.ax;
            return sds_check;
        }

        private Func<object> sds_check()
        { 
            r.ax = pw.slot_detune1;
            r.ax |= pw.slot_detune2;
            r.ax |= pw.slot_detune3;
            r.ax |= pw.slot_detune4;//; 全部０か？
            if (r.ax == 0) goto sdf_set;
            r.al = 1;
        sdf_set:;
            pw.slotdetune_flag = r.al;
            ch3mode_set();
            return null;
        }



        //2742-2835
        //;==============================================================================
        //;	FM3のmodeを設定する
        //;==============================================================================
        private void ch3mode_set()
        {
            r.al = 1;
            if (r.di == pw.part3)
                goto cmset_00;
            r.al++;
            if (r.di == pw.part3b)
                goto cmset_00;
            r.al = 4;
            if (r.di == pw.part3c)
                goto cmset_00;
            r.al = 8;

        cmset_00:;
            if ((pw.partWk[r.di].slotmask & 0xf0) == 0)            //;s0
                goto cm_clear;
            if (pw.partWk[r.di].slotmask != 0xf0)
                goto cm_set;
            if ((pw.partWk[r.di].volmask & 0x0f) == 0)
                goto cm_clear;
            if ((pw.partWk[r.di].lfoswi & 0x1) != 0)
                goto cm_set;

            //cm_noset1:;
            if ((pw.partWk[r.di]._volmask & 0x0f) == 0)
                goto cm_clear;
            if ((pw.partWk[r.di].lfoswi & 0x10) != 0)
                goto cm_set;

            cm_clear:;
            r.al ^= 0xff;
            pw.slot3_flag &= r.al;
            if (pw.slot3_flag != 0)
                goto cm_set2;

            //cm_clear2:;
            if (pw.slotdetune_flag == 1)
                goto cm_set2;
            r.ah = 0x3f;
            goto cm_set_main;

        cm_set:;
            pw.slot3_flag |= r.al;

        cm_set2:;
            r.ah = 0x7f;

        cm_set_main:;
            if (pw.board2 == 0)
            {
                if ((pw.partWk[r.di].partmask & 2) != 0)//; Effect/パートマスクされているか？
                {
                    cm_nowefcplaying();
                    return;
                }
            }

            if (r.ah == pw.ch3mode)
                goto cm_exit;// ; 以前と変更無しなら何もしない

            pw.ch3mode = r.ah;
            r.dh = 0x27;
            r.dl = r.ah;
            r.dl &= 0b1100_1111;//;Resetはしない
            opnset44();

            //;	効果音モードに移った場合はそれ以前のFM3パートで音程書き換え
            if (r.ah == 0x3f)
                goto cm_exit;
            if (r.di == pw.part3)
                goto cm_exit;

            //cm_otodasi:;

            r.stack.Push(r.bp);
            r.bp = r.di;
            r.stack.Push(r.di);
            r.di = (ushort)pw.part3;//offset part3
            otodasi_cm();

        //cm_3bchk:;
            if (r.bp == pw.part3b)
                goto cm_exit2;
            r.di = (ushort)pw.part3;//offset part3b
            otodasi_cm();

        //cm_3cchk:;
            if (r.bp == pw.part3c)
                goto cm_exit2;
            r.di = (ushort)pw.part3c;//offset part3c
            otodasi_cm();

        cm_exit2:;
            r.di = r.stack.Pop();
            r.bp = r.stack.Pop();

        cm_exit:;
            return;
        }

        private void otodasi_cm()
        {
            if (pw.partWk[r.di].partmask != 0)
                goto ocm_ret;

            otodasi();

        ocm_ret:;
            return;
        }

        private void cm_nowefcplaying()
        { 
            if (pw.board2 == 0)
            {
                pw.ch3mode_push= r.ah;
                return;
            }
        }



        //2836-2953
        //;==============================================================================
        //;	FM slotmask set
        //;==============================================================================
        private Func<object> slotmask_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "slotmask_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;
            r.al &= 0xf;
            if (r.al == 0)
                goto sm_not_car;

            r.al = (byte)((r.al << 4) | ((r.al >> 4) & 0x0f));
            pw.partWk[r.di].carrier = r.al;
            goto sm_set;

        sm_not_car:;
            if (pw.partb != 3)
                goto sm_notfm3;

            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto sm_notfm3;
            }
            else
            {
                if (r.di == pw.part_e)
                    goto sm_notfm3;
            }

            r.bl = pw.fm3_alg_fb;
            goto sm_car_set;

        sm_notfm3:;
            //r.dl = pw.partWk[r.di].voicenum;
            //r.stack.Push(r.ax);
            //toneadr_calc();
            //r.ax = r.stack.Pop();
            //r.bl = (byte)pw.inst[r.bx + 24].dat;
            r.bl = pw.partWk[r.di].alg_fb;

        sm_car_set:;
            r.bh = 0;
            r.bl &= 7;
            r.bx += 0;//offset carrier_table
            r.al = pw.carrier_table[r.bx];
            pw.partWk[r.di].carrier = r.al;

        sm_set:;
            r.ah &= 0xf0;
            if (pw.partWk[r.di].slotmask == r.ah)
                goto sm_no_change;
            pw.partWk[r.di].slotmask = r.ah;
            if ((r.ah & 0xf0) != 0)
                goto sm_noset_pm;
            pw.partWk[r.di].partmask |= 0x20;//;s0の時パートマスク
            goto sms_ns;

        sm_noset_pm:;
            pw.partWk[r.di].partmask &= 0xdf;//;s0以外の時パートマスク解除

        sms_ns:;
            ch3_setting();//; FM3chの場合のみ ch3modeの変更処理
            if (!r.carry) goto sms_nms;
            //; ch3なら、それ以前のFM3パートでkeyon処理
            if (r.di == pw.part3)
                goto sm_exit;

            r.stack.Push(r.bp);
            r.bp = r.di;
            r.stack.Push(r.di);
            r.di = (ushort)pw.part3;
            keyon_sm();

        //sm_3bchk:;
            if (r.bp == pw.part3b)
                goto sm_exit2;
            r.di = (ushort)pw.part3b;
            keyon_sm();

        //sm_3cchk:;
            if (r.bp == pw.part3c)
                goto sm_exit2;
            r.di = (ushort)pw.part3c;
            keyon_sm();

        sm_exit2:;
            r.di = r.stack.Pop();
            r.bp = r.stack.Pop();

        sm_exit:;
        sms_nms:;
            r.ah = 0;
            r.al = pw.partWk[r.di].slotmask;
            r.al = r.rol(r.al, 1);//; slot4
            if (!r.carry) goto sms_n4;
            r.ah |= 0b0001_0001;

        sms_n4:;
            r.al = r.rol(r.al, 1);//; slot3
            if (!r.carry) goto sms_n3;
            r.ah |= 0b0100_0100;

        sms_n3:;
            r.al = r.rol(r.al, 1);//; slot2
            if (!r.carry) goto sms_n2;
            r.ah |= 0b0010_0010;

        sms_n2:;
            r.al = r.rol(r.al, 1);//; slot1
            if (!r.carry) goto sms_n1;
            r.ah |= 0b1000_1000;

        sms_n1:;
            pw.partWk[r.di].neiromask = r.ah;
            //r.bx = r.stack.Pop();//; commands
            if (pw.partWk[r.di].partmask == 0)
                return mp1;//; パート復活
            return fmmnp_1;

        sm_no_change:;
            return null;
        }

        private void keyon_sm()
        {
            if (pw.partWk[r.di].partmask != 0)
                goto ksm_ret;
            if ((pw.partWk[r.di].keyoff_flag & 1) != 0)//; keyon中か？
                goto ksm_ret;// keyoff中
            keyon();
        ksm_ret:;
            return;
        }



        //2954-2977
        //;==============================================================================
        //;	ssg effect
        //;==============================================================================
        public Func<object> ssg_efct_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "ssg_efct_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (pw.partWk[r.di].partmask != 0)
                return null;

            if (r.al == 0)
                goto ses_off;

            r.stack.Push(r.si);
            r.stack.Push(r.di);
            efcdrv.eff_on2();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();

        //ses_ret:;
            return null;

        ses_off:;
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            efcdrv.effoff();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            return null;
        }



        //2978-3025
        //;==============================================================================
        //;	fm effect
        //;==============================================================================
        public Func<object> fm_efct_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "fm_efct_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (pw.partWk[r.di].partmask != 0)
                return null;//ses_ret;↑

            if (r.al == 0)
                goto fes_off;
            if (pw.board2 != 0)
            {
                r.bh = pw.fmsel;
            }
            r.bl = pw.partb;
            r.stack.Push(r.bx);
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            fm_effect_on();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            r.ax = r.stack.Pop();
            pw.partb = r.al;
            if (pw.board2 != 0)
            {
                if (r.ah == 0)
                {
                    sel44();
                    return null;
                }
                sel46();
                return null;
            }
            else
            {
                return null;
            }

        fes_off:;
            if (pw.board2 != 0)
            {
                r.bh = pw.fmsel;
            }
            r.bl = pw.partb;
            r.stack.Push(r.bx);
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            fm_effect_off();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            r.ax = r.stack.Pop();
            pw.partb = r.al;
            if (pw.board2 != 0)
            {
                if (r.ah == 0)
                {
                    sel44();
                    return null;
                }
                sel46();
                return null;
            }
            else
            {
                return null;
            }
        }



        //3026-3033
        //;==============================================================================
        //;	fadeout
        //;==============================================================================
        public Func<object> fade_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "fade_set");
#endif

            pw.fadeout_flag = 1;
            r.al = (byte)pw.md[r.si++].dat;
            //KUMA:fout の処理をここでやってしまう
            pw.fadeout_speed = r.al;
            return null;
        }



        //3034-3044
        //;==============================================================================
        //;	LFO depth +- set
        //;==============================================================================
        public Func<object> mdepth_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "mdepth_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].mdspd = r.al;
            pw.partWk[r.di].mdspd2 = r.al;
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].mdepth = r.al;

            return null;
        }



        //3045-3063
        public Func<object> mdepth_count()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "mdepth_count");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al |= r.al;
            if ((r.al & 0x80) != 0)
                goto mdc_lfo2;
            if (r.al != 0)
                goto mdc_no_deca;
            r.al--;//;255

        mdc_no_deca:;
            pw.partWk[r.di].mdc = r.al;
            pw.partWk[r.di].mdc2 = r.al;

            return null;

        mdc_lfo2:;
            r.al &= 0x7f;
            if (r.al != 0)
                goto mdc_no_decb;

            r.al--;//;255

        mdc_no_decb:;
            pw.partWk[r.di]._mdc = r.al;
            pw.partWk[r.di]._mdc2 = r.al;

            return null;
        }



        //3064-3081
        //;==============================================================================
        //;	ポルタメント計算なのね
        //;==============================================================================
        public void porta_calc()
        {
            r.ax = pw.partWk[r.di].porta_num2;
            pw.partWk[r.di].porta_num += r.ax;
            if (pw.partWk[r.di].porta_num3 == 0)
                goto pc_ret;
            if ((pw.partWk[r.di].porta_num3&0x8000) != 0)
                goto pc_minus;

            pw.partWk[r.di].porta_num3--;
            pw.partWk[r.di].porta_num++;
            return;

        pc_minus:;
            pw.partWk[r.di].porta_num3++;
            pw.partWk[r.di].porta_num--;

        pc_ret:;
            return;
        }



        //3082-3151
        //;==============================================================================
        //;	ポルタメント(FM)
        //;==============================================================================
        private Func<object> porta()
        {
            if (pw.partWk[r.di].partmask != 0)
                goto porta_notset;

            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = pw.cmd;
            WriteOPNARegister(cd);

            r.al = (byte)pw.md[r.si++].dat;
            lfoinit();
            oshift();
            fnumset();
            r.ax = pw.partWk[r.di].fnum;
            r.stack.Push(r.ax);
            r.al = pw.partWk[r.di].onkai;
            r.stack.Push(r.ax);
            r.al = (byte)pw.md[r.si++].dat;
            oshift();
            fnumset();
            r.bx = pw.partWk[r.di].fnum;//; bx=ポルタメント先のfnum値
            r.cx = r.stack.Pop();
            pw.partWk[r.di].onkai = r.cl;
            r.cx = r.stack.Pop();
            pw.partWk[r.di].fnum = r.cx;//;cx=ポルタメント元のfnum値
            r.ax = 0;
            r.stack.Push(r.cx);
            r.stack.Push(r.bx);
            r.ch &= 0x38;
            r.bh &= 0x38;
            r.bh -= r.ch;//;先のoctarb - 元のoctarb
            if (r.bh == 0) goto not_octarb;
            r.bh = (byte)((r.bh & 0x80) | ((r.bh >> 1) & 0x7f));
            r.bh = (byte)((r.bh & 0x80) | ((r.bh >> 1) & 0x7f));
            r.bh = (byte)((r.bh & 0x80) | ((r.bh >> 1) & 0x7f));
            r.al = r.bh;
            r.ax = (ushort)(sbyte)r.al;//;ax=octarb差
            r.bx = 0x26a;
            int ans = r.ax * r.bx;//;(dx) ax = 26ah* octarb差
            r.dx = (ushort)(ans >> 16);
            r.ax = (ushort)ans;
        
        not_octarb:;
            r.bx = r.stack.Pop();
            r.cx = r.stack.Pop();
            r.cx &= 0x7ff;
            r.bx &= 0x7ff;
            r.bx -= r.cx;
            r.ax += r.bx;//;ax=26ah* octarb差 + 音程差
            r.bl = (byte)pw.md[r.si].dat;
            r.si++;
            pw.partWk[r.di].leng = r.bl;
            calc_q();
            r.bh = 0;
            int src = (short)r.ax;
            r.dx = (ushort)(src % (short)r.bx);//;ax=(26ah* ovtarb差 + 音程差) / 音長
            r.ax = (ushort)(src / (short)r.bx);
            pw.partWk[r.di].porta_num2 = r.ax;//;商
            pw.partWk[r.di].porta_num3 = r.dx;//;余り
            pw.partWk[r.di].lfoswi |= 8;//;Porta ON
            //r.ax = r.stack.Pop();//; commands
            return porta_return;

        porta_notset:;
            r.al = (byte)pw.md[r.si++].dat;//;最初の音程を読み飛ばす(Mask時)
            return null;
        }



        //3152-3196
        //;==============================================================================
        //;	ポルタメント(PSG)
        //;==============================================================================
        private Func<object> portap()
        {
            if (pw.partWk[r.di].partmask != 0)
            {
                r.al = (byte)pw.md[r.si++].dat;
                return null;
                //return porta_notset;
            }

            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = pw.cmd;
            WriteOPNARegister(cd);

            r.al = (byte)pw.md[r.si++].dat;
            lfoinitp();
            oshiftp();
            fnumsetp();
            r.ax = pw.partWk[r.di].fnum;
            r.stack.Push(r.ax);
            r.al = pw.partWk[r.di].onkai;
            r.stack.Push(r.ax);
            r.al = (byte)pw.md[r.si++].dat;
            oshiftp();
            fnumsetp();
            r.ax = pw.partWk[r.di].fnum;//; ax = ポルタメント先のpsg_tune値
            r.bx = r.stack.Pop();
            pw.partWk[r.di].onkai = r.bl;
            r.bx = r.stack.Pop();//; bx = ポルタメント元のpsg_tune値
            pw.partWk[r.di].fnum = r.bx;
            r.ax -= r.bx;//; ax = psg_tune差
            r.bl = (byte)pw.md[r.si].dat;
            r.si++;
            pw.partWk[r.di].leng = r.bl;
            calc_q();
            r.bh = 0;
            int src = (short)r.ax;
            r.dx = (ushort)(src % (short)r.bx);//; ax = psg_tune差 / 音長
            r.ax = (ushort)(src / (short)r.bx);
            pw.partWk[r.di].porta_num2 = r.ax;//;商
            pw.partWk[r.di].porta_num3 = r.dx;//;余り
            pw.partWk[r.di].lfoswi |= 8;//;Porta ON
            //r.ax = r.stack.Pop();//; commandsp
            return porta_returnp;
        }



        //3197-3204
        //;==============================================================================
        //;	ＳＴＡＴＵＳに値を出力
        //;==============================================================================
        public Func<object> status_write()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "status_write");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.status = r.al;
            return null;
        }



        //3205-3214
        //;==============================================================================
        //;	ＳＴＡＴＵＳに値を加算
        //;==============================================================================
        public Func<object> status_add()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "status_add");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.bx = 0;//offset status
            r.al += pw.status;//add al,[bx]
            pw.status = r.al;//mov[bx],al
            return null;
        }



        //3215-3256
        //;==============================================================================
        //;	ボリュームを次の一個だけ変更（Ｖ２．７拡張分）
        //;==============================================================================
        private Func<object> vol_one_up_fm()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "vol_one_up_fm");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al += pw.partWk[r.di].volume;
            if (r.al < 128)
                return vo_vset;
            r.al = 127;
            return vo_vset;
        }

        private Func<object> vo_vset()
        {
            r.al++;
            pw.partWk[r.di].volpush = r.al;
            pw.volpush_flag = 1;
            return null;
        }

        private Func<object> vol_one_up_psg()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "vol_one_up_psg");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al += pw.partWk[r.di].volume;
            if (r.al < 16)
                return vo_vset;
            r.al = 15;
            return vo_vset;
        }

        public Func<object> vol_one_up_pcm()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "vol_one_up_pcm");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.carry = (r.al + pw.partWk[r.di].volume > 0xff);
            r.al += pw.partWk[r.di].volume;
            if (r.carry) return voup_over;
            return vmax_check;
        }

        private Func<object> vmax_check()
        {
            if (r.al < 255)
                return vo_vset;
            return voup_over;
        }

        private Func<object> voup_over()
        { 
            r.al = 254;
            return vo_vset;
        }

        public Func<object> vol_one_down()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "vol_one_down");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;
            r.al = pw.partWk[r.di].volume;
            r.carry=r.al < r.ah;
            r.al -= r.ah;
            if (!r.carry) return vmax_check;
            r.al = 0;
            return vo_vset;
        }



        //3257-3300
        //;==============================================================================
        //;	ＦＭ音源ハードＬＦＯの設定（Ｖ２．４拡張分）
        //;==============================================================================
        private Func<object> hlfo_set()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (pw.board2 != 0)
            {
                r.ah = r.al;
                r.al = pw.partWk[r.di].fmpan;
                r.al &= 0b1100_0000;
                r.al |= r.ah;
                pw.partWk[r.di].fmpan = r.al;
                if (pw.partb != 3)
                    goto hlfoset_notfm3;
                if (pw.fmsel != 0)
                    goto hlfoset_notfm3;
                //;2608の時のみなので part_eはありえない
                //; FM3の場合は 4つのパート総て設定
                r.stack.Push(r.di);
                r.di = (ushort)pw.part3;//offset part3
                pw.partWk[r.di].fmpan = r.al;
                r.di = (ushort)pw.part3b;//offset part3b
                pw.partWk[r.di].fmpan = r.al;
                r.di = (ushort)pw.part3c;//offset part3c
                pw.partWk[r.di].fmpan = r.al;
                r.di = (ushort)pw.part3d;//offset part3d
                pw.partWk[r.di].fmpan = r.al;
                r.di = r.stack.Pop();

            hlfoset_notfm3:;
                if (pw.partWk[r.di].partmask != 0)//; パートマスクされているか？
                    goto hlfo_exit;
                r.dh = pw.partb;
                r.dh += 0xb4 - 1;
                calc_panout();
                opnset();
            }
        hlfo_exit:;
            return null;
        }



        //3301-3314
        //;==============================================================================
        //;	ＦＭ音源ハードＬＦＯのスイッチ（Ｖ２．４拡張分）
        //;==============================================================================
        private Func<object> hlfo_onoff()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (pw.board2 != 0)
            {
                r.dl = r.al;
                r.dh = 0x22;
                pw.port22h = r.dl;
                opnset44();
                return null;
            }
            else
            {
                return null;
            }
        }


        //3315-3324
        //;==============================================================================
        //;	ＦＭ音源ハードＬＦＯのディレイ設定
        //;==============================================================================
        private Func<object> hlfo_delay()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (pw.board2 != 0)
            {
                pw.partWk[r.di].hldelay = r.al;
            }
            return null;
        }



        //3325-3332
        //;==============================================================================
        //;	COMMAND 'Z' （小節の長さの変更）
        //;==============================================================================
        public Func<object> syousetu_lng_set()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "syousetu_lng_set");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.syousetu_lng = r.al;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Tempo, null, (int)pw.tempo_d, (int)pw.syousetu_lng);
            cd.addtionalData = md;
            WriteOPNARegister(cd);

            return null;
        }



        //3333-3377
        //;==============================================================================
        //;	COMMAND '@' [PROGRAM CHANGE]
        //;==============================================================================
        private Func<object> comAt()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "com@");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].voicenum = r.al;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = new MmlDatum(-1, enmMMLType.Instrument, pw.cmd.linePos
                , (int)0xff
                , (int)pw.partWk[r.di].voicenum
                );
            WriteDummy(cd);

            r.dl = r.al;
            if (pw.partWk[r.di].partmask != 0)//;パートマスクされているか？
                goto comAt_mask;

            neiroset();
            return null;

        comAt_mask:;
            toneadr_calc();

            r.dl = (byte)pw.inst[r.bx + 24].dat;//    mov dl,24[bx]
            pw.partWk[r.di].alg_fb = r.dl;//; alg/fb設定
            r.bx += 4;

            neiroset_tl();//; tl設定(NO break dl)

            //; FM3chで、マスクされていた場合、fm3_alg_fbを設定
            if (pw.partb != 3)
                goto comAt_exit;

            if (pw.partWk[r.di].neiromask == 0)
                goto comAt_exit;

            if (pw.board2 != 0)
            {
                if (pw.fmsel == 0)
                    goto comAt_afset;
            }
            else
            {
                if (r.di != pw.part_e)
                    goto comAt_afset;
            }

        comAt_exit:;
            return null;

        comAt_afset:;//	;in. dl = alg/fb
            if ((pw.partWk[r.di].slotmask & 0x10) != 0)//;slot1を使用していなければ
                goto comAt_notslot1;

            r.al = pw.fm3_alg_fb;
            r.al &= 0b0011_1000;//;fbは前の値を使用
            r.dl &= 0b0000_0111;
            r.dl |= r.al;

        comAt_notslot1:;
            pw.fm3_alg_fb = r.dl;
            pw.partWk[r.di].alg_fb = r.al;
            return null;
        }



        //3378-3394
        //;==============================================================================
        //;	COMMAND 'q' [STEP-GATE CHANGE]
        //;==============================================================================
        public Func<object> comq()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comq");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].qdata = r.al;
            pw.partWk[r.di].qdat3 = 0;
            comq_dmy();

            return null;
        }

        public Func<object> comq3()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comq3");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].qdat2 = r.al;
            comq_dmy();

            return null;
        }

        public Func<object> comq4()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comq4");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].qdat3 = r.al;
            comq_dmy();

            return null;
        }



        //3395-3402
        //;==============================================================================
        //;	COMMAND 'Q' [STEP-GATE CHANGE 2]
        //;==============================================================================
        public Func<object> comq2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comq2");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].qdatb = r.al;
            comq_dmy();

            return null;
        }

        private void comq_dmy()
        {
            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Gatetime, pw.cmd.linePos
                , (int)pw.partWk[r.di].qdatb // Q%
                , (int)pw.partWk[r.di].qdata // q [X] -x  ,  x    :数値1
                , (int)pw.partWk[r.di].qdat2 // q  x  -x  , [X]   :数値3
                , (int)pw.partWk[r.di].qdat3 // q  x [-X] ,  x    :数値2
                );
            cd.addtionalData = md;
            WriteDummy(cd);
        }


        //3403-3410
        //;==============================================================================
        //;	COMMAND 'V' [VOLUME CHANGE]
        //;==============================================================================
        public Func<object> comv()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comv");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].volume = r.al;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, (int)r.al);
            cd.addtionalData = md;
            WriteDummy(cd);

            return null;
        }



        //3411-3474
        //;==============================================================================
        //;	COMMAND 't' [TEMPO CHANGE1]
        //;	COMMAND 'T' [TEMPO CHANGE2]
        //;	COMMAND 't±' [TEMPO CHANGE 相対1]
        //;	COMMAND 'T±' [TEMPO CHANGE 相対2]
        //;==============================================================================
        public Func<object> comt()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comt");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 251)
                goto comt_sp0;

        comt_exit1:;
            pw.tempo_d = r.al;//;T(FC)
            pw.tempo_d_push = r.al;
            return calc_tb_tempo;

        comt_sp0:;
            r.al++;
            if (r.al != 0)
                goto comt_sp1;

            r.al = (byte)pw.md[r.si++].dat; //;t(FC FF)

        comt_exit2c:;
            if (r.al >= 18)
                goto comt_exit2;
        comt_2c_over:;
            r.al = 18;
        comt_exit2:;
            pw.tempo_48 = r.al;
            pw.tempo_48_push = r.al;
            return calc_tempo_tb;

        comt_sp1:;
            r.al++;
            bool zero = r.al == 0;
            r.al = (byte)pw.md[r.si++].dat;
            if (!zero) goto comt_sp2;

            r.ah = pw.tempo_d_push;//;T± (FC FE)
            if ((r.al & 0x80) != 0)
                goto comt_sp1_minus;
            r.carry = (r.al + r.ah) > 0xff;
            r.al += r.ah;
            if (!r.carry) goto comt_sp1_exitc;
            r.al = 250;
            goto comt_exit1;
        comt_sp1_minus:;
            r.carry = (r.al + r.ah) > 0xff;
            r.al += r.ah;
            if (r.carry) goto comt_sp1_exitc;

            r.al = 0;
        comt_sp1_exitc:;
            if (r.al < 251)
                goto comt_exit1;

            r.al = 250;
            goto comt_exit1;

        comt_sp2:;
            r.ah = pw.tempo_48_push;//; t± (FC FD)
            if ((r.al & 0x80) != 0)
                goto comt_sp2_minus;
            r.carry = (r.al + r.ah) > 0xff;
            r.al += r.ah;
            if (!r.carry) goto comt_exit2;
            r.al = 255;
            goto comt_exit2;
        comt_sp2_minus:;
            r.carry = (r.al + r.ah) > 0xff;
            r.al += r.ah;
            if (!r.carry) goto comt_2c_over;

            goto comt_exit2c;
        }



        //3475-3496
        //;==============================================================================
        //;	T->t 変換
        //; input[tempo_d]
        //;		output[tempo_48]
        //;==============================================================================
        private Func<object> calc_tb_tempo()
        {
            //;	TEMPO = 112CH / [ 256 - TB] timerB -> tempo
            r.bl = 0;
            r.bl -= pw.tempo_d;//tempo_d レジスタ地
            r.al = 255;

            if (r.bl < 18) goto ctbt_exit;

            r.ax = 0x112c;
            r.ah = (byte)(0x112c % r.bl);
            r.al = (byte)(0x112c / r.bl);
            if ((r.ah & 0x80) == 0) goto ctbt_exit;
            r.al++;//;四捨五入
        ctbt_exit:;

            pw.tempo_48 = r.al;
            pw.tempo_48_push = r.al;

            return null;
        }



        //3497-3520
        //;==============================================================================
        //;	t->T 変換
        //; input[tempo_48]
        //;		output[tempo_d]
        //;==============================================================================
        private Func<object> calc_tempo_tb()
        {
            //;	TB = 256 - [ 112CH / TEMPO] tempo -> timerB
            r.bl = pw.tempo_48;
            r.al = 0;

            if (r.bl < 18)
                goto cttb_exit;

            r.ax = 0x112c;

            r.al = (byte)(0x112c / r.bl);
            r.ah = (byte)(0x112c % r.bl);

            r.dl = 0;
            r.dl -= r.al;
            r.al = r.dl;
            if ((r.ah & 0x80) == 0)
                goto cttb_exit;
            r.al--;//;四捨五入

        cttb_exit:;
            pw.tempo_d = r.al;
            pw.tempo_d_push = r.al;

            return null;
        }



        //3521-3527
        //;==============================================================================
        //;	COMMAND '&' [タイ]
        //;==============================================================================
        public Func<object> comtie()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comtie");
#endif
            
            pw.tieflag |= 1;
            return null;
        }



        //3528-3534
        //;==============================================================================
        //;	COMMAND 'D' [ﾃﾞﾁｭｰﾝ]
        //;==============================================================================
        public Func<object> comd()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comd");
#endif

            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            pw.partWk[r.di].detune = r.ax;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = new MmlDatum(-1, enmMMLType.Detune, pw.cmd.linePos
                , (int)(short)pw.partWk[r.di].detune
                );
            WriteDummy(cd);

            return null;
        }



        //3535-3541
        //;==============================================================================
        //;	COMMAND 'DD' [相対ﾃﾞﾁｭｰﾝ]
        //;==============================================================================
        public Func<object> comdd()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comdd");
#endif
            
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            pw.partWk[r.di].detune += r.ax;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = new MmlDatum(-1, enmMMLType.Detune, pw.cmd.linePos
                , (int)(short)pw.partWk[r.di].detune
                );
            WriteDummy(cd);

            return null;
        }



        //3542-3557
        //;==============================================================================
        //;	COMMAND '[' [ﾙｰﾌﾟ ｽﾀｰﾄ]
        //;==============================================================================
        public Func<object> comstloop()
        {
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            r.bx = r.ax;
            r.ax = (ushort)pw.mmlbuf;
            if (r.di != pw.part_e)
                goto comst_nonefc;
            r.ax = (ushort)pw.efcdat;

        comst_nonefc:;
            r.bx += r.ax;
            r.bx++;
            pw.md[r.bx].dat = 0;

            return null;
        }



        //3558-3586
        //;==============================================================================
        //;	COMMAND	']' [ﾙｰﾌﾟ ｴﾝﾄﾞ]
        //;==============================================================================
        public Func<object> comedloop()
        {
            r.al = (byte)(pw.md[r.si++].dat);
            if (r.al == 0)
                goto muloop;//; 0 ﾅﾗ ﾑｼﾞｮｳｹﾝ ﾙｰﾌﾟ
            r.ah = r.al;
            pw.md[r.si].dat++;

            r.al = (byte)(pw.md[r.si++].dat);
            if (r.ah != r.al)
                goto reloop;
            r.si += 2;
            return null;

        muloop:;
            r.si++;
            pw.partWk[r.di].loopcheck = 1;

        reloop:;
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            r.ax += 2;

            r.bx = (ushort)pw.mmlbuf;
            if (r.di != pw.part_e)
                goto comed_nonefc;
            r.bx = (ushort)pw.efcdat;
        comed_nonefc:;
            r.ax += r.bx;

            r.si = r.ax;

            return null;
        }



        //3587-3609
        //;==============================================================================
        //;	COMMAND	':' [ﾙｰﾌﾟ ﾀﾞｯｼｭﾂ]
        //;==============================================================================
        public Func<object> comexloop()
        {
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            r.bx = r.ax;

            r.ax = (ushort)pw.mmlbuf;
            if (r.di != pw.part_e)
                goto comex_nonefc;
            r.ax = (ushort)pw.efcdat;
        comex_nonefc:;
            r.bx += r.ax;

            r.dl = (byte)pw.md[r.bx].dat;
            r.dl--;
            r.bx++;
            if (r.dl == pw.md[r.bx].dat)
                goto loopexit;

            return null;

        loopexit:;
            r.bx += 3;
            r.si = r.bx;
            return null;
        }



        //3610-3616
        //;==============================================================================
        //;	COMMAND 'L' [ｸﾘｶｴｼ ﾙｰﾌﾟ ｾｯﾄ]
        //;==============================================================================
        public Func<object> comlopset()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comlopset");
#endif

            pw.partWk[r.di].partloop = r.si;

            return null;
        }



        //3617-3624
        //;==============================================================================
        //;	COMMAND '_' [ｵﾝｶｲ ｼﾌﾄ]
        //;==============================================================================
        public Func<object> comshift()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comshift");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].shift = r.al;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = new MmlDatum(-1, enmMMLType.KeyShift, pw.cmd.linePos
                , (int)(sbyte)pw.partWk[r.di].shift
                );
            WriteDummy(cd);

            return null;
        }


        //3625-3633
        //;==============================================================================
        //;	COMMAND '__' [相対転調]
        //;==============================================================================
        public Func<object> comshift2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comshift2");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al += pw.partWk[r.di].shift;
            pw.partWk[r.di].shift = r.al;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = new MmlDatum(-1, enmMMLType.KeyShift, pw.cmd.linePos
                , (int)(sbyte)pw.partWk[r.di].shift
                );
            WriteDummy(cd);

            return null;
        }



        //3634-3641
        //;==============================================================================
        //;	COMMAND '_M' [Master転調値]
        //;==============================================================================
        public Func<object> comshift_master()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comshift_master");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].shift_def = r.al;
            return null;
        }



        //3642-3654
        //;==============================================================================
        //;	COMMAND ')' [VOLUME UP]
        //;==============================================================================
        //	;	ＦＯＲ ＦＭ
        private Func<object> comvolup()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvolup");
#endif
            
            r.al = pw.partWk[r.di].volume;
            r.al += 4;
            return volupck;
        }

        private Func<object> volupck()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "volupck");
#endif

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, Math.Min((int)r.al, 127));
            cd.addtionalData = md;
            WriteDummy(cd);

            if (r.al < 128)
                return vset;
            r.al = 127;
            return vset;
        }

        private Func<object> vset()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "vset");
#endif

            pw.partWk[r.di].volume = r.al;
            return null;
        }



        //3656-3661
        //; 数字付き
        private Func<object> comvolup2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvolup2");
#endif
            
            r.al = (byte)pw.md[r.si++].dat;
            r.al += pw.partWk[r.di].volume;
            return volupck;
        }



        //3662-3671
        //; ＦＯＲ ＰＳＧ
        private Func<object> comvolupp()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvolupp");
#endif
            
            r.al = pw.partWk[r.di].volume;
            r.al++;
            return volupckp;
        }

        private Func<object> volupckp()
        {
            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, Math.Min((int)r.al, 15));
            cd.addtionalData = md;
            WriteDummy(cd);

            if (r.al < 16)
                return vset;
            r.al = 15;
            return vset;
        }

        //    ; 数字付き
        private Func<object> comvolupp2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvolupp2");
#endif
            
            r.al = (byte)pw.md[r.si++].dat;
            r.al += pw.partWk[r.di].volume;
            return volupckp;
        }



        //3678-3716
        //;==============================================================================
        //;	COMMAND '(' [VOLUME DOWN]
        //;==============================================================================
        //	;	ＦＯＲ ＦＭ
        private Func<object> comvoldown()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvoldown");
#endif

            r.al = pw.partWk[r.di].volume;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, Math.Max((int)(r.al - 4), 0));
            cd.addtionalData = md;
            WriteDummy(cd);

            r.carry = (r.al < 4);
            r.al -= 4;
            if (!r.carry) return vset;
            r.al = 0;
            return vset;
        }

        //    ; 数字付き
        private Func<object> comvoldown2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvoldown2");
#endif
            
            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;
            r.al = pw.partWk[r.di].volume;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, Math.Max((int)(r.al - r.ah), 0));
            cd.addtionalData = md;
            WriteDummy(cd);

            r.carry = (r.al < r.ah);
            r.al -= r.ah;
            if (!r.carry) return vset;
            r.al = 0;
            return vset;
        }

        //	;	ＦＯＲ ＰＳＧ
        private Func<object> comvoldownp()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvoldownp");
#endif
            
            r.al = pw.partWk[r.di].volume;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, Math.Max((int)(r.al - 1), 0));
            cd.addtionalData = md;
            WriteDummy(cd);

            if (r.al == 0) return vset;
            r.al--;
            return vset;
        }

        //    ; 数字付き
        private Func<object> comvoldownp2()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comvoldownp2");
#endif
            
            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;
            r.al = pw.partWk[r.di].volume;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, Math.Max((int)(r.al - r.ah), 0));
            cd.addtionalData = md;
            WriteDummy(cd);

            r.carry = (r.al < r.ah);
            r.al -= r.ah;
            if (!r.carry) return vset;
            r.al = 0;
            return vset;
        }



        //3717-3721
        //;==============================================================================
        //;	ＬＦＯ２用処理
        //;==============================================================================
        public Func<object> _lfoset()
        {
            r.ax = 0;//offset lfoset
            return _lfo_main(lfoset);
        }



        //3722-3732
        private Func<object> _lfo_main(Func<object> fnc)
        {
            //pushf
            //cli
            r.stack.Push(r.ax);
            lfo_change();
            r.ax = r.stack.Pop();
            object o = fnc();
            while (o != null) o = ((Func<object>)o)();
            lfo_change();
            //popf
            return null;
        }



        //3733-3736
        public Func<object> _mdepth_set()
        {
            r.ax = 0;//offset lfoset
            return _lfo_main(mdepth_set);
        }



        //3737-3740
        public Func<object> _lfowave_set()
        {
            r.ax = 0;//offset lfoset
            return _lfo_main(lfowave_set);
        }



        //3741-3744
        public Func<object> _lfo_extend()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "_lfo_extend");
#endif

            r.ax = 0;//offset lfo_extend
            return _lfo_main(lfo_extend);
        }



        //3745-3748
        public Func<object> _lfoset_delay()
        {
            r.ax = 0;//offset lfoset
            return _lfo_main(lfoset_delay);
        }



        //3749-3761
        public Func<object> _lfoswitch()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.al &= 7;
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            pw.partWk[r.di].lfoswi &= 0x8f;
            pw.partWk[r.di].lfoswi |= r.al;
            lfo_change();
            lfoinit_main();
            lfo_change();
            return null;
        }



        //3762-3765
        private Func<object> _lfoswitch_f()
        {
            _lfoswitch();
            return ch3_setting;
        }


        //3766-3809
        //;==============================================================================
        //;	LFO1<->LFO2 change
        //;==============================================================================
        public void lfo_change()
        {
            r.ax = pw.partWk[r.di].lfodat;
            pw.partWk[r.di].lfodat = pw.partWk[r.di]._lfodat;
            pw.partWk[r.di]._lfodat = r.ax;

            r.cl = 4;
            pw.partWk[r.di].lfoswi = (byte)((pw.partWk[r.di].lfoswi << 4) | ((pw.partWk[r.di].lfoswi & 0xf0) >> 4));
            pw.partWk[r.di].extendmode = (byte)((pw.partWk[r.di].extendmode << 4) | ((pw.partWk[r.di].extendmode & 0xf0) >> 4));


            r.al = pw.partWk[r.di].delay;
            pw.partWk[r.di].delay = pw.partWk[r.di]._delay;
            pw.partWk[r.di]._delay = r.al;

            r.al = pw.partWk[r.di].speed;
            pw.partWk[r.di].speed = pw.partWk[r.di]._speed;
            pw.partWk[r.di]._speed = r.al;


            r.al = pw.partWk[r.di].step;
            pw.partWk[r.di].step = pw.partWk[r.di]._step;
            pw.partWk[r.di]._step = r.al;

            r.al = pw.partWk[r.di].time;
            pw.partWk[r.di].time = pw.partWk[r.di]._time;
            pw.partWk[r.di]._time = r.al;


            r.al = pw.partWk[r.di].delay2;
            pw.partWk[r.di].delay2 = pw.partWk[r.di]._delay2;
            pw.partWk[r.di]._delay2 = r.al;

            r.al = pw.partWk[r.di].speed2;
            pw.partWk[r.di].speed2 = pw.partWk[r.di]._speed2;
            pw.partWk[r.di]._speed2 = r.al;


            r.al = pw.partWk[r.di].step2;
            pw.partWk[r.di].step2 = pw.partWk[r.di]._step2;
            pw.partWk[r.di]._step2 = r.al;

            r.al = pw.partWk[r.di].time2;
            pw.partWk[r.di].time2 = pw.partWk[r.di]._time2;
            pw.partWk[r.di]._time2 = r.al;


            r.al = pw.partWk[r.di].mdepth;
            pw.partWk[r.di].mdepth = pw.partWk[r.di]._mdepth;
            pw.partWk[r.di]._mdepth = r.al;

            r.al = pw.partWk[r.di].mdspd;
            pw.partWk[r.di].mdspd = pw.partWk[r.di]._mdspd;
            pw.partWk[r.di]._mdspd = r.al;


            r.al = pw.partWk[r.di].mdspd2;
            pw.partWk[r.di].mdspd2 = pw.partWk[r.di]._mdspd2;
            pw.partWk[r.di]._mdspd2 = r.al;
            r.ah = pw.partWk[r.di].lfo_wave;
            pw.partWk[r.di].lfo_wave = pw.partWk[r.di]._lfo_wave;
            pw.partWk[r.di]._lfo_wave = r.ah;

            r.al = pw.partWk[r.di].mdc;
            pw.partWk[r.di].mdc = pw.partWk[r.di]._mdc;
            pw.partWk[r.di]._mdc = r.al;
            r.al = pw.partWk[r.di].mdc2;
            pw.partWk[r.di].mdc2 = pw.partWk[r.di]._mdc2;
            pw.partWk[r.di]._mdc2 = r.al;

            return;
        }



        //3810-3826
        //;==============================================================================
        //;	LFO ﾊﾟﾗﾒｰﾀ ｾｯﾄ
        //;==============================================================================
        public Func<object> lfoset()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "lfoset");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].delay = r.al;
            pw.partWk[r.di].delay2 = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].speed = r.al;
            pw.partWk[r.di].speed2 = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].step = r.al;
            pw.partWk[r.di].step2 = r.al;

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].time = r.al;
            pw.partWk[r.di].time2 = r.al;

            return lfoinit_main;
        }



        //3827-3832
        public Func<object> lfoset_delay()
        {
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].delay = r.al;
            pw.partWk[r.di].delay2 = r.al;

            return lfoinit_main;
        }



        //3833-3846
        //;==============================================================================
        //;	LFO SWITCH
        //;==============================================================================
        public Func<object> lfoswitch()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "lfoswitch");
#endif
            r.al = (byte)pw.md[r.si++].dat;
            if ((r.al & 0b1111_1000) == 0)
                goto ls_00;
            r.al = 1;

        ls_00:;
            r.al &= 7;
            pw.partWk[r.di].lfoswi &= 0xf8;
            pw.partWk[r.di].lfoswi |= r.al;
            return lfoinit_main;
        }



        //3847-3850
        private Func<object> lfoswitch_f()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "lfoswitch_f");
#endif
            object o = lfoswitch();
            while (o != null) o = ((Func<object>)o)();
            return ch3_setting;
        }



        //3851-3872
        //;==============================================================================
        //;	PSG ENVELOPE SET
        //;==============================================================================
        public Func<object> psgenvset()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "psgenvset");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].eenv_ar = r.al;//pat
            pw.partWk[r.di].eenv_arc = r.al;//patb
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].eenv_dr = r.al;//pv2
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].eenv_sr = r.al;//pr1
            pw.partWk[r.di].eenv_src = r.al;//pr1b
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].eenv_rr = r.al;//pr2
            pw.partWk[r.di].eenv_rrc = r.al;//pr2b

            if (pw.partWk[r.di].envf!=0xff)
                goto not_set_count2;//; 拡張＞ノーマルに移行したか？

            pw.partWk[r.di].envf = 2;//; RR
            pw.partWk[r.di].eenv_volume = 0xf1;// -15;//;Volume//.penv
        not_set_count2:;
            return null;
        }



        //3873-3881
        //;==============================================================================
        //;	'y' COMMAND[ｺｲﾂｶﾞ ｲﾁﾊﾞﾝ ｶﾝﾀﾝ]
        //;==============================================================================
        public Func<object> comy()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "comy");
#endif

            r.dh = (byte)pw.md[r.si++].dat;
            r.dl = (byte)pw.md[r.si++].dat;
            opnset();
            return null;
        }



        //3882-3902
        //;==============================================================================
        //;	'w' COMMAND[PSG NOISE ﾍｲｷﾝ ｼｭｳﾊｽｳ]
        //;==============================================================================
        private Func<object> psgnoise()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "psgnoise");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.psnoi = r.al;
            return null;
        }

        private Func<object> psgnoise_move()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "psgnoise_move");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            r.al+=pw.psnoi;
            if ((r.al & 0x80) == 0)
                goto pnm_nminus;
            r.al = 0;

        pnm_nminus:;
            if (r.al < 32)
                goto pnm_set;
            r.al = 31;

        pnm_set:;
            pw.psnoi = r.al;
            return null;
        }



        //3903-3910
        //;==============================================================================
        //;	'P' COMMAND[PSG TONE / NOISE / MIX SET]
        //;==============================================================================
        private Func<object> psgsel()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "psgsel");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].psgpat = r.al;
            return null;
        }



        //3911-3956
        //;==============================================================================
        //;	'p' COMMAND[FM PANNING SET]
        //;==============================================================================
        private Func<object> panset()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (pw.board2 != 0)
            {
                return panset_main();
            }
            return null;
        }

        private Func<object> panset_main()
        {
            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = new MmlDatum(-1, enmMMLType.Pan, pw.cmd.linePos
                , (int)r.al
                );
            WriteDummy(cd);

            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al &= 0b1100_0000;
            r.ah = r.al;//;ah<- pan data
            r.al = pw.partWk[r.di].fmpan;
            r.al &= 0b0011_1111;
            r.al |= r.ah;
            pw.partWk[r.di].fmpan = r.al;
            if (pw.partb != 3)
                goto panset_notfm3;
            if (pw.fmsel != 0)
                goto panset_notfm3;
            //;	FM3の場合は 4つのパート総て設定
            r.stack.Push(r.di);
            r.di = (ushort)pw.part3;
            pw.partWk[r.di].fmpan = r.al;
            r.di = (ushort)pw.part3b;
            pw.partWk[r.di].fmpan = r.al;
            r.di = (ushort)pw.part3c;
            pw.partWk[r.di].fmpan = r.al;
            r.di = (ushort)pw.part3d;
            pw.partWk[r.di].fmpan = r.al;
            r.di = r.stack.Pop();

        panset_notfm3:;
            if (pw.partWk[r.di].partmask != 0)//; パートマスクされているか？
                goto panset_exit;
            r.dl = r.al;
            r.dh = pw.partb;
            r.dh += 0xb4 - 1;
            calc_panout();
            opnset();

        panset_exit:;
            return null;
        }


        //3957-3968
        //;==============================================================================
        //;	0b4h～に設定するデータを取得 out.dl
        //;==============================================================================
        private void calc_panout()
        {
            if (pw.board2 != 0)
            {
                r.dl = pw.partWk[r.di].fmpan;
                if (pw.partWk[r.di].hldelay_c == 0)
                    goto cpo_ret;

                r.dl &= 0xc0;//;HLFO Delayが残ってる場合はパンのみ設定
            cpo_ret:;
                return;
            }
        }



        //3969-3990
        //;==============================================================================
        //;	Pan setting Extend
        //;==============================================================================
        private Func<object> panset_ex()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.si++;//;逆走flagは読み飛ばす
            if (pw.board2 != 0)
            {
                if (r.al == 0)
                    goto pex_mid;
                if ((r.al & 0x80) != 0)
                    goto pex_left;

                r.al = 2;
                return panset_main();
            pex_mid:;
                r.al = 3;
                return panset_main();
            pex_left:;
                r.al = 1;
                return panset_main();
            }
            else
            {
                return null;
            }
        }



        //3991-4060
        //;==============================================================================
        //;	"\?" COMMAND[OPNA Rhythm Keyon / Dump]
        //;==============================================================================
        public Func<object> rhykey()
        {
            if (pw.board2 != 0)
            {
                r.dh = 0x10;
                r.al = (byte)pw.md[r.si++].dat;
                r.al &= pw.rhythmmask;
                if (r.al == 0) goto rhst_ret;
                r.dl = r.al;
                if (pw.fadeout_volume == 0)
                    goto rk_00;
                r.stack.Push(r.dx);
                r.dl = pw.rhyvol;
                volset2rf();
                r.dx = r.stack.Pop();
            rk_00:;
                if ((r.dl & 0x80) != 0)
                    goto rhyset;
                r.al = r.dl;
                r.bx = 0;//offset rdat
                r.stack.Push(r.dx);
                r.cx = 6;
                r.dh = 0x18;
            //rklp:;
                do
                {
                    r.al = r.ror(r.al, 1);
                    if (!r.carry) goto rk00;
                    r.dl = pw.rdat[r.bx];
                    opnset44();
                rk00:;
                    r.bx++;
                    r.dh++;
                    r.cx--;
                } while (r.cx != 0);
                r.dx = r.stack.Pop();

            rhyset:;
                opnset44();
                if ((r.dl & 0x80) == 0)
                    goto rhst_00;

                r.bx = 0;//offset rdump_bd
                rflag_inc(false);
                r.dl = (byte)~r.dl;
                pw.rshot_dat &= r.dl;
                goto rhst_ret2;
            rhst_00:;
                r.bx = 0;//offset rshot_bd
                rflag_inc(true);
                pw.rshot_dat |= r.dl;
            rhst_ret2:;
                if (pw.md[r.si].dat != 0xeb)
                    goto rhst_ret;
                _rwait();
                rhst_ret:;
                return null;
            }
            else
            {
                r.si++;
                return null;
            }
        }

        private void rflag_inc(bool isShot)
        {
            r.al = r.dl;
            r.cx = 6;
        //ri_loop:;
            do
            {
                r.al = r.ror(r.al, 1);
                if (!r.carry) goto ri_not;
                if (isShot) pw.rshot[r.bx]++;
                else pw.rdump[r.bx]++;
                ri_not:;
                r.bx++;
                r.cx--;
            } while (r.cx != 0);
        }



        //4061-4132
        //;==============================================================================
        //;	"\v?n" COMMAND
        //;==============================================================================
        public Func<object> rhyvs()
        {
            if (pw.board2 != 0)
            {
                r.al = (byte)pw.md[r.si++].dat;
                r.cl = 0xc0;
                r.dl = 0x1f;
                r.dl &= r.al;
                return rs002();
            }
            else
            {
                r.si++;
                return null;
            }
        }

        private Func<object> rs002()
        {
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al = r.rol(r.al, 1);
            r.al &= 0x7;
            r.bx = 0xffff;//offset rdat-1
            r.dh = r.al;
            r.ah = 0;
            r.bx += r.ax;
            r.al = 0x18 - 1;
            r.al += r.dh;
            r.dh = r.al;
            r.al = pw.rdat[r.bx];
            r.al &= r.cl;
            r.dl |= r.al;
            pw.rdat[r.bx] = r.dl;
            opnset44();
            return null;
        }

        public Func<object> rhyvs_sft()
        {
            if (pw.board2 != 0)
            {
                r.al = (byte)pw.md[r.si++].dat;
                r.bx = 0xffff;//offset rdat-1
                r.dh = r.al;
                r.ah = 0;
                r.bx += r.ax;

                r.dh += 0x18 - 1;
                r.al = pw.rdat[r.bx];
                r.al &= 0b0001_1111;
                r.dl = r.al;
                r.al = (byte)pw.md[r.si++].dat;
                r.al += r.dl;
                if (r.al < 32)
                    goto rvss00;
                if ((r.al & 0x80) != 0)
                    goto rvss01;

                r.al = 31;
                goto rvss00;
            rvss01:;
                r.al = 0;
            rvss00:;
                r.al &= 0b0001_1111;
                r.dl = r.al;
                r.al = pw.rdat[r.bx];
                r.al &= 0b1110_0000;
                r.dl |= r.al;
                pw.rdat[r.bx] = r.dl;
                opnset44();
                return null;
            }
            else
            {
                r.si+=2;
                return null;
            }
        }



        //4133-4151
        //;==============================================================================
        //;	"\p?" COMMAND
        //;==============================================================================
        public Func<object> rpnset()
        {
            if (pw.board2 != 0)
            {
                r.al = (byte)pw.md[r.si++].dat;
                r.ah = r.al;
                r.cl = 0x1f;
                r.ah &= 3;
                r.ah = r.ror(r.ah, 1);
                r.ah = r.ror(r.ah, 1);
                r.dl = r.ah;
                return rs002();
            }
            else
            {
                r.si++;
                return null;
            }
        }



        //4152-4168
        //;==============================================================================
        //;	"\Vn" COMMAND
        //;==============================================================================
        public Func<object> rmsvs()
        {
            if (pw.board2 != 0)
            {
                r.al = (byte)pw.md[r.si++].dat;
                r.dl = r.al;
                r.al = pw.rhythm_voldown;
                if (r.al != 0)
                {
                    r.al = (byte)-r.al;
                    r.ax = (ushort)(r.al * r.dl);
                    r.dl = r.ah;
                }
                volset2r();
            }
            else
            {
                r.si++;
            }

            return null;
        }

        private void volset2r()
        {
            pw.rhyvol = r.dl;
            volset2rf();
        }

        //4169-4181
        private void volset2rf()
        {
            if (pw.board2 != 0)
            {
                r.dh = 0x11;
                r.al = pw.fadeout_volume;
                if (r.al == 0)
                    goto vs2r_000;
                r.al = (byte)~r.al;
                int ans = r.al * r.dl;
                r.dl = (byte)(ans >> 8);
            vs2r_000:;
                opnset44();
            }
        }



        //4184-4204
        public Func<object> rmsvs_sft()
        {
            if (pw.board2 != 0)
            {
                r.dh = 0x11;
                r.al = (byte)pw.md[r.si++].dat;
                r.dl = r.al;
                r.al = pw.rhyvol;
                r.al += r.dl;
                if (r.al < 64)
                    goto rmss00;
                if ((r.al & 0x80) != 0)
                    goto rmss01;
                r.al = 63;
                goto rmss00;
            rmss01:;
                r.al = 0;
            rmss00:;
                r.dl = r.al;
                volset2r();
                return null;
            }
            else
            {
                r.si++;
                    return null;
            }
        }



        //4205-4261
        //;==============================================================================
        //;	SHIFT[di] 分移調する
        //;==============================================================================
        public void oshift()
        {
            //oshiftp:
            if (r.al == 0xf)//;休符
                return;
            r.dl = pw.partWk[r.di].shift;
            r.dl += pw.partWk[r.di].shift_def;
            if ((r.dl & r.dl) == 0)
                return;

            r.bl = r.al;
            r.bl &= 0xf;
            r.al &= 0xf0;
            r.al >>= 4;//KUMA:ホントはror x4
            r.bh = r.al;//; bh=OCT bl = ONKAI

            if ((r.dl & 0x80) == 0)
                goto shiftplus;

            //;
            //; - ﾎｳｺｳ ｼﾌﾄ
            //;
            //shiftminus:
            r.carry = false;
            if (r.bl + r.dl > 0xff) r.carry = true;
            r.bl += r.dl;
            if (r.carry) goto sfm2;

            //sfm1:
            do
            {
                r.bh--;
                r.carry = false;
                if (r.bl + 12 > 0xff) r.carry = true;
                r.bl += 12;
            } while (!r.carry);

        sfm2:;
            r.al = r.bh;
            r.al = (byte)((r.al >> 4) | (byte)(r.al << 4));//ror x4
            r.al |= r.bl;
            return;

        //;
        //; + ﾎｳｺｳ ｼﾌﾄ
        //;
        shiftplus:;
            r.bl += r.dl;
        //spm1:;
            do
            {
                if (r.bl < 0xc)
                    goto spm2;
                r.bh++;
                r.bl -= 12;
            } while (true);
        spm2:;
            r.al = r.bh;
            r.al = (byte)((r.al >> 4) | (byte)(r.al << 4));//ror x4
            r.al |= r.bl;
            return;

            //osret:	ret
        }

        private void oshiftp()
        {
            oshift();
        }

        //4262-4331
        //;==============================================================================
        //;	ＦＭ BLOCK, F-NUMBER SET
        // ; INPUTS	-- AL[KEY#,0-7F]
        //;==============================================================================
        private void fnumset()
        {
            r.ah = r.al;
            r.ah &= 0xf;
            if (r.ah == 0xf)
            {
                fnrest();//; 休符の場合
                return;
            }
            pw.partWk[r.di].onkai = r.al;

            //;
            //; BLOCK/FNUM CALICULATE
            //;
            r.ch = r.al;
            r.ch = r.ror(r.ch, 1);
            r.ch &= 0x38;//; ch=BLOCK
            r.bl = r.al;
            r.bl &= 0xf;//; bl=ONKAI
            r.bh = 0;
            //r.bx += r.bx;
            r.ax = pw.fnum_data[r.bx];

            //;
            //; BLOCK SET
            //;
            r.ah |= r.ch;
            pw.partWk[r.di].fnum = r.ax;
            return;
        }

        public void fnrest()
        { 
            pw.partWk[r.di].onkai = 0xff;
            if ((pw.partWk[r.di].lfoswi & 0x11) != 0)
                goto fnr_ret;
            pw.partWk[r.di].fnum = 0;//;音程LFO未使用
        fnr_ret:;
            return;
        }

        //;
        //; PSG TUNE SET
        //;
        private void fnumsetp()
        {
            r.ah = r.al;
            r.ah &= 0xf;
            if (r.ah == 0xf)
            {
                fnrest();//; ｷｭｳﾌ ﾅﾗ FNUM ﾆ 0 ｦ ｾｯﾄ
                return;
            }
            pw.partWk[r.di].onkai = r.al;

            r.cl = r.al;
            r.cl = (byte)((r.cl >> 4) | (byte)(r.cl << 4));//ror x4
            r.cl &= 0xf;//;cl=oct
            r.bl = r.al;
            r.bl &= 0xf;
            r.bh = 0;//;bx=onkai
            //r.bx += r.bx;
            r.ax = pw.psg_tune_data[r.bx];

            r.carry = r.cl == 0 ? false : ((r.ax & (1 << (r.cl - 1))) != 0);
            r.ax = (ushort)((short)r.ax >> r.cl);//    shr ax,cl

            if (!r.carry) goto pt_non_inc;
            r.ax++;
        pt_non_inc:;
            pw.partWk[r.di].fnum = r.ax;
            return;
        }



        //4332-4393
        //;==============================================================================
        //;	Set[FNUM / BLOCK + DETUNE + LFO]
        //;==============================================================================
        private void otodasi()
        {
            r.ax = pw.partWk[r.di].fnum;
            if (r.ax != 0)
                goto od_00;
            return;
        od_00:;
            if (pw.partWk[r.di].slotmask == 0)
                goto od_exit;
            r.cx = r.ax;
            r.cx &= 0x3800;//; cx=BLOCK
            r.ah &= 7;//; ax=FNUM
            //;
            //; Portament/LFO/Detune SET
            //;
            r.ax += pw.partWk[r.di].porta_num;
            r.ax += pw.partWk[r.di].detune;
            r.dh = pw.partb;
            if (r.dh != 3)//; Ch 3
                goto od_non_ch3;

            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto od_non_ch3;
            }
            else
            {
                if (r.di == pw.part_e) //offset part_e
                    goto od_non_ch3;
            }

            if (pw.ch3mode != 0x3f)
            {
                ch3_special();
                return;
            }

            od_non_ch3:;
            if ((pw.partWk[r.di].lfoswi & 1) == 0)
                goto od_not_lfo1;
            r.ax += pw.partWk[r.di].lfodat;
        od_not_lfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x10) == 0)
                goto od_not_lfo2;
            r.ax += pw.partWk[r.di]._lfodat;
        od_not_lfo2:;
            fm_block_calc();

#if DEBUG
            //Log.WriteLine(LogLevel.TRACE, string.Format("cx:{0:X4}  ax:{1:X4}  lfodat:{2}  step:{3}"
            //    , r.cx, r.ax, (short)pw.partWk[r.di].lfodat, (sbyte)pw.partWk[r.di].step));
#endif

            //;
            //; SET BLOCK/FNUM TO OPN
            //;	input CX:AX
            r.ax |= r.cx;//;AX=block/Fnum
            r.dh += 0xa4 - 1;
            r.dl = r.ah;
            //    pushf
            //    cli
            opnset();
            r.dh -= 4;
            r.dl = r.al;
            opnset();
            //    popf
        od_exit:;
            return;
        }



        //4394-4543
        //;==============================================================================
        //;	ch3=効果音モード を使用する場合の音程設定
        //; input CX:block AX:fnum
        //;==============================================================================
        private void ch3_special()
        {
            r.stack.Push(r.si);
            r.si = r.cx;//; si=block
            r.bl = pw.partWk[r.di].slotmask;//;bl=slot mask 4321xxxx
            r.cl = pw.partWk[r.di].lfoswi;//;cl=lfoswitch
            r.bh = pw.partWk[r.di].volmask;//;bh=lfo1 mask 4321xxxx
            if ((r.bh & 0xf) != 0)
                goto c3s_00;
            r.bh = 0xf0;//;all
        c3s_00:;
            r.ch = pw.partWk[r.di]._volmask;//; ch=lfo2 mask 4321xxxx
            if ((r.ch & 0xf) != 0)
                goto ns_sl4;
            r.ch = 0xf0;//;all


        //;	slot	4
        ns_sl4:;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry) goto ns_sl3;

            r.stack.Push(r.ax);
            r.ax += pw.slot_detune4;
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry) goto ns_sl4b;
            if ((r.cl & 1) == 0) goto ns_sl4b;
            r.ax += pw.partWk[r.di].lfodat;

        ns_sl4b:;
            r.carry = (r.ch & 0x80) != 0;
            r.ch = (byte)((r.ch << 1) | (r.ch >> 7));
            if (!r.carry) goto ns_sl4c;
            if ((r.cl & 0x10) == 0) goto ns_sl4c;
            r.ax += pw.partWk[r.di]._lfodat;

        ns_sl4c:;
            r.stack.Push(r.cx);
            r.cx = r.si;
            fm_block_calc();
            r.ax |= r.cx;
            r.cx = r.stack.Pop();

            r.dh = 0xa6;
            r.dl = r.ah;
            //    pushf
            //    cli
            opnset();
            r.dh = 0xa2;
            r.dl = r.al;
            opnset();
            //    popf
            r.ax = r.stack.Pop();


        //; slot	3
        ns_sl3:;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry) goto ns_sl2;

            r.stack.Push(r.ax);
            r.ax += pw.slot_detune3;
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry) goto ns_sl3b;
            if ((r.cl & 1) == 0) goto ns_sl3b;
            r.ax += pw.partWk[r.di].lfodat;

        ns_sl3b:;
            r.carry = (r.ch & 0x80) != 0;
            r.ch = (byte)((r.ch << 1) | (r.ch >> 7));
            if (!r.carry) goto ns_sl3c;
            if ((r.cl & 0x10) == 0) goto ns_sl3c;
            r.ax += pw.partWk[r.di]._lfodat;

        ns_sl3c:;
            r.stack.Push(r.cx);
            r.cx = r.si;
            fm_block_calc();
            r.ax |= r.cx;
            r.cx = r.stack.Pop();

            r.dh = 0xac;
            r.dl = r.ah;
            //    pushf
            //    cli
            opnset();
            r.dh = 0xa8;
            r.dl = r.al;
            opnset();
            //    popf
            r.ax = r.stack.Pop();


        //; slot	2
        ns_sl2:;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry) goto ns_sl1;

            r.stack.Push(r.ax);
            r.ax += pw.slot_detune2;
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry) goto ns_sl2b;
            if ((r.cl & 1) == 0) goto ns_sl2b;
            r.ax += pw.partWk[r.di].lfodat;

        ns_sl2b:;
            r.carry = (r.ch & 0x80) != 0;
            r.ch = (byte)((r.ch << 1) | (r.ch >> 7));
            if (!r.carry) goto ns_sl2c;
            if ((r.cl & 0x10) == 0) goto ns_sl2c;
            r.ax += pw.partWk[r.di]._lfodat;

        ns_sl2c:;
            r.stack.Push(r.cx);
            r.cx = r.si;
            fm_block_calc();
            r.ax |= r.cx;
            r.cx = r.stack.Pop();

            r.dh = 0xae;
            r.dl = r.ah;
            //    pushf
            //    cli
            opnset();
            r.dh = 0xaa;
            r.dl = r.al;
            opnset();
            //    popf
            r.ax = r.stack.Pop();

        //; slot	1
        ns_sl1:;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry) goto ns_exit;

            r.ax += pw.slot_detune1;
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry) goto ns_sl1b;
            if ((r.cl & 1) == 0) goto ns_sl1b;
            r.ax += pw.partWk[r.di].lfodat;

        ns_sl1b:;
            r.carry = (r.ch & 0x80) != 0;
            r.ch = (byte)((r.ch << 1) | (r.ch >> 7));
            if (!r.carry) goto ns_sl1c;
            if ((r.cl & 0x10) == 0) goto ns_sl1c;
            r.ax += pw.partWk[r.di]._lfodat;

        ns_sl1c:;
            r.cx = r.si;
            fm_block_calc();
            r.ax |= r.cx;

            r.dh = 0xad;
            r.dl = r.ah;
            //    pushf
            //    cli
            opnset();
            r.dh = 0xa9;
            r.dl = r.al;
            opnset();
        //    popf

        ns_exit:;
            r.si = r.stack.Pop();

            return;
        }



        //4544-4584
        //;==============================================================================
        //;	FM音源のdetuneでオクターブが変わる時の修正
        //;		input CX:block / AX:fnum+detune
        //;		output CX:block / AX:fnum
        //;==============================================================================
        private void fm_block_calc()
        {
            r.sign = (r.ax & 0x8000) != 0;
        od0:;
            if (r.sign) goto od1;

            if (r.ax < 0x26a) goto od1;
            //;
            if (r.ax < 0x26a * 2)//;04d2h
                goto od2;
            //;

            r.cx += 0x800;//;oct.up
            if (r.cx == 0x4000) goto od05;
            r.ax -= 0x26a;//;4d2h-26ah
            r.sign = (r.ax & 0x8000) != 0;
            goto od0;

        od05:;//; ﾓｳ ｺﾚｲｼﾞｮｳ ｱｶﾞﾝﾅｲﾖﾝ
            r.cx = 0x3800;
            if (r.ax < 0x800)
                goto od_ret;
            r.ax = 0x7ff;//;04d2h
        od_ret:;
            return;
        //	;
        od1:;
            r.carry = r.cx < 0x800;
            r.cx -= 0x800;//;oct.down
            if (r.carry) goto od15;
            r.ax += 0x26a;//;4d2h-26ah
            r.sign = (r.ax & 0x8000) != 0;
            goto od0;

        od15:;//; ﾓｳ ｺﾚｲｼﾞｮｳ ｻｶﾞﾝﾅｲﾖﾝ
            r.cx = 0;
            r.sign = (r.ax & 0x8000) != 0;
            if (r.sign) goto od16;
            if (r.ax >= 8)//;4
                goto od2;
            od16:;
            r.ax = 8;//;4
                     //	;
        od2:;
            return;
        }



        //4585-4699
        //;==============================================================================
        //;	ＰＳＧ 音程設定
        //;==============================================================================
        private void otodasip()
        {
            r.ax = pw.partWk[r.di].fnum;
            if (r.ax != 0)
                goto od_00p;
            return;
        od_00p:;
            //	;
            //	; PSG Portament set
            //	;
            r.ax = (ushort)((short)r.ax + (short)pw.partWk[r.di].porta_num);
            //  ;
            //	; PSG Detune/LFO set
            //  ;
            if ((pw.partWk[r.di].extendmode & 1) != 0)
                goto od_ext_detune;
            r.ax -= pw.partWk[r.di].detune;
            if ((pw.partWk[r.di].lfoswi & 1) == 0)
                goto od_notlfo1;
            r.ax -= pw.partWk[r.di].lfodat;
        od_notlfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x10) == 0)
                goto tonesetp;
            r.ax -= pw.partWk[r.di]._lfodat;
            goto tonesetp;
        od_ext_detune:;//; 拡張DETUNE(DETUNE)の計算
            r.stack.Push(r.ax);
            r.bx = pw.partWk[r.di].detune;
            if (r.bx == 0)
                goto od_ext_lfo;//;LFOへ

            int ans = (short)r.ax * (short)r.bx;//  imul    bx
            ans <<= 4;
            r.dx = (ushort)(ans >> 16);
            r.ax = (ushort)(ans >> 0);
            if (ans < 0)
                goto extdet_minus;
            r.dx++;
            goto extdet_set;
        extdet_minus:;
            r.dx--;
        extdet_set:;
            r.ax = r.stack.Pop();
            r.ax -= r.dx;//; Detuneをずらす
            r.stack.Push(r.ax);
        od_ext_lfo:;//; 拡張DETUNE(LFO)の計算
            r.dx = 0;
            if ((pw.partWk[r.di].lfoswi & 0x11) == 0)
                goto extlfo_set;
            r.dx = 0;
            if ((pw.partWk[r.di].lfoswi & 0x1) == 0)
                goto od_ext_notlfo1;
            r.dx = pw.partWk[r.di].lfodat;
        od_ext_notlfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x10) == 0)
                goto od_ext_notlfo2;
            r.dx += pw.partWk[r.di]._lfodat;
        od_ext_notlfo2:;
            if (r.dx == 0)
                goto extlfo_set;
            int ans1 = (short)r.ax * (short)r.dx;//  imul    dx
            ans1 <<= 4;
            r.dx = (ushort)(ans1 >> 16);
            r.ax = (ushort)(ans1 >> 0);
            if (ans1 < 0)
                goto extlfo_minus;
            r.dx++;
            goto extlfo_set;
        extlfo_minus:;
            r.dx--;
        extlfo_set:;
            r.ax = r.stack.Pop();
            r.ax -= r.dx;//; LFOをずらす
                         //	;
                         //	; TONE SET
                         // ;
        tonesetp:;
            if (true)//;突撃mixでは0 //KUMA:false
            {
                if (r.ax < 0x1000)
                    goto tsp_01;
                if ((r.ax & 0x8000) != 0)
                    goto tsp_00;
                r.ax = 0xfff;
                goto tsp_01;
            tsp_00:;
                r.ax = 0;
            }
        tsp_01:;
            r.dh = pw.partb;
            r.dh--;
            r.dh += r.dh;
            r.dl = r.al;
            //    pushf
            //    cli
            opnset44();
            r.dh++;
            r.dl = r.ah;
            opnset44();
            //    popf
            return;
        }



        //4700-4722
        //;==============================================================================
        //;	ＦＭ ＶＯＬＵＭＥ ＳＥＴ
        //;==============================================================================
        //;------------------------------------------------------------------------------
        //;	スロット毎の計算 & 出力 マクロ
        //;			in.	dl 元のTL値
        //; dh Outするレジスタ
        //; al 音量変動値 中心=80h
        //;------------------------------------------------------------------------------
        private void volset_slot()
        {
            r.carry = (r.al + r.dl > 0xff);
            r.al += r.dl;
            if (!r.carry)
                goto vsl_noover1;
            r.al = 255;
        vsl_noover1:;
            r.carry = (r.al < 0x80);
            r.al -= 0x80;
            if (!r.carry)
                goto vsl_noover2;
            r.al = 0;
        vsl_noover2:;
            r.dl = r.al;
            opnset();
        }



        //4723-4742
        //;------------------------------------------------------------------------------
        //;	ＦＭ音量設定メイン
        //;------------------------------------------------------------------------------
        private void volset()
        {
            r.bl = pw.partWk[r.di].slotmask; //; bl<- slotmask
            if (r.bl != 0)
                goto vs_exec;
            return;//;SlotMaskが0の時

        vs_exec:;
            r.al = pw.partWk[r.di].volpush;
            if (r.al == 0)
                goto vs_00a;

            r.al--;
            goto vs_00;

        vs_00a:;
            r.al = pw.partWk[r.di].volume;
        vs_00:;
            r.cl = r.al;
            if (r.di == pw.part_e)
            {
                fmvs();//;効果音の場合はvoldown/fadeout影響無し
                return;
            }

            pmdAsm_4743_voldown();
        }



        //4743-4752
        //;------------------------------------------------------------------------------
        //;	音量down計算
        //;------------------------------------------------------------------------------
        private void pmdAsm_4743_voldown()
        {
            r.al = pw.fm_voldown;
            if (r.al == 0)
            {
                fm_fade_calc();
                return;
            }

            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.cl);
            r.cl = r.ah;

            fm_fade_calc();
        }



        //4753-4764
        //;------------------------------------------------------------------------------
        //;	Fadeout計算
        //;------------------------------------------------------------------------------
        private void fm_fade_calc()
        {
            r.al = pw.fadeout_volume;
            if (r.al >= 2)
            {
                r.al >>= 1; //50%下げれば充分
                r.al = (byte)-r.al;
                r.ax = (ushort)(r.al * r.cl);
                r.cl = r.ah;
            }

            fmvs();
        }



        //4765-4860
        //;------------------------------------------------------------------------------
        //;	音量をcarrierに設定 & 音量LFO処理
        //;		input cl to Volume[0 - 127]
        //; bl to SlotMask
        //;------------------------------------------------------------------------------
        private void fmvs()
        {
            r.bh = 0;//; Vol Slot Mask
            r.ch = r.bl;//;ch=SlotMask Push

            r.stack.Push(r.si);
            r.si = 0;// offset vol_tbl
            pw.vol_tbl[r.si] = 0x80;
            pw.vol_tbl[r.si + 1] = 0x80;
            pw.vol_tbl[r.si + 2] = 0x80;
            pw.vol_tbl[r.si + 3] = 0x80;

            r.cl = (byte)~r.cl;//;cl=carrierに設定する音量+80H(add)
            r.bl &= pw.partWk[r.di].carrier;//; bl=音量 を設定するSLOT xxxx0000b
            r.bh |= r.bl;
            r.bl = r.rol(r.bl, 1);
            if (!r.carry)
                goto fmvs_01;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_01:;
            r.si++;
            r.bl = r.rol(r.bl, 1);
            if (!r.carry)
                goto fmvs_02;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_02:;
            r.si++;
            r.bl = r.rol(r.bl, 1);
            if (!r.carry)
                goto fmvs_03;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_03:;
            r.si++;
            r.bl = r.rol(r.bl, 1);
            if (!r.carry)
                goto fmvs_04;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_04:;
            r.si -= 3;
            if (r.cl == 255)//; 音量0?
                goto fmvs_no_lfo;

            if ((pw.partWk[r.di].lfoswi & 2) == 0)
                goto fmvs_not_vollfo1;

            r.bl = pw.partWk[r.di].volmask;
            r.bl &= r.ch;//; bl=音量LFOを設定するSLOT xxxx0000b
            r.bh |= r.bl;
            r.ax = pw.partWk[r.di].lfodat;//; ax=音量LFO変動値(sub)
            fmlfo_sub();

        fmvs_not_vollfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x20) == 0)
                goto fmvs_no_lfo;

            r.bl = pw.partWk[r.di]._volmask; // mov bl,_volmask[di]
            r.bl &= r.ch;//;bh=音量LFOを設定するSLOT xxxx0000b
            r.bh |= r.bl;
            r.ax = pw.partWk[r.di]._lfodat;//; ax=音量LFO変動値(sub)
            fmlfo_sub();

        fmvs_no_lfo:;
            r.dh = 0x4c - 1;
            r.dh +=(byte)pw.partb;//; dh=FM Port Address
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.bh = r.rol(r.bh, 1);
            if (!r.carry)
                goto fmvm_01;
            r.dl = pw.partWk[r.di].slot4;
            volset_slot();
        fmvm_01:;
            r.dh -= 8;
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.bh = r.rol(r.bh, 1);
            if (!r.carry)
                goto fmvm_02;
            r.dl = pw.partWk[r.di].slot3;
            volset_slot();
        fmvm_02:;
            r.dh += 4;
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.bh = r.rol(r.bh, 1);
            if (!r.carry)
                goto fmvm_03;
            r.dl = pw.partWk[r.di].slot2;
            volset_slot();
        fmvm_03:;
            r.bh = r.rol(r.bh, 1);
            if (!r.carry)
                goto fmvm_04;
            r.dh -= 8;
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.dl = pw.partWk[r.di].slot1;
            volset_slot();
        fmvm_04:;
            r.si = r.stack.Pop();
        }



        //4861-4886
        //;------------------------------------------------------------------------------
        //;	音量LFO用サブ
        //;------------------------------------------------------------------------------
        private void fmlfo_sub()
        {
            r.stack.Push(r.cx);
            r.cx = 4;
        fmlfo_loop:;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry)
                goto fml_exit;
            if ((r.al & 0x80) != 0)
                goto fmls_minus;
            r.carry = (pw.vol_tbl[r.si] < r.al);
            pw.vol_tbl[r.si] -= r.al;
            if (!r.carry)
                goto fml_exit;
            pw.vol_tbl[r.si] = 0;
            goto fml_exit;
        fmls_minus:;
            r.carry = (pw.vol_tbl[r.si] < r.al);
            pw.vol_tbl[r.si] -= r.al;
            if (r.carry)
                goto fml_exit;
            pw.vol_tbl[r.si] = 0xff;
        fml_exit:;
            r.si++;
            r.cx--;
            if (r.cx != 0) goto fmlfo_loop;

            r.cx = r.stack.Pop();
            r.si -= 4;
        }



        //4887-4994
        //;==============================================================================
        //;	ＰＳＧ ＶＯＬＵＭＥ ＳＥＴ
        //;==============================================================================
        private void volsetp()
        {
            if (pw.partWk[r.di].envf == 3)
                goto volsetp_ret;
            if (pw.partWk[r.di].envf != 0xff)//-1
                goto vsp_00;
            if (pw.partWk[r.di].eenv_count != 0)
                goto vsp_00;
            volsetp_ret:;
            return;
        vsp_00:;
            r.al = pw.partWk[r.di].volpush;
            if (r.al == 0)
                goto vsp_01a;
            r.al--;
            goto vsp_01;
        vsp_01a:;
            r.al = pw.partWk[r.di].volume;
        vsp_01:;
            r.dl = r.al;
            //;------------------------------------------------------------------------------
            //;	音量down計算
            //;------------------------------------------------------------------------------
            r.al = pw.ssg_voldown;
            if (r.al == 0)
                goto psg_fade_calc;
            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	Fadeout計算
        //;------------------------------------------------------------------------------
        psg_fade_calc:;
            r.al = pw.fadeout_volume;
            if (r.al == 0)
                goto psg_env_calc;
            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	ENVELOPE 計算
        //;------------------------------------------------------------------------------
        psg_env_calc:;
            if (r.dl == 0)//; 音量0?
                goto pv_out;
            if (pw.partWk[r.di].envf != 0xff)//-1
                goto normal_pvset;
            r.al = r.dl;//; 拡張版 音量 = dl * (eenv_vol + 1) / 16
            r.dl = pw.partWk[r.di].eenv_volume;
            if (r.dl == 0)
                goto pv_min;
            r.dl++;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.al;
            r.dl >>= 3;
            r.carry = ((r.dl % 2) != 0);
            r.dl >>= 1;
            if (!r.carry) goto pv1;
            r.dl++;
            goto pv1;
        normal_pvset:;
            r.dl += pw.partWk[r.di].eenv_volume;//.penv;
            if ((r.dl & 0x80) == 0)
                goto pv0;
            pv_min:;
            r.dl = 0;
        pv0:;
            if (r.dl == 0) goto pv_out;//;0になったら音量LFOは掛けない
            if (r.dl < 16)
                goto pv1;
            r.dl = 15;
        //;------------------------------------------------------------------------------
        //;	音量LFO計算
        //;------------------------------------------------------------------------------
        pv1:;
            if ((pw.partWk[r.di].lfoswi & 0x22) == 0)
                goto pv_out;
            r.ax = 0;
            if ((pw.partWk[r.di].lfoswi & 0x2) == 0)
                goto pv_nolfo1;
            r.ax = pw.partWk[r.di].lfodat;
        pv_nolfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x20) == 0)
                goto pv_nolfo2;
            r.ax += pw.partWk[r.di]._lfodat;
        pv_nolfo2:;
            r.dh = 0;
            r.dx += r.ax;
            if ((r.dx & 0x8000) == 0)
                goto pv10;
            r.dl = 0;
            goto pv_out;
        pv10:;
            if (r.dx < 16)
                goto pv_out;
            r.dl = 15;
        //;------------------------------------------------------------------------------
        //;	出力
        //;------------------------------------------------------------------------------
        pv_out:;
            r.dh = pw.partb;
            r.dh += 8 - 1;
            //Console.WriteLine("{0} {1}", r.dh, r.dl);
            opnset44();
        }



        //4995-5035
        //;==============================================================================
        //;	ＦＭ ＫＥＹＯＮ
        //;==============================================================================
        private void keyon()
        {
            if (pw.partWk[r.di].onkai != 0xff) //-1
                goto ko1;
            //keyon_ret:;
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        ko1:;
            r.dh = 0x28;
            r.dl = pw.partb;
            r.dl--;
            r.bh = 0;
            r.bl = r.dl;
            if (pw.board2 != 0)
            {
                if (pw.fmsel != r.bh)//;0
                    goto ura_keyon;
            }

            r.bx += 0;//offset omote_key1
            r.al = pw.fmKeyOnDataTbl[r.bx];
            r.al |= pw.partWk[r.di].slotmask;
            if (pw.partWk[r.di].sdelay_c == 0)
                goto no_sdm;
            r.al &= pw.partWk[r.di].sdelay_m;
        no_sdm:;
            pw.fmKeyOnDataTbl[r.bx] = r.al;
            r.dl |= r.al;
            opnset44();
            return;

        ura_keyon:;
            if (pw.board2 != 0)
            {
                r.bx += 3;//offset ura_key1
                r.al = pw.fmKeyOnDataTbl[r.bx];
                r.al |= pw.partWk[r.di].slotmask;
                if (pw.partWk[r.di].sdelay_c == 0)
                    goto no_sdm2;
                r.al &= pw.partWk[r.di].sdelay_m;
            no_sdm2:;
                pw.fmKeyOnDataTbl[r.bx] = r.al;
                r.dl |= r.al;
                r.dl |= 0b0000_0100;//;Ura Port
                opnset44();
                return;
            }
        }



        //5036-5068
        //;==============================================================================
        //;	ＰＳＧ ＫＥＹＯＮ
        //;==============================================================================
        private void keyonp()
        {
            if (pw.partWk[r.di].onkai != 0xff)//-1
                goto ko1p;
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        ko1p:;
            //    pushf
            //    cli
            psgmsk();//; AL=07h AH = Maskdata
            r.al |= r.ah;
            r.ah &= pw.partWk[r.di].psgpat;
            r.ah = (byte)~r.ah;
            r.al &= r.ah;
            r.dh = 7;
            r.dl = r.al;
            opnset44();
            //    popf
            //	;
            //	; PSG ﾉｲｽﾞ ｼｭｳﾊｽｳ ﾉ ｾｯﾄ
            //	;
            r.dl = pw.psnoi;
            if (r.dl == pw.psnoi_last)
                goto psnoi_ret;// ; 同じなら定義しない
            if ((pw.psgefcnum & 0x80) == 0)
                goto psnoi_ret;//;PSG効果音発音中は変更しない
            r.dh = 6;
            opnset44();
            pw.psnoi_last = r.dl;
        psnoi_ret:;
            return;
        }



        //5069-5086
        //;==============================================================================
        //;	ＰＳＧ07hポートのKEYON/OFF準備(07Hを読み、マスクする値を算出)
        //; OUTPUT...al<- 07h Read Data
        //;			   ah<- Mask Data
        //;==============================================================================
        private void psgmsk()
        {
            r.cl = pw.partb;
            r.al = 0;
            r.carry = true;
            int i = r.cl;
            while (i != 0)
            {
                bool bc = (r.al & 0x80) != 0;
                r.al <<= 1;
                r.al |= (byte)((r.carry) ? 1 : 0);
                r.carry = bc;
                i--;
            }
            r.ah = r.al;
            r.al <<= 3;
            r.ah |= r.al;
            get07();
        }



        //5087-5137
        //;==============================================================================
        //;	KEY OFF
        //; don't Break AL
        //;==============================================================================
        private void keyoff()
        {
            if (pw.partWk[r.di].onkai != 0xff)
            {
                kof1();
                return;
            }
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        }

        private void kof1()
        { 
            r.dh = 0x28;
            r.dl = pw.partb;
            r.dl--;

            r.bh = 0;
            r.bl = r.dl;
            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto ura_keyoff;
            }

            r.bx += 0;//offset omote_key1 KUMA: fmKeyOnDataTblへの位置

            r.cl = pw.partWk[r.di].slotmask;
            r.cl = (byte)~r.cl;
            r.cl &= pw.fmKeyOnDataTbl[r.bx];

            pw.fmKeyOnDataTbl[r.bx] = r.cl;
            r.dl |= r.cl;
            opnset44();
            return;

        ura_keyoff:;
            if (pw.board2 != 0)
            {
                r.bx += 3;//offset ura_key1 KUMA: fmKeyOnDataTblへの位置(裏は+3)

                r.cl = pw.partWk[r.di].slotmask;
                r.cl = (byte)~r.cl;
                r.cl &= pw.fmKeyOnDataTbl[r.bx];

                pw.fmKeyOnDataTbl[r.bx] = r.cl;
                r.dl |= r.cl;
                r.dl |= 0b0100;//;FM Ura Port
                opnset44();
                return;
            }
        }

        public void keyoffp()
        {
            if (pw.partWk[r.di].onkai != 0xff)
            {
                kofp1();
                return;
            }
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        }

        private void kofp1()
        { 
            if (pw.partWk[r.di].envf == 0xff)
                goto kofp1_ext;
            pw.partWk[r.di].envf = 2;
            return;

        kofp1_ext:;
            pw.partWk[r.di].eenv_count = 4;
            return;
        }



        //5138-5152
        //;==============================================================================
        //;	音色の設定
        //;		INPUTS	-- [PARTB]			
        //;			-- dl[TONE_NUMBER]
        //;			-- di[PART_DATA_ADDRESS]
        //;==============================================================================
        private void neiroset()
        {
            toneadr_calc();
            silence_fmpart();
            if (!r.carry)
            {
                neiroset_main();
                return;
            }
            //; neiromask=0の時(TLのworkのみ設定)
            r.bx += 4;
            neiroset_tl();
            return;
        }



        //5153-5196
        //;==============================================================================
        //;	音色設定メイン
        //;==============================================================================
        //;------------------------------------------------------------------------------
        //;	AL/FBを設定
        //;------------------------------------------------------------------------------
        private void neiroset_main()
        {
            r.dh = 0xb0 - 1;
            r.dh += pw.partb;
            if (pw.inst != null && r.bx + 24 < pw.inst.Length) r.dl = (byte)pw.inst[r.bx + 24].dat;
            else r.dl = 0;

            if (pw.af_check == 0)//;ALG/FBは設定しないmodeか？
                goto no_af;

            r.dl = pw.partWk[r.di].alg_fb;

        no_af:;
            if (pw.partb != 3)
                goto nss_notfm3;

            if (pw.board2 != 0)
            {
                if (pw.fmsel != 0)
                    goto nss_notfm3;
            }
            else
            {
                if (r.di == pw.part_e)
                    goto nss_notfm3;
            }

            if (pw.af_check == 0)//;ALG/FBは設定しないmodeか？
                goto set_fm3_alg_fb;

            r.dl = pw.fm3_alg_fb;
            goto nss_notfm3;

        set_fm3_alg_fb:;
            if ((pw.partWk[r.di].slotmask & 0x10) != 0)//;slot1を使用していなければ
                goto nss_notslot1;

            r.al = pw.fm3_alg_fb;
            r.al &= 0b0011_1000;//;fbは前の値を使用
            r.dl &= 0b0000_0111;
            r.dl |= r.al;

        nss_notslot1:;
            pw.fm3_alg_fb = r.dl;

        nss_notfm3:;
            opnset();
            pw.partWk[r.di].alg_fb = r.dl;
            r.dl &= 7;//; dl=algo

            check_carrier();
        }



        //5197-5220
        //;------------------------------------------------------------------------------
        //;	Carrierの位置を調べる(VolMaskにも設定)
        //;------------------------------------------------------------------------------
        private void check_carrier()
        {
            r.stack.Push(r.bx);
            r.bh = 0;
            r.bl = r.dl;
            r.bx += 0;//offset carrier_table
            r.al = pw.carrier_table[r.bx];
            if ((pw.partWk[r.di].volmask & 0xf) != 0)
                goto not_set_volmask;//; Volmask値が0以外の場合は設定しない
            pw.partWk[r.di].volmask = r.al;

        not_set_volmask:;
            if ((pw.partWk[r.di]._volmask & 0xf) != 0)
                goto not_set_volmask2;
            pw.partWk[r.di]._volmask = r.al;

        not_set_volmask2:;
            pw.partWk[r.di].carrier = r.al;
            r.ah = pw.carrier_table[r.bx + 8];//; slot2/3の逆転データ(not済み)
            r.bx = r.stack.Pop();
            r.al = pw.partWk[r.di].neiromask;
            r.ah &= r.al;//; AH=TL用のmask / AL=その他用のmask

            //; ------------------------------------------------------------------------------
            //; 各音色パラメータを設定(TLはモジュレータのみ)
            //; ------------------------------------------------------------------------------
            r.dh = 0x30 - 1;
            r.dh += pw.partb;
            r.cx = 4;//; DT / ML
        ns01:;
            if (pw.inst != null && r.bx < pw.inst.Length) r.dl = (byte)pw.inst[r.bx ].dat;
            else r.dl = 0;
            r.bx++;
            r.carry = ((r.al & 0x80) != 0);
            r.al = (byte)((r.al << 1) | ((r.al & 0x80) >> 7));
            if (!r.carry) goto ns_ns;
            opnset();
        ns_ns:;
            r.dh += 4;
            r.cx--;
            if (r.cx != 0) goto ns01;
            r.cx = 4;//; TL
        ns01b:;
            if (pw.inst != null && r.bx < pw.inst.Length) r.dl = (byte)pw.inst[r.bx].dat;
            else r.dl = 0;
            r.bx++;
            r.carry = ((r.al & 0x80) != 0);
            r.al = (byte)((r.al << 1) | ((r.al & 0x80) >> 7));
            if (!r.carry) goto ns_nsb;
            opnset();

        ns_nsb:;
            r.dh += 4;
            r.cx--;
            if (r.cx != 0) goto ns01b;

            r.cx = 16;//; 残り
        ns01c:;
            if (pw.inst != null && r.bx < pw.inst.Length) r.dl = (byte)pw.inst[r.bx].dat;
            else r.dl = 0;
            r.bx++;
            r.carry = ((r.al & 0x80) != 0);
            r.al = (byte)((r.al << 1) | ((r.al & 0x80) >> 7));
            if (!r.carry) goto ns_nsc;
            opnset();

        ns_nsc:;
            r.dh += 4;
            r.cx--;
            if (r.cx != 0) goto ns01c;

            //; ------------------------------------------------------------------------------
            //; SLOT毎のTLをワークに保存
            //; ------------------------------------------------------------------------------
            r.bx -= 20;
            neiroset_tl();
        }



        //5258-5268
        private void neiroset_tl()
        {
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            
            r.si = r.bx;
            //r.di += pw.slot1;

            if (pw.inst != null && r.si + 3 < pw.inst.Length)
            {
                pw.partWk[r.di].slot1 = (byte)pw.inst[r.si + 0].dat;
                pw.partWk[r.di].slot3 = (byte)pw.inst[r.si + 1].dat;
                pw.partWk[r.di].slot2 = (byte)pw.inst[r.si + 2].dat;
                pw.partWk[r.di].slot4 = (byte)pw.inst[r.si + 3].dat;
            }

            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            return;
        }



        //5269-5302
        //;==============================================================================
        //;	TONE DATA START ADDRESS を計算
        //;		input dl  tone_number
        //;		output bx  address
        //;==============================================================================
        private void toneadr_calc()
        {
            if (pw.prg_flg != 0)
                goto prgdat_get;

            if (r.di == pw.part_e)
                goto prgdat_get;

            r.bx = (ushort)pw.tondat;

            r.al = r.dl;
            r.ah = 0;
            r.ax += r.ax;
            r.ax += r.ax;
            r.ax += r.ax;
            r.ax += r.ax;
            r.ax += r.ax;
            r.bx += r.ax;
            return;

        prgdat_get:;
            r.bx = (ushort)pw.prgdat_adr;
            if (r.di != pw.part_e)
                goto gpd_loop;

            r.bx = pw.prgdat_adr2;//;FM効果音の場合

        gpd_loop:;
            pw.inst = pw.md;
            if (r.bx >= pw.inst.Length)
            {
                throw new PmdException("お探しの音色番号は見つかりませんでした。");
            }
            if (pw.inst[r.bx].dat == r.dl)
                goto gpd_exit;
            r.bx += 26;
            goto gpd_loop;

        gpd_exit:;
            r.bx++;
            return;
        }



        //5303-5335
        //;==============================================================================
        //;	[PartB]
        //        のパートの音を完璧に消す(TL= 127 and RR = 15 and KEY-OFF)
        //; cy=1 ･･･ 全スロットneiromaskされている
        //;==============================================================================
        private void silence_fmpart()
        {
            r.al = pw.partWk[r.di].neiromask;
            if (r.al == 0)
                goto sfm_exit;

            r.stack.Push(r.dx);
            r.dh = pw.partb;
            r.dh += 0x40 - 1;
            r.cx = 4;
            r.dl = 127;//; TL = 127 / RR=15
        ns00c:;
            r.carry = ((r.al & 0x80) != 0);
            r.al = (byte)((r.al << 1) | ((r.al & 0x80) >> 7));
            if (!r.carry) goto ns00d;
            opnset();
            r.dh += 0x40;
            opnset();
            r.dh -= 0x40;
        ns00d:;
            r.dh += 4;
            r.cx--;
            if (r.cx != 0) goto ns00c;

            r.stack.Push(r.bx);
            kof1();//; KEY OFF
            r.bx = r.stack.Pop();

            r.dx = r.stack.Pop();//    pop dx
            r.carry = false;
            return;

        sfm_exit:;
            r.carry = true;
            return;
        }



        //5336-5495
        //;==============================================================================
        //;	ＬＦＯ処理
        //;		Don't Break cl
        //;		output cy = 1    変化があった
        //;==============================================================================
        public void lfo()
        {
        //lfop:;
            if (pw.partWk[r.di].delay == 0)
                goto lfo1;
            pw.partWk[r.di].delay--;//; cy=0

        lfo_ret:;
            return;

        lfo1:;
            if ((pw.partWk[r.di].extendmode & 2) == 0) //; TimerAと合わせるか？
                goto lfo_normal;//; そうじゃないなら無条件にlfo処理
            r.ch = pw.TimerAtime;
            r.ch -= pw.lastTimerAtime;
            if (r.ch == 0)
                goto lfo_ret;// 前回の値と同じなら何もしない cy = 0

            r.ax = pw.partWk[r.di].lfodat;
            r.stack.Push(r.ax);

        lfo_loop:;
            lfo_main();
            r.ch--;
            if (r.ch != 0)
                goto lfo_loop;

            goto lfo_check;

        lfo_normal:;
            r.ax = pw.partWk[r.di].lfodat;
            r.stack.Push(r.ax);
            lfo_main();

        lfo_check:;
            r.ax = r.stack.Pop();

            if (r.ax != pw.partWk[r.di].lfodat)
                goto lfo_stc_ret;
            return;//;c=0

        lfo_stc_ret:;
            r.carry = true;
            return;
        }

        private void lfop()
        {
            lfo();
        }

        private void lfo_main()
        {
            if (pw.partWk[r.di].speed == 1)
                goto lfo2;
            if (pw.partWk[r.di].speed == 0xff)//-1
                goto lfom_ret;
            pw.partWk[r.di].speed--;

        lfom_ret:;
            return;

        lfo2:;
            r.al = pw.partWk[r.di].speed2;
            pw.partWk[r.di].speed = r.al;
            r.bl = pw.partWk[r.di].lfo_wave;
            if (r.bl == 0)
                goto lfo_sankaku;
            if (r.bl == 4)
                goto lfo_sankaku;
            if (r.bl == 2)
                goto lfo_kukei;
            if (r.bl == 6)
                goto lfo_oneshot;
            if (r.bl != 5)
                goto not_sankaku;
            //; 三角波 lfowave = 0,4,5
            r.al = pw.partWk[r.di].step;
            r.ah = r.al;
            if ((r.ah & 0x80) == 0)
                goto lfo2ns;
            r.ah = (byte)-r.ah;

        lfo2ns:;
            r.ax = (ushort)((sbyte)r.al * r.ah); //; lfowave=5の場合 1step = step×｜step｜
            goto lfo20;

        lfo_sankaku:;
            r.al = pw.partWk[r.di].step;
            r.ax = (ushort)(sbyte)r.al;// cbw

        lfo20:;
            pw.partWk[r.di].lfodat += r.ax;
            if (pw.partWk[r.di].lfodat != 0)
                goto lfo21;
            md_inc();

        lfo21:;
            r.al = pw.partWk[r.di].time;
            if (r.al == 255)
                goto lfo3;
            r.al--;
            if (r.al != 0)
                goto lfo3;
            r.al = pw.partWk[r.di].time2;
            if (r.bl == 4)
                goto lfo22;
            r.al += r.al;//; lfowave=0,5の場合 timeを反転時２倍にする

        lfo22:;
            pw.partWk[r.di].time = r.al;
            r.al = pw.partWk[r.di].step;
            r.al = (byte)-r.al;
            pw.partWk[r.di].step = r.al;
            return;

        lfo3:;
            pw.partWk[r.di].time = r.al;
            return;

        not_sankaku:;
            r.bl--;
            if (r.bl != 0)
                goto not_nokogiri;
            //; ノコギリ波 lfowave = 1,6
            r.al = pw.partWk[r.di].step;
            r.ax = (ushort)(sbyte)r.al;// cbw
            pw.partWk[r.di].lfodat += r.ax;
            r.al = pw.partWk[r.di].time;
            if (r.al == 0xff)//-1
                goto nk_lfo3;
            r.al--;
            if (r.al != 0) goto nk_lfo3;
            pw.partWk[r.di].lfodat = (ushort)-pw.partWk[r.di].lfodat;
            md_inc();

            r.al = pw.partWk[r.di].time2;
            r.al += r.al;

        nk_lfo3:;
            pw.partWk[r.di].time = r.al;
            return;

        lfo_oneshot:;
            //; ワンショット lfowave = 6
            r.al = pw.partWk[r.di].time;
            if (r.al == 0) 
                goto lfoone_ret;
            if (r.al == 0xff)//-1
                goto lfoone_nodec;
            r.al--;
            pw.partWk[r.di].time = r.al;

        lfoone_nodec:;
            r.al = pw.partWk[r.di].step;
            r.ax = (ushort)(sbyte)r.al;// cbw
            pw.partWk[r.di].lfodat += r.ax;

        lfoone_ret:;
            return;

        lfo_kukei:;
            //; 矩形波 lfowave = 2
            r.al = pw.partWk[r.di].step;
            r.ax = (ushort)((sbyte)r.al * (sbyte)pw.partWk[r.di].time);
            pw.partWk[r.di].lfodat = r.ax;
            md_inc();
            pw.partWk[r.di].step = (byte)-pw.partWk[r.di].step;
            return;

        not_nokogiri:;
            //; ランダム波 lfowave = 3
            r.al = pw.partWk[r.di].step;
            if ((r.al & 0x80) == 0)
                goto ns_plus;
            r.al = (byte)-r.al;

        ns_plus:;
            r.ax = (ushort)(r.al * pw.partWk[r.di].time);
            r.stack.Push(r.ax);
            r.stack.Push(r.cx);
            r.ax += r.ax;
            rnd();
            r.cx = r.stack.Pop();
            r.bx = r.stack.Pop();
            r.ax -= r.bx;
            pw.partWk[r.di].lfodat = r.ax;

            md_inc();
        }



        //5496-5543
        //;==============================================================================
        //;	MDコマンドの値によってSTEP値を変更
        //;==============================================================================
        private void md_inc()
        {
            pw.partWk[r.di].mdspd--;
            if (pw.partWk[r.di].mdspd != 0)
                goto md_exit;
            r.al = pw.partWk[r.di].mdspd2;
            pw.partWk[r.di].mdspd = r.al;
            r.al = pw.partWk[r.di].mdc;
            if(r.al==0)
                goto md_exit;//; count =0
            if((r.al&0x80)!=0)
                goto mdi21;// count > 127 (255)
            r.al--;
            pw.partWk[r.di].mdc = r.al;

        mdi21:;
            r.al = pw.partWk[r.di].step;
            if ((r.al & 0x80) == 0)
                goto mdi22;
            r.al = (byte)-r.al;
            r.al += pw.partWk[r.di].mdepth;
            if ((r.al & 0x80) != 0)
                goto mdi21_ov;
            r.al = (byte)-r.al;

        mdi21_s:;
            pw.partWk[r.di].step = r.al;

        md_exit:;
            return;

        mdi21_ov:;
            r.al = 0;
            if ((pw.partWk[r.di].mdepth & 0x80)!=0)
                goto mdi21_s;
            r.al = 0x81;// -127;
            goto mdi21_s;

        mdi22:;
            r.al += pw.partWk[r.di].mdepth;
            if ((r.al & 0x80) != 0)
                goto mdi22_ov;

            mdi22_s:;
            pw.partWk[r.di].step = r.al;
            return;

        mdi22_ov:;
            r.al = 0;
            if ((pw.partWk[r.di].mdepth & 0x80) != 0)
                goto mdi22_s;
            r.al = 0x7f;
            goto mdi22_s;
        }



        //5544-5561
        //;==============================================================================
        //;	乱数発生ルーチン INPUT : AX=MAX_RANDOM
        //;				OUTPUT: AX=RANDOM_NUMBER
        //;==============================================================================
        private void rnd()
        {
            r.cx = r.ax;
            r.ax = 259;

            r.ax = (ushort)(r.ax * pw.seed);
            r.ax += 3;
            r.ax &= 32767;//0x7fff

            pw.seed = r.ax;
            int ans = (r.ax * r.cx);
            r.cx = 32767;
            r.ax = (ushort)(ans / r.cx);
            r.dx = (ushort)(ans % r.cx);

            return;
        }



        //5562-5589
        //;==============================================================================
        //;	ＬＦＯとＰＳＧ／ＰＣＭのソフトウエアエンベロープの初期化
        //;==============================================================================
        //;==============================================================================
        //;	ＰＳＧ／ＰＣＭ音源用 Entry
        //;==============================================================================
        public void lfoinitp()
        {
            r.ah = r.al;//; ｷｭｰﾌ ﾉ ﾄｷ ﾊ INIT ｼﾅｲﾖ
            r.ah &= 0xf;
            if (r.ah != 0xc)
                goto lip_00;
            r.al = pw.partWk[r.di].onkai_def;
            r.ah = r.al;
            r.ah &= 0xf;
        lip_00:;
            pw.partWk[r.di].onkai_def = r.al;

            if (r.ah == 0xf)            //;	4.8r 修正
                goto lfo_exitp;
            pw.partWk[r.di].porta_num = 0;//;ポルタメントは初期化

            if ((pw.tieflag & 1) == 0)//; ﾏｴ ｶﾞ & ﾉ ﾄｷ ﾓ INIT ｼﾅｲ｡
            {
                seinit();
                return;
            }

        lfo_exitp:;
            r.stack.Push(r.ax);
            soft_env();//; 前が & の場合 -> 1回 SoftEnv処理
            r.ax = r.stack.Pop();

            lfo_exit();
            //; ここまで
        }



        //5590-5641
        //;==============================================================================
        //;	ソフトウエアエンベロープ初期化
        //;==============================================================================
        private void seinit()
        {
            if (pw.partWk[r.di].envf == 0xff)
                goto extenv_init;

            pw.partWk[r.di].envf = 0;
            pw.partWk[r.di].eenv_volume = 0;//.penv

            r.ah = pw.partWk[r.di].eenv_arc;//.patb
            pw.partWk[r.di].eenv_ar = r.ah;//.pat
            if (r.ah != 0)
                goto lfin2;
            pw.partWk[r.di].envf = 1;//; ATTACK=0 ... ｽｸﾞ Decay ﾆ
            r.ah = pw.partWk[r.di].eenv_dr;//.pv2
            pw.partWk[r.di].eenv_volume = r.ah;//.penv

        lfin2:;
            r.ah = pw.partWk[r.di].eenv_src;//.pr1b
            pw.partWk[r.di].eenv_sr = r.ah;//.pr1
            r.ah = pw.partWk[r.di].eenv_rrc;//.pr2b
            pw.partWk[r.di].eenv_rr = r.ah;//.pr2
            lfin1();
            return;
        //; 拡張ssg_envelope用
        extenv_init:;
            r.ah = pw.partWk[r.di].eenv_ar;
            r.ah -= 16;
            pw.partWk[r.di].eenv_arc = r.ah;
            r.ah = pw.partWk[r.di].eenv_dr;
            r.ah -= 16;
            if ((r.ah & 0x80) == 0)
                goto eei_dr_notx;
            r.ah += r.ah;
        eei_dr_notx:;
            pw.partWk[r.di].eenv_drc = r.ah;

            r.ah = pw.partWk[r.di].eenv_sr;
            r.ah -= 16;
            if ((r.ah & 0x80) == 0)
                goto eei_sr_notx;
            r.ah += r.ah;
        eei_sr_notx:;
            pw.partWk[r.di].eenv_src = r.ah;

            r.ah = pw.partWk[r.di].eenv_rr;
            r.ah += r.ah;
            r.ah -= 16;
            pw.partWk[r.di].eenv_rrc = r.ah;

            r.ah = pw.partWk[r.di].eenv_al;
            pw.partWk[r.di].eenv_volume = r.ah;
            pw.partWk[r.di].eenv_count = 1;

            r.stack.Push(r.ax);
            ext_ssgenv_main();//; 最初の１回
            r.ax = r.stack.Pop();

            lfin1();
        }



        //5642-5680
        //;==============================================================================
        //;	ＦＭ音源用 Entry
        //;==============================================================================
        private void lfoinit()
        {
            r.ah = r.al;//; ｷｭｰﾌ ﾉ ﾄｷ ﾊ INIT ｼﾅｲﾖ
            r.ah &= 0xf;
            if (r.ah != 0xc)
                goto li_00;
            r.al = pw.partWk[r.di].onkai_def;
            r.ah = r.al;
            r.ah &= 0xf;
        li_00:;
            pw.partWk[r.di].onkai_def = r.al;

            if (r.ah == 0xf)
            {
                lfo_exit();
                return;
            }
            pw.partWk[r.di].porta_num = 0;//;ポルタメントは初期化

            if ((pw.tieflag & 1) == 0)
            {//; ﾏｴ ｶﾞ & ﾉ ﾄｷ ﾓ INIT ｼﾅｲ｡
                lfin1();
                return;
            }
            lfo_exit();
        }

        private void lfo_exit()
        { 
            if ((pw.partWk[r.di].lfoswi & 3) == 0)//; LFO使用中か？
                goto le_no_one_lfo1;// ; 前が & の場合 -> 1回 LFO処理

            r.stack.Push(r.ax);
            lfo();
            r.ax = r.stack.Pop();

        le_no_one_lfo1:;
            if((pw.partWk[r.di].lfoswi&0x30)==0)//; LFO使用中か？
            goto le_no_one_lfo2;//; 前が & の場合 -> 1回 LFO処理

            r.stack.Push(r.ax);
            //    pushf
            //    cli
            lfo_change();
            lfo();
            lfo_change();
            //    popf
            r.ax = r.stack.Pop();

        le_no_one_lfo2:;
            return;
        }



        //5681-5757
        //;==============================================================================
        //;	ＬＦＯ初期化
        //;==============================================================================
        private void lfin1()
        {
            if (pw.board2 != 0)
            {
                r.ah = pw.partWk[r.di].hldelay;
                pw.partWk[r.di].hldelay_c = r.ah;
                if (r.ah == 0)
                    goto non_hldelay;
                r.dh = pw.partb;//    mov dh,[partb]
                r.dh += 0xb4 - 1;
                r.dl = pw.partWk[r.di].fmpan;
                r.dl &= 0xc0;//;HLFO = OFF
                opnset();
            non_hldelay:;
            }

            r.ah = pw.partWk[r.di].sdelay;
            pw.partWk[r.di].sdelay_c = r.ah;
            r.cl = pw.partWk[r.di].lfoswi;
            if ((r.cl & 3) == 0)
                goto li_lfo1_exit;//; LFOは未使用
            if ((r.cl & 4) != 0)//;keyon非同期か?
                goto li_lfo1_next;

            lfoinit_main();

        li_lfo1_next:;
            r.stack.Push(r.ax);
            lfo();
            r.ax = r.stack.Pop();

        li_lfo1_exit:;
            if ((r.cl & 0x30) == 0)
                goto li_lfo2_exit;//; LFOは未使用
            if ((r.cl & 0x40) != 0)//;keyon非同期か?
                goto li_lfo2_next;

            r.stack.Push(r.ax);
            // pushf
            //    cli
            lfo_change();
            lfoinit_main();
            lfo_change();
            //    popf
            r.ax = r.stack.Pop();

        li_lfo2_next:;
            r.stack.Push(r.ax);
            // pushf
            //    cli
            lfo_change();
            lfo();
            lfo_change();
            //    popf
            r.ax = r.stack.Pop();

        li_lfo2_exit:;
            //    ret
        }

        private Func<object> lfoinit_main()
        {
            pw.partWk[r.di].lfodat = 0;
            r.dl = pw.partWk[r.di].delay2;//    mov dx, word ptr delay2[di]
            r.dh = pw.partWk[r.di].speed2;
            pw.partWk[r.di].delay = r.dl;
            pw.partWk[r.di].speed = r.dh;
            r.dl = pw.partWk[r.di].step2;//    mov dx, word ptr step2[di]
            r.dh = pw.partWk[r.di].time2;
            pw.partWk[r.di].step = r.dl;
            pw.partWk[r.di].time = r.dh;

            r.dl = pw.partWk[r.di].mdc2;
            pw.partWk[r.di].mdc = r.dl;

            if (pw.partWk[r.di].lfo_wave == 2)//; 矩形波または
                goto lim_first;
            if (pw.partWk[r.di].lfo_wave != 3)//;ランダム波の場合は
                goto lim_nofirst;
            lim_first:;
            pw.partWk[r.di].speed = 1;//; delay直後にLFOが掛かるようにする
            return null;
        lim_nofirst:;
            pw.partWk[r.di].speed++;//; それ以外の場合はdelay直後のspeed値を +1
            return null;
        }



        //5758-5961
        //;==============================================================================
        //;	ＰＳＧ／ＰＣＭのソフトウエアエンベロープ
        //;==============================================================================
        public void soft_env()
        {
            if ((pw.partWk[r.di].extendmode & 4) == 0)//; TimerAと合わせるか？
            {
                soft_env_main();// ; そうじゃないなら無条件にsenv処理
                return;
            }

            r.ch = pw.TimerAtime;
            r.ch -= pw.lastTimerAtime;
            if (r.ch == 0)
                goto senv_ret;//; 前回の値と同じなら何もしない cy = 0
            r.cl = 0;

        senv_loop:;
            soft_env_main();
            if (!r.carry) goto sel00;
            r.cl = 1;
        sel00:;
            r.ch--;
            if (r.ch != 0) goto senv_loop;
            r.cl = r.ror(r.cl, 1);//; cy setting
        senv_ret:;
            return;
        }

        private void soft_env_main()
        {
            if (pw.partWk[r.di].envf == 0xff)//-1
            {
                ext_ssgenv_main();
                return;
            }

            r.dl = pw.partWk[r.di].eenv_volume;//.penv;
            soft_env_sub();
            r.carry = false;
            if (r.dl == pw.partWk[r.di].eenv_volume)//.penv
                goto sem_ret;//; cy=0
            r.carry = true;

        sem_ret:;
            return;
        }

        private void soft_env_sub()
        {
            if (pw.partWk[r.di].envf != 0)//-1
                goto se1;

            //;
            //; Attack
            //;
            pw.partWk[r.di].eenv_ar--;//.pat--;
            if (pw.partWk[r.di].eenv_ar != 0)
                goto se2;

            pw.partWk[r.di].envf = 1;
            r.al = pw.partWk[r.di].eenv_dr;//pv2[di]
            pw.partWk[r.di].eenv_volume = r.al;//penv[di]
            r.carry = true;
            return;

        se1:;
            if (pw.partWk[r.di].envf == 2)
                goto se3;

            //;
            //; Decay
            //;
            if (pw.partWk[r.di].eenv_sr == 0)
                goto se2; //ＤＲ＝０の時は減衰しない
            pw.partWk[r.di].eenv_sr--;
            if (pw.partWk[r.di].eenv_sr != 0)
                goto se2;

            r.al = pw.partWk[r.di].eenv_src;//pr1b[di]
            pw.partWk[r.di].eenv_sr = r.al;//pr1[di]
            pw.partWk[r.di].eenv_volume--;//penv[di]

        se4:;
            if (pw.partWk[r.di].eenv_volume >= 0xf1)//-15
                goto se2;
            if (pw.partWk[r.di].eenv_volume < 15)
                goto se2;
            se5:;
            pw.partWk[r.di].eenv_volume = 0xf1;// mov penv[di],-15
        se2:;
            return;

        //;
        //; Release
        //;
        se3:;
            if (pw.partWk[r.di].eenv_rr == 0)//pr2
                goto se5;//; ＲＲ＝０の時はすぐに音消し
            pw.partWk[r.di].eenv_rr--;//pr2[di]
            if (pw.partWk[r.di].eenv_rr != 0)
                goto se2;
            r.al = pw.partWk[r.di].eenv_rrc;//pr2b[di]
            pw.partWk[r.di].eenv_rr = r.al;//pr2[di]
            pw.partWk[r.di].eenv_volume--;//penv[di]
            goto se4;
        }

        //;	拡張版
        private void ext_ssgenv_main()
        {
            r.ah = pw.partWk[r.di].eenv_count;
            if (r.ah != 0)
                goto esm_main2;
            esm_ret:;
            r.carry = false;
            return;//;cy=0
        esm_main2:;
            r.dl = pw.partWk[r.di].eenv_volume;
            esm_sub();
            if (r.dl == pw.partWk[r.di].eenv_volume)
            {
                r.carry = false;
                goto esm_ret;// cy=0
            }
            r.carry = true;
            return;
        }

        private void esm_sub()
        {
        //esm_ar_check:;
            r.ah--;
            if (r.ah != 0)
                goto esm_dr_check;
            //;
            //;	[[[Attack Rate]]]
            //;
            r.al = pw.partWk[r.di].eenv_arc;
            r.al--;
            if ((r.al & 0x80) != 0)
                goto arc_count_check;//;0以下の場合はカウントCHECK
            r.al++;
            pw.partWk[r.di].eenv_volume += r.al;
            if (pw.partWk[r.di].eenv_volume >= 15)
                goto esm_ar_next;
            r.ah = pw.partWk[r.di].eenv_ar;
            r.ah -= 16;
            pw.partWk[r.di].eenv_arc = r.ah;
            return;
        esm_ar_next:;
            pw.partWk[r.di].eenv_volume = 15;
            pw.partWk[r.di].eenv_count++;
            if (pw.partWk[r.di].eenv_sl != 15)//; SL=0の場合はすぐSRに
                return;// goto esm_ret;
            pw.partWk[r.di].eenv_count++;
            return;
        arc_count_check:;
            if (pw.partWk[r.di].eenv_ar == 0)//; AR=0?
                return;// goto esm_ret;
            pw.partWk[r.di].eenv_arc++;
            return;
        esm_dr_check:;
            r.ah--;
            if (r.ah != 0)
                goto esm_sr_check;
            //;
            //;	[[[Decay Rate]]]
            //;
            r.al = pw.partWk[r.di].eenv_drc;
            r.al--;
            if ((r.al & 0x80) != 0)
                goto drc_count_check;//;0以下の場合はカウントCHECK
            r.al++;
            r.carry = pw.partWk[r.di].eenv_volume < r.al;
            pw.partWk[r.di].eenv_volume -= r.al;
            r.al = pw.partWk[r.di].eenv_sl;
            if (r.carry)
                goto dr_slset;
            if (pw.partWk[r.di].eenv_volume < r.al)
                goto dr_slset;
            r.ah = pw.partWk[r.di].eenv_dr;
            r.ah -= 16;
            if ((r.ah & 0x80) == 0)
                goto esm_dr_notx;
            r.ah += r.ah;
        esm_dr_notx:;
            pw.partWk[r.di].eenv_drc = r.ah;
            return;
        dr_slset:;
            pw.partWk[r.di].eenv_volume = r.al;
            pw.partWk[r.di].eenv_count++;
            return;
        drc_count_check:;
            if (pw.partWk[r.di].eenv_dr == 0)//; DR=0?
                return;// goto esm_ret;
            pw.partWk[r.di].eenv_drc++;
            return;
        esm_sr_check:;
            r.ah--;
            if (r.ah != 0)
                goto esm_rr;
            //;
            //;	[[[Sustain Rate]]]
            //;
            r.al = pw.partWk[r.di].eenv_src;
            r.al--;
            if ((r.al & 0x80) != 0)
                goto src_count_check;//;0以下の場合はカウントCHECK
            r.al++;
            r.carry = pw.partWk[r.di].eenv_volume < r.al;
            pw.partWk[r.di].eenv_volume -= r.al;
            if (!r.carry)
                goto esm_sr_exit;
            pw.partWk[r.di].eenv_volume = 0;
        esm_sr_exit:;
            r.ah = pw.partWk[r.di].eenv_sr;
            r.ah -= 16;
            if ((r.ah & 0x80) == 0)
                goto esm_sr_notx;
            r.ah += r.ah;
        esm_sr_notx:;
            pw.partWk[r.di].eenv_src = r.ah;
            return;
        src_count_check:;
            if (pw.partWk[r.di].eenv_sr == 0)//; SR=0?
                return;// goto esm_ret;
            pw.partWk[r.di].eenv_src++;
            return;
        esm_rr:;
            //;
            //;	[[[Release Rate]]]
            //;
            r.al = pw.partWk[r.di].eenv_rrc;
            r.al--;
            if ((r.al & 0x80) != 0)
                goto rrc_count_check;//;0以下の場合はカウントCHECK
            r.al++;
            r.carry = pw.partWk[r.di].eenv_volume < r.al;
            pw.partWk[r.di].eenv_volume -= r.al;
            if (!r.carry)
                goto esm_rr_exit;
            pw.partWk[r.di].eenv_volume = 0;
        esm_rr_exit:;
            r.ah = pw.partWk[r.di].eenv_rr;
            r.ah += r.ah;
            r.ah -= 16;
            pw.partWk[r.di].eenv_rrc = r.ah;
            return;
        rrc_count_check:;
            if (pw.partWk[r.di].eenv_rr == 0)//; RR=0?
                return;// goto esm_ret;
            pw.partWk[r.di].eenv_rrc++;
            return;
        }



        //5961-6000
        //;==============================================================================
        //;	FADE IN / OUT ROUTINE
        //;
        //;		FROM Timer-A
        //;==============================================================================
        private void fadeout()
        {
            if (pw.pause_flag == 1)//;pause中はfadeoutしない
                goto fade_exit;
            r.al = pw.fadeout_speed;
            if (r.al == 0)
                goto fade_exit;
            if ((r.al & 0x80) != 0)
                goto fade_in;

            r.carry = (r.al + pw.fadeout_volume > 0xff);
            r.al += pw.fadeout_volume;
            if (r.carry) goto fadeout_end;

            pw.fadeout_volume = r.al;
            return;

        fadeout_end:;
            pw.fadeout_volume = 255;
            pw.fadeout_speed = 0;
            if (pw.fade_stop_flag != 1)
                goto fade_exit;

            pw.music_flag |= 2;
        fade_exit:;
            return;

        fade_in:;
            r.carry = (r.al + pw.fadeout_volume > 0xff);
            r.al += pw.fadeout_volume;
            if (!r.carry) goto fadein_end;

            pw.fadeout_volume = r.al;
            return;

        fadein_end:;
            pw.fadeout_volume = 0;
            pw.fadeout_speed = 0;
            if (pw.board2 != 0)
            {
                r.dl = pw.rhyvol;
                volset2rf();
            }
            return;
        }


        //6001-6036
        //;==============================================================================
        //;	インタラプト 設定
        //; FM音源専用
        //;==============================================================================
        private void setint()
        {
            //pushf
            //cli; 割り込み禁止
            //;
            //; ＯＰＮ割り込み初期設定
            //;
            pw.tempo_d = 200;//; TIMER B SET
            pw.tempo_d_push = 200;

            calc_tb_tempo();
            settempo_b();

            r.dx = 0x2500;
            opnset44();
            r.dx = 0x2400;//; TIMER A SET(9216μs固定)

            opnset44();// 一番遅くて丁度いい

            r.dh = 0x27;
            r.dl = 0b0011_1111;//; TIMER ENABLE

            opnset44();

            //    popf

            //;
            //;　小節カウンタリセット
            //;
            r.ax = 0;
            pw.opncount = r.al;
            pw.syousetu = r.ax;
            pw.syousetu_lng = 96;
        }



        //6037-6165
        //;==============================================================================
        //;	ALL SILENCE
        //;==============================================================================
        private void silence()
        {
            if (pw.board2 != 0)
            {
                sel44();//mmainには飛ばない状況下なので大丈夫
                r.ah = 2;
            }
            oploop();
        }

        private void oploop()
        { 
            if (pw.fm_effec_flag != 1) goto opi_nef;
            if (pw.board2 != 0)
            {
                if (r.ah != 1) goto opi_nef;
            }

            byte[] bxTbl = pw.fmoff_ef;
            r.bx = 0;//offset fmoff_ef
            goto opi_ef;
        opi_nef:
            bxTbl = pw.fmoff_nef;
            r.bx = 0; //offset fmoff_nef
        opi_ef:

        opi0:
            r.dh = bxTbl[r.bx];
            r.bx++;
            if (r.dh == 0xff) goto opi1b;

            r.dh += 0x80;
            r.dl = 0xff;// FM Release = 15
            opnset();
            goto opi0;

        opi1b:
            if (pw.board2 != 0)
            {
                r.stack.Push(r.ax);
                sel46();//mmainには飛ばない状況下なので大丈夫
                r.ax = r.stack.Pop();
                r.ah--;
                if (r.ah != 0) { oploop(); return; }
            }

            r.dx = 0x2800;// FM KEYOFF
            r.cx = 3;
            if (pw.board2 == 0)
            {
                if (pw.fm_effec_flag != 1) goto opi1;
                r.cx--;
            }

        opi1:
            do
            {
                opnset44();
                r.dl++;
                r.cx--;
            } while (r.cx > 0);

            if (pw.board2 != 0)
            {
                r.dx = 0x2804;// FM KEYOFF[URA]
                r.cx = 3;
                if (pw.fm_effec_flag != 1) goto opi2;
                r.cx--;
            opi2:
                do
                {
                    opnset44();
                    r.dl++;
                    r.cx--;
                } while (r.cx > 0);
            }

            if (pw.effon != 0) goto psg_ef;
            if (pw.ppsdrv_flag == 0) goto opi_nonppsdrv;

            r.ah = 0;
            ChipDatum cd = new ChipDatum(0x02, 0, 0);
            ppsdrv(cd);//.Stop();// ppsdrv keyoff

        opi_nonppsdrv:
            r.dx = 0x07bf;// PSG KEYOFF
            opnset44();
            goto s_pcm;

        psg_ef:
            //	pushf
            //    cli
            get07();
            r.dl = r.al;
            r.dl &= 0b0011_1111;
            r.dl |= 0b1001_1011;
            r.dh = 0x7;
            opnset44();
        //    popf

        s_pcm:;
            if (pw.board2 != 0)
            {
                if (pw.pcmflag != 0) goto pcm_ef;// PCM効果音発声中か？
                if (pw.adpcm != 0)
                {
                    if (pw.ademu == 0)
                    {
                        if (pw.pcm_gs_flag == 1) goto pcm_ef;
                        r.dx = 0x0102;// PAN=0 / x8 bit mode
                        opnset46();
                        r.dx = 0x0001;// PCM RESET
                        opnset46();
                    }
                }
                r.dx = 0x1080;//TA/TB/EOS を RESET
                opnset46();
                r.dx = 0x1018;//TIMERB/A/EOSのみbit変化あり
                opnset46();//(NEC音源でも一応実行)
                if (pw.pcm != 0)
                {
                    pcmdrv86.stop_86pcm();
                }
            pcm_ef:;
                if (pw.ppz != 0)
                {
                    if (pw.ppz_call_seg != 0)
                    {
                        r.ah = 0x12;
                        cd = new ChipDatum(0x12, 0, 0);
                        ppz8em(cd);//.StopInterrupt();// FIFO割り込み停止
                        r.ax = 0x0200;
                    ppz_off_loop:;
                        r.stack.Push(r.ax);
                        cd = new ChipDatum(0x02, r.al, 0);
                        ppz8em(cd);//.StopPCM(r.al);// ppz keyoff
                        r.ax = r.stack.Pop();
                        r.al++;
                        if (r.al < 8) goto ppz_off_loop;
                    }
                    //_not_ppz8:
                }
            }
        }



        //6166-6248
        //;==============================================================================
        //;	SET DATA TO OPN
        //; INPUTS ---- D,E
        //;==============================================================================
        //;
        //;	表
        //;
        public void opnset44()
        {
            r.stack.Push(r.ax);
            r.stack.Push(r.dx);
            r.stack.Push(r.bx);

            r.bx = r.dx;
            r.dx = (ushort)pw.fm1_port1;

            //    pushf
            //    cli

            rdychk();
            r.al = r.bh;
            pc98.OutPort(r.dx, r.al);
            _waitP();
            r.dx = (ushort)pw.fm1_port2;
            r.al = r.bl;
            pc98.OutPort(r.dx, r.al);

            //    popf

            r.bx = r.stack.Pop();
            r.dx = r.stack.Pop();
            r.ax = r.stack.Pop();
        }

        //;
        //;	裏
        //;
        public void opnset46()
        {
            if (pw.board2 != 0)
            {
                r.stack.Push(r.ax);
                r.stack.Push(r.bx);
                r.stack.Push(r.dx);

                r.bx = r.dx;
                r.dx = (ushort)pw.fm2_port1;

                //    pushf
                //    cli

                rdychk();
                r.al = r.bh;
                pc98.OutPort(r.dx, r.al);
                _waitP();
                r.dx = (ushort)pw.fm2_port2;
                r.al = r.bl;
                pc98.OutPort(r.dx, r.al);

                //    popf


                r.dx = r.stack.Pop();
                r.bx = r.stack.Pop();
                r.ax = r.stack.Pop();
            }
        }

        //;
        //;	表／裏
        //;
        private void opnset()
        {
            r.stack.Push(r.ax);
            r.stack.Push(r.bx);
            r.stack.Push(r.dx);

            r.bx = r.dx;
            r.dx = (ushort)pw.fm_port1;

            //    pushf
            //    cli

            rdychk();
            r.al = r.bh;
            pc98.OutPort(r.dx, r.al);
            _waitP();
            r.dx = (ushort)pw.fm_port2;
            r.al = r.bl;
            pc98.OutPort(r.dx, r.al);

            //    popf

            r.dx = r.stack.Pop();
            r.bx = r.stack.Pop();
            r.ax = r.stack.Pop();
        }



        //6249-6263
        //;==============================================================================
        //;	READ PSG 07H Port
        //; cliしてから来ること
        //;==============================================================================
        public void get07()
        {
            r.stack.Push(r.dx);
            r.dx = (ushort)pw.fm1_port1;
            rdychk();
            r.al = 7;
            pc98.OutPort(r.dx, r.al);
            _waitP();// ; PSG Read Wait
            r.dx = (ushort)pw.fm1_port2;
            r.al = pc98.InPort(r.dx);
            r.dx = r.stack.Pop();
        }



        //6264-6405
        //;==============================================================================
        //;	ＩＮＴ６０Ｈのメイン
        //;==============================================================================
        private void int60_start()
        {
            //; TimerA/B 再入check

            if ((reint_chk[r.ah] & 1) != 0)
            {
                if (pw.TimerBflag != 0) { int60_error(); return; }
            }
            if ((reint_chk[r.ah] & 2) != 0)
            {
                if (pw.TimerAflag != 0) { int60_error(); return; }
            }
            if ((reint_chk[r.ah] & 4) != 0)
            {
                if (pw.int60flag != 1) { int60_error(); return; }
            }

            if (r.ah != 0xf) int60_jumptable[r.ah]();
            else
            {
                //KUMA: 注意)外部スレッドから音源をアクセスしないようにする必要があります
                //KUMA:      どうしても必要な場合は本スレッドを止めてからにしてください。
                if (pw.board2 != 0) int60_jumptable[r.ah]();
                else nothing();
            }

            r.al = pw.al_push;
            r.ah = pw.ah_push;
            r.dx = pw.dx_push;
            int60_exit();
            return;
        }

        // 再入check用code / bit0=TimerBint 1=TimerAint 2=INT60
        private byte[] reint_chk = new byte[]{
          4,4,0,6,6,0,0,0,0,0,0,0,7,7,0,5,
          0,0,0,0,0,0,0,0,7,0,5,5,7,0,7,0,
          0,0
        };

        private Action[] int60_jumptable;
        private void Set_int60_jumptable()
        {
            int60_jumptable = new Action[]{
                mstart_f,//0
                mstop_f,//1
                fout,//2  in al:fadeout_speed
                null,//eff_on,//3
                null,//effoff,//4
                get_ss,//;5 out ax:小節数
                null,//get_musdat_adr,//6
                null,//get_tondat_adr,//7
                null,//get_fv,//8
                drv_chk,//9
                get_status,// A
                null,//get_efcdat_adr,//B
                null,//fm_effect_on,//C
                null,//fm_effect_off,//D
                get_pcm_adr,//E
                null,//pcm_effect,//F
                get_workadr,//10
                //get_fmefc_num,//11
                //get_pcmefc_num,//12
                //set_fm_int,//13
                //set_efc_int,//14
                //get_psgefcnum,//15
                //get_joy,//16
                //get_ppsdrv_flag,//17
                //set_ppsdrv_flag,//18
                //set_fv,//19
                //pause_on,//1A
                //pause_off,//1B
                //ff_music,//1C
                //get_memo,//1D
                //part_mask,//1E
                //get_fm_int,//1F
                //get_efc_int,//20
                //get_mus_name,//21
                //get_size//22
            };
        }

        //6406
        private int int60_max = 0x22;



        //6408-6413
        private void get_ss()
        {
            getss();
            pw.al_push = r.al;
            pw.ah_push = r.ah;
        }



        //6452-6475
        private void drv_chk()
        {
            if (pw.board2 != 0)
            {
                if (pw.ppz != 0)
                {
                    if (pw.ademu != 0)
                    {
                        pw.al_push = 5;
                    }
                    else
                    {
                        pw.al_push = 4;
                    }
                }
                else
                {
                    if (pw.pcm != 0)
                    {
                        pw.al_push = 2;
                    }
                    else
                    {
                        pw.al_push = 1;
                    }
                }
            }
            else
            {
                pw.al_push = 0;
            }
            r.ah = (byte)pw.vers;
            r.al = (byte)pw.verc;
            pw.ah_push = r.ah;
            pw.dx_push = r.ax;
        }


        //6476-6481
        private void get_status()
        {
            getst();
            pw.al_push = r.al;
            pw.ah_push = r.ah;
        }



        //6482-6487
        private void get_pcm_adr()
        {
            r.ax = 0;// r.cs;
            pw.ds_push = r.ax;
            pw.dx_push = 0;//offset pcm_table
        }



        //6488-6493
        private void get_workadr()
        {
            r.ax = 0;// r.cs;
            pw.ds_push = r.ax;
            pw.dx_push = 0;//offset part_data_table
        }



        //6738-6789
        //;==============================================================================
        //;	メモ文字列の取り出し
        //;==============================================================================
        public ushort get_memo(int al)
        {
            try {
                r.al = (byte)al;
                r.si = (ushort)pw.mmlbuf;
                if (pw.md[r.si].dat != 0x1a)
                    goto getmemo_errret;//;音色がないfile=メモのアドレス取得不能
                r.si += 0x18;
                r.si = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
                r.si += (ushort)pw.mmlbuf;
                r.si -= 4;
                r.bx = (ushort)(pw.md[r.si + 2].dat + pw.md[r.si + 3].dat * 0x100);//;bh=0feh,bl=ver
                if (r.bl == 0x40)//;Ver4.0 & 00Hの場合
                    goto getmemo_exec;
                if (r.bh != 0xfe)
                    goto getmemo_errret;//;Ver.4.1以降は 0feh
                if (r.bl < 0x41)
                    goto getmemo_errret;//;MC version 4.1以前だったらError
                getmemo_exec:;
                if (r.bl < 0x42)//;Ver.4.2以降か？
                    goto getmemo_oldver41;
                r.al++;//; ならalを +1 (0FFHで#PPSFile)
            getmemo_oldver41:;
                if (r.bl < 0x48)//;Ver.4.8以降か？
                    goto getmemo_oldver47;
                r.al++;//; ならalを +1 (0FEHで#PPZFile)
            getmemo_oldver47:;
                r.si = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
                r.si += (ushort)pw.mmlbuf;
                r.al++;
            getmemo_loop:;
                r.dx = (ushort)(pw.md[r.si + 0].dat + pw.md[r.si + 1].dat * 0x100);
                if (r.dx == 0)
                    goto getmemo_errret;
                r.si += 2;
                r.al--;
                if (r.al != 0)
                    goto getmemo_loop;
                getmemo_exit:;
                r.dx += (ushort)pw.mmlbuf;
                pw.ds_push = 0;// r.cs; セグメントなし
                pw.dx_push = r.dx;
                return r.dx;

            getmemo_errret:;
                pw.ds_push = 0;
                pw.dx_push = 0;
                return 0;
            }
            catch
            {
                Log.WriteLine(LogLevel.WARNING, "メモのアドレスが範囲外を指していることを検出しました。無視します。");
                pw.ds_push = 0;
                pw.dx_push = 0;
                return 0;
            }
        }


        //6790-6819
        //;==============================================================================
        //;	曲の頭だし
        //;		input DX<- 小節番号
        //; output AL<- return code   0:正常終了
        //;						1:その小節まで曲がない
        //;						2:曲が演奏されていない
        //;==============================================================================
        private void ff_music()
        {
            r.stack.Push(r.dx);
            r.dx = pw.mask_adr;
            //pushf
            //cli
            r.al = pc98.InPort(r.dx);
            r.al |= pw.mask_data;
            pc98.OutPort(r.dx, r.al);//;FM割り込みを禁止
            //popf
            r.dx = r.stack.Pop();
            ff_music_main();
            pw.al_push = r.al;
            r.dx = pw.mask_adr;
            //pushf
            //cli
            r.al = pc98.InPort(r.dx);
            r.al &= pw.mask_data2;
            pc98.OutPort(r.dx, r.al);//;FM割り込みを許可
            //    popf
            return;
        }



        //6820-6898
        private void ff_music_main()
        {
            if (pw.status2 == 255)
                goto ffm_exit2;
            pw.skip_flag = 1;
            if (r.dx >= pw.syousetu)//	cmp dx,[syousetu]
                goto ffm_main;
            pw.skip_flag = 2;
            r.stack.Push(r.dx);
            if (pw.effon != 1)
                goto ff_no_ssg_dr;
            efcdrv.effend();//; ssgdrums cut

        ff_no_ssg_dr:;
            data_init2();
            play_init();
            opn_init();
            r.dx = r.stack.Pop();
            if (r.dx != 0)
                goto ffm_main;
            silence();
            goto ffm_exit0b;

        ffm_main:;
            r.stack.Push(r.dx);
            maskon_all();
            if (pw.board2 != 0)
            {
                r.dx = 0x10ff;
                opnset44();//;Rhythm音源を全部Dump
            }
            r.dx = r.stack.Pop();
            r.ah = pw.fadeout_volume;
            r.al = pw.rhythmmask;
            pw.fadeout_volume = 255;
            pw.rhythmmask = 0;
            r.stack.Push(r.ax);
            r.stack.Push(r.bp);
            r.bp = r.dx;
        ffm_loop:;
            mmain();
            syousetu_count();
            if (pw.status2 == 255)
                goto ffm_exit1;
            if (r.bp >= pw.syousetu)
                goto ffm_loop;
            r.bp = r.stack.Pop();
            r.ax = r.stack.Pop();
            pw.fadeout_volume = r.ah;
            pw.rhythmmask = r.al;
            if (pw.board2 != 0)
            {
                if (r.ah != 0)
                    goto ffm_exit0;
                r.dl = pw.rhyvol;
                volset2rf();
            }
        ffm_exit0:;
            maskoff_all();
        ffm_exit0b:;
            r.dl = pw.ff_tempo;
            r.dl--;//; ffより1少ないtempo
            stb_ff();
            r.al = 0;
            goto ffm_exit;

        ffm_exit1:;
            r.bp = r.stack.Pop();
            r.ax = r.stack.Pop();
            maskoff_all();
            r.al = 1;
            goto ffm_exit;

        ffm_exit2:;
            r.al = 2;

        ffm_exit:;
            pw.skip_flag = 0;
            return;
        }



        //6899-6931
        //;==============================================================================
        //;	全パート一時マスク
        //;==============================================================================
        private void maskon_all()
        {
            r.si = 0;//offset part_table
            r.cx = (ushort)pw.max_part1;
            r.di = (ushort)pw.part1;

        maskon_loop:;
            if (pw.ppz != 0)
            {
                r.al = pw.part_table[r.si++];
                if (r.al != 0xff)//-1
                    goto monl_main;
                r.si += 6;//; skip Rhythm & Effects(for PPZ parts)
            monl_main:;
            }
            else
            {
                r.si++;
            }

            r.al = pw.part_table[r.si++];//    lodsw; ah=音源 al = partb
            r.ah = pw.part_table[r.si++];

            pw.partWk[r.di].partmask |= 0x80;
            if (pw.partWk[r.di].partmask != 0x80)
                goto maskon_next;//;既に他でマスクされてた

            r.stack.Push(r.cx);
            r.stack.Push(r.di);
            r.stack.Push(r.si);
            maskon_main();//;1パートマスク
            r.si = r.stack.Pop();
            r.di = r.stack.Pop();
            r.cx = r.stack.Pop();

        maskon_next:;
            r.di += 1;//qq
            r.cx--;
            if (r.cx != 0) goto maskon_loop;
            return;
        }



        //6932-6963
        //;==============================================================================
        //;	全パート一時マスク解除
        //;==============================================================================
        private void maskoff_all()
        {
            r.si = 0;//offset part_table
            r.cx = (ushort)pw.max_part1;
            r.di = (ushort)pw.part1;
        maskoff_loop:;
            if (pw.ppz != 0)
            {
                r.al = pw.part_table[r.si++];
                if (r.al != 0xff)//-1
                    goto moffl_main;
                r.si += 6;//; skip Rhythm & Effects(for PPZ parts)
            moffl_main:;
            }
            else
            {
                r.si++;
            }

            r.al = pw.part_table[r.si++];//    lodsw; ah=音源 al = partb
            r.ah = pw.part_table[r.si++];

            pw.partWk[r.di].partmask &= 0x7f;
            if (pw.partWk[r.di].partmask != 0)
                goto maskoff_next;//;まだ他でマスクされてる

            r.stack.Push(r.cx);
            r.stack.Push(r.di);
            r.stack.Push(r.si);
            maskoff_main();//;1パート復帰
            r.si = r.stack.Pop();
            r.di = r.stack.Pop();
            r.cx = r.stack.Pop();

        maskoff_next:;
            r.di += 1;//qq
            r.cx--;
            if (r.cx != 0) goto maskoff_loop;
            return;
        }



        //6964-7107
        //;==============================================================================
        //;	パートのマスク & Keyoff
        //;==============================================================================
        private void part_mask()
        {
            r.ah = r.al;
            r.ah &= 0x7f;
            if (pw.ppz != 0)
            {
                r.carry = (r.ah < 16 + 8);
            }
            else
            {
                r.carry = (r.ah < 16 );
            }
            if (!r.carry) return;//goto pm_ret;
            if ((r.al & 0x80) != 0)
            {
                part_on();
                return;
            }
            r.bh = 0;
            r.bl = r.al;
            r.bl += r.bl;
            r.bl += r.al;
            r.bx = 0;//offset part_table
            r.dl = pw.part_table[r.bx];//; dl<- Part番号
            if ((r.dl & 0x80) != 0)
            {
                rhythm_mask();
                return;
            }
            r.bx++;
            r.al = pw.part_table[r.bx + 0];//;AH=音源 AL = partb
            r.ah = pw.part_table[r.bx + 1];
            r.bh = 0;
            r.bl = r.dl;
            //r.bx += r.bx;
            r.bx = 0;//offset part_data_table
            r.di = (ushort)pw.part_data_table[r.bx];
            r.dl = pw.partWk[r.di].partmask;
            pw.partWk[r.di].partmask |= 1;
            if (r.dl != 0)
                return;//goto pm_ret;// ; 既にマスクされていた
            if (pw.play_flag == 0)
                return;// goto pm_ret;// ; 曲が止まっている

            maskon_main();
        }

        private void maskon_main()
        {
            if (r.ah == 0)
                goto pm_fm1;
            r.ah--;
            if (pw.board2 != 0)
            {
                if (r.ah == 0) goto pm_fm2;
            }
            r.ah--;
            if (r.ah == 0) goto pm_ssg;
            r.ah--;
            if (pw.board2 != 0)
            {
                if (r.ah == 0) goto pm_pcm;
            }
            r.ah--;
            if (r.ah == 0) goto pm_drums;
            if (pw.ppz != 0)
            {
                r.ah--;
                if (r.ah == 0) goto pm_ppz;
            }
        //pm_ret:;
            return;

        pm_fm1:;
            //pushf
            //cli
            pw.partb = r.al;
            if (pw.board2 != 0)
            {
                sel44();
            }
            silence_fmpart();//; 音を完璧に消す
            //popf
            return;

        pm_fm2:;
            if (pw.board2 != 0)
            {
                //pushf
                //cli
                pw.partb = r.al;
                sel46();
                silence_fmpart();//; 音を完璧に消す
                //popf
                return;
            }

        pm_drums:;
            if (pw.psgefcnum >= 11)
                goto pm_ssg_ret;

            efcdrv.effend();
            return;

        pm_ssg:;
            //pushf
            //cli
            pw.partb = r.al;
            psgmsk();//	;AL=07h AH = Maskdata
            r.dh = 7;
            r.dl = r.al;
            r.dl |= r.ah;
            opnset44();// ; PSG keyoff
                       //popf
        pm_ssg_ret:;
            return;

        pm_pcm:;
            if (pw.board2 != 0)
            {
                if (pw.adpcm != 0)
                {
                    if (pw.ademu != 0)
                    {
                        if (pw.adpcm_emulate != 1)
                            goto pmpcm_noadpcm;
                        r.ax = 0x0207;
                        ChipDatum cd = new ChipDatum(0x02, r.al, 0);
                        ppz8em(cd);//.StopPCM(r.al);//; PPZ8 ch7 発音停止
                    pmpcm_noadpcm:;
                    }
                    else
                    {
                        //pushf
                        //cli
                        r.dx = 0x0102;//; PAN=0 / x8 bit mode
                        opnset46();
                        r.dx = 0x0001;//; PCM RESET
                        opnset46();
                        //popf
                    }
                }
                if (pw.pcm != 0)
                {
                    pcmdrv86.stop_86pcm();
                }
                return;
            }

        pm_ppz:;
            if (pw.ppz != 0)
            {
                if (pw.ademu != 0)
                {
                    if (r.al != 7)
                        goto pmppz_exec;
                    if (pw.adpcm_emulate == 1)
                        goto pmppz_noexec;
                    pmppz_exec:;
                }
                r.ah = 2;
                ChipDatum cd = new ChipDatum(0x02, r.al, 0);
                ppz8em(cd);//.StopPCM(r.al);// ; ppz stop(al= partb)
            pmppz_noexec:;
                return;
            }
        }

        private void rhythm_mask()
        { 
            pw.rhythmmask = 0;//;Rhythm音源をMask
            if (pw.board2 != 0)
            {
                r.dx = 0x10ff;
                opnset44();//;Rhythm音源を全部Dump
            }
            return;
        }


        //7108-7176
        //;==============================================================================
        //;	パートのマスク解除 & FM音源音色設定	in.AH=part番号
        //;==============================================================================
        private void part_on()
        {
            r.bh = 0;
            r.bl = r.ah;
            r.bl += r.bl;
            r.bl += r.ah;
            r.bx += 0;//offset part_table
            r.dl = pw.part_table[r.bx];//;dl<- Part番号
            if ((r.dl & 0x80) != 0)
            {
                rhythm_on();
                return;
            }
            r.bx++;
            r.al = pw.part_table[r.bx + 0];//;AH=音源 AL = partb
            r.ah = pw.part_table[r.bx + 1];
            r.bh = 0;
            r.bl = r.dl;
            //r.bx += r.bx;
            r.bx += 0;//offset part_data_table
            r.di = (ushort)pw.part_data_table[r.bx];
            if (pw.partWk[r.di].partmask == 0)
                return;//goto po_ret;// ; マスクされてない
            pw.partWk[r.di].partmask &= 0xfe;
            if (pw.partWk[r.di].partmask != 0)
                return;//goto po_ret;//;効果音でまだマスクされている
            if (pw.play_flag == 0)
                return;// goto po_ret;// ; 曲が止まっている
            maskoff_main();
        }

        private void maskoff_main()
        {
            if (r.ah == 0)
                goto po_fm1;//; FM音源の場合は
            if (pw.board2 != 0)
            {
                r.ah--;
                if (r.ah == 0) goto po_fm2;//;音色設定処理
            }

        //po_ret:;
            return;

        po_fm1:;
            r.dl = pw.partWk[r.di].voicenum;
            //pushf
            //cli
            pw.partb = r.al;
            if (pw.board2 != 0)
            {
                sel44();
            }
            if (pw.partWk[r.di].address == 0)
                goto pof1_not_set;
            neiro_reset();

        pof1_not_set:;
            //popf
            return;

        po_fm2:;
            if (pw.board2 != 0)
            {
                r.dl = pw.partWk[r.di].voicenum;
                //pushf
                //cli
                pw.partb = r.al;
                sel46();
                if (pw.partWk[r.di].address == 0)
                    goto pof2_not_set;
                neiro_reset();
            pof2_not_set:;
                //popf
                return;
            }
        }

        private void rhythm_on()
        {
            pw.rhythmmask = 0xff;//;Rhythm音源をMask解除
            return;
        }



        //7243-7332
        //;==============================================================================
        //;	ボードがない時
        //;==============================================================================
        private void int60_start_not_board()
        {
            n_int60_jumptable[r.ah]();

            r.al = pw.al_push;
            r.ah = pw.ah_push;
            r.dx = pw.dx_push;
            int60_exit();
            return;
        }

        private Action[] n_int60_jumptable;

        private void Set_n_int60_jumptable()
        {
            n_int60_jumptable = new Action[] {
            // nothing//0
            //,nothing//1
            //,nothing//2
            //,nothing//3
            //,nothing//4
            //,get_255//5
            //,get_musdat_adr//6
            //,get_tondat_adr//7
            //,get_255//8
            //,drv_chk2//9
            //,get_65535//A
            //,get_efcdat_adr//B
            //,nothing		//C
            //,nothing		//D
            //,get_pcm_adr	//E
            //,nothing		//F
            //,get_workadr	//10
            //,get_255//11
            //,get_255//12
            //,nothing//13
            //,nothing//14
            //,get_65535//15
            //,get_65535//16
            //,get_255//17
            //,nothing//18
            //,nothing//19
            //,nothing//1A
            //,nothing		//1B
            //,nothing		//1C
            //,get_memo	//1D
            //,nothing//1E
            //,get_fm_int//1F
            //,get_efc_int//20
            //,get_mus_name//21
            //,get_size//22
            };
        }

        private void get_255()
        {
            pw.al_push = 255;
        }

        //7333-7334
        private void nothing()
        {
        }

        private void get_65535()
        {
            pw.ah_push = 255;
            get_255();
        }



        //7346-7415
        //;==============================================================================
        //;	ＦＭ効果音ルーチン
        //;==============================================================================
        //;==============================================================================
        //;	発音
        //;		input AL to number_of_data
        //;==============================================================================
        private void fm_effect_on()
        {
            if (pw.efcdat == -1) return;//KUMA: 将来効果音使うときまで封印

            //pushf
            //cli
            if (pw.fm_effec_flag == 0)
                goto not_e_flag;

            r.stack.Push(r.ax);
            fm_effect_off();
            r.ax = r.stack.Pop();

        not_e_flag:;
            pw.fm_effec_num = r.al;
            pw.fm_effec_flag = 1;//; 効果音発声してね
            pw.partb = 3;
            if (pw.board2 == 0)
            {
                r.di = (ushort)pw.part3;//offset part3
                pw.partWk[r.di].partmask |= 2;//; Part Mask
                r.di = (ushort)pw.part3b;//offset part3b
                pw.partWk[r.di].partmask |= 2;//; Part Mask
                r.di = (ushort)pw.part3c;//offset part3c
                pw.partWk[r.di].partmask |= 2;//; Part Mask
                r.di = (ushort)pw.part3d;//offset part3d
                pw.partWk[r.di].partmask |= 2;//; Part Mask
            }
            else
            {
                r.di = (ushort)pw.part6;//offset part6
                pw.partWk[r.di].partmask |= 2;//; Part Mask
            }
            r.bh = 0;
            r.bl = pw.fm_effec_num;//; bx = effect no.
            r.di = (ushort)pw.part_e;//offset part_e
            r.al = 0;
            pw.partWk[r.di].Clear();//; PartData 初期化
            r.bx += r.bx;
            r.bx += (ushort)pw.efcdat;
            r.ax = (ushort)(pw.md[r.bx].dat + pw.md[r.bx + 1].dat * 0x100);
            r.ax += (ushort)pw.efcdat;
            r.di = (ushort)pw.part_e;
            pw.partWk[r.di].address = r.ax;//; アドレスのセット
            pw.partWk[r.di].leng = 1;//; あと1カウントで演奏開始
            pw.partWk[r.di].volume = 108;//; FM VOLUME DEFAULT= 108
            pw.partWk[r.di].slotmask = 0xf0;//; FM SLOTMASK
            pw.partWk[r.di].neiromask = 0xff;//; FM Neiro MASK
            if (pw.board2 != 0)
            {
                r.dl = 0xc0;
                pw.partWk[r.di].fmpan = r.dl;//; FM PAN = Middle
                r.dh = 0xb6;
                sel46();//;ここでmmainが来てもsel46のまま
                opnset();
            }
            else
            {
                r.al = pw.ch3mode;
                pw.ch3mode_push = r.al;
                pw.ch3mode = 0x3f;
            }

            //popf
            //ret
        }



        //7416-7467
        //;==============================================================================
        //;	消音
        //;==============================================================================
        private void fm_effect_off()
        {
            //pushf
            //cli
            if (pw.fm_effec_flag == 0)
                goto feo_ret;

            pw.fm_effec_num = 0xff;// -1;
            pw.fm_effec_flag = 0;//; 効果音止めてね
            if (pw.board2 != 0)
            {
                sel46();//;ここでmmainが来てもsel46のまま
            }
            r.di = (ushort)pw.part_e;
            pw.partb = 3;
            silence_fmpart();
            if (pw.play_flag == 0)
                goto feo_ret;//; 曲が止まっている

            if (pw.board2 != 0)
            {
                r.di = (ushort)pw.part6;
                r.dl = pw.partWk[r.di].voicenum;
                neiro_reset();
            }
            else
            {
                r.di = (ushort)pw.part3;
                r.dl = pw.partWk[r.di].voicenum;
                neiro_reset();

                r.di = (ushort)pw.part3b;
                r.dl = pw.partWk[r.di].voicenum;
                neiro_reset();

                r.di = (ushort)pw.part3c;
                r.dl = pw.partWk[r.di].voicenum;
                neiro_reset();


                r.di = (ushort)pw.part3d;
                r.dl = pw.partWk[r.di].voicenum;
                neiro_reset();

                r.al = pw.ch3mode_push;
                pw.ch3mode = r.al;
                r.dh = 0x27;
                r.dl = r.al;
                r.dl &= 0b1100_1111;//;Resetはしない
                opnset44();
            }

        feo_ret:;
            //	popf
            return;
        }



        //7658-7721
        //;==============================================================================
        //;	FM TimerA/B 処理 Main
        //;		*Timerが来ている事を確認してから飛んで来ること。
        //;		 pushしてあるレジスタは ax/dx/ds のみ。
        //;==============================================================================
        private void FM_Timer_main()
        {
            //push cx
            //;------------------------------------------------------------------------------
            //;	Timer Reset
            //; 同時にＦＭ割り込み Timer AorB どちらが来たかを読み取る
            //;------------------------------------------------------------------------------
            r.dx = (ushort)pw.fm1_port1;
            rdychk();
            r.al = 0x27;
            pc98.OutPort(r.dx, r.al);
            _wait();
            r.ah = pw.ch3mode;//; ah = 27hに出力する値
            r.al = (byte)pw.timer.StatReg;// pc98.InPort(r.dx);//;rdychk	;al = status
            byte a = r.ah;
            r.ah = r.al;
            r.al = a;//;ah = status / al=27hに出力する値

            r.dx = (ushort)pw.fm1_port2;
            pc98.OutPort(r.dx, r.al);//;Timer Reset

            //r.ah = (byte)(pw.timer.StatReg & 3);//; ah = TimerA/B flag

            //;------------------------------------------------------------------------------
            //;	割り込み許可
            //;------------------------------------------------------------------------------
            if (pw.disint == 1)
                goto not_sti;
            //sti
            not_sti:;

            //;------------------------------------------------------------------------------
            //;	どちらが来たかで場合分け処理
            //;------------------------------------------------------------------------------

            r.ah--;//; Timer Aか？
            if (r.ah == 0) goto TimerA_int;// Timer Aの方を処理

            r.ah--;//; Timer Bか？
            if (r.ah == 0) goto TimerB_int;// Timer Bの方を処理

            TimerB_main();// 同時
        TimerA_int:;
            TimerA_main();
            goto exit_Timer;

        TimerB_int:;
            TimerB_main();
        exit_Timer:;
            //    cli
        }



        //7722-7760
        //;==============================================================================
        //;	TimerBの処理[メイン]
        //;==============================================================================
        private void TimerB_main()
        {
            if (pw.sync != 0) return;
            opnint_sub();
        }

        private void opnint_sub()
        {
            pw.TimerBflag = 1;
            if (pw.music_flag == 0) goto not_mstop;
            if ((pw.music_flag & 1) == 0) goto not_mstart;
            mstart();
        not_mstart:
            if ((pw.music_flag & 2) == 0) goto not_mstop;
            mstop();
        not_mstop:
            if (pw.play_flag == 0) goto not_play;
            mmain();
            settempo_b();
            syousetu_count();
            r.al = pw.TimerAtime;
            pw.lastTimerAtime = r.al;
        not_play:
            pw.TimerBflag = 0;
            if ((pw.intfook_flag & 1) == 0) goto TimerB_nojump;
            //    call dword ptr[fmint_ofs]
            TimerB_nojump:;
            return;
        }



        //7761-7827
        //;==============================================================================
        //;	TimerAの処理[メイン]
        //;==============================================================================
        private void TimerA_main()
        {
            pw.TimerAflag = 1;
            pw.TimerAtime++;
            r.al = pw.TimerAtime;
            r.al &= 7;
            if (r.al != 0)
                goto not_fade;
            fadeout();//; Fadeout処理
            rew();//;Rew処理
        not_fade:;
            if (pw.effon == 0)
                goto not_psgeffec;
            if (pw.ppsdrv_flag == 0)
                goto ta_not_ppsdrv;
            if ((pw.psgefcnum & 0x80) != 0)
                goto not_psgeffec;//;ppsdrvの方で鳴らしている
            ta_not_ppsdrv:;
            efcdrv.effplay();//; SSG効果音処理
        not_psgeffec:;
            if (pw.fm_effec_flag == 0)
                goto not_fmeffec;
            fm_efcplay();//; FM効果音処理
        not_fmeffec:;
            if (pw.key_check == 0)
                goto vtc000;
            if (pw.play_flag == 0)
                goto vtc000;
            if (pw.va != 0)
            {
                r.al = pc98.InPort(8);
                r.ah = pw.esc_sp_key;
                if ((r.ah & r.al) != 0)
                    goto vtc000;
                r.al = pc98.InPort(9);
                if ((r.al & 0b1000_0000) != 0)
                    goto vtc000;
            }
            else
            {
                //mov es, 0
                r.bx = 0x52a;
                r.al = pw.esc_sp_key;
                r.al &= 0;//byte ptr es:0eh[bx]
                if (r.al != pw.esc_sp_key)
                    goto vtc000;
                if ((0 & 0b0000_0001) == 0) //byte ptr es:[bx];esc
                    goto vtc000;
                //mov es,cs
            }

            pw.music_flag |= 2;//;次のTimerBでMSTOP
            pw.fadeout_flag = 0;//;CTRL+ESCで止めた=外部扱い

        vtc000:;
            pw.TimerAflag = 0;
            if ((pw.intfook_flag & 2) == 0)
                goto TimerA_nojump;

            //TBD
            //pw.efcint_ofs(); //dword ptr[efcint_ofs]

        TimerA_nojump:;
            return;
        }



        //7828-7840
        //;==============================================================================
        //;	小節のカウント
        //;==============================================================================
        private void syousetu_count()
        {
            r.al = pw.opncount;
            r.al++;
            if (r.al != pw.syousetu_lng) goto sc_ret;
            r.al = 0;
            pw.syousetu++;
        sc_ret:
            pw.opncount = r.al;
        }



        //7841-7860
        //;==============================================================================
        //;	テンポ設定
        //;==============================================================================
        private void settempo_b()
        {
            r.ah = pw.grph_sp_key;
            check_grph();

            if (!r.carry)
            {
                //stb_n:
                r.dl = pw.tempo_d;
            }
            else
            {
                r.dl = pw.ff_tempo;
            }
            stb_ff();
        }

        private void stb_ff()
        { 
            if (r.dl == pw.TimerB_speed) return;

            pw.TimerB_speed = r.dl;
            r.dh = 0x26;
            opnset44();
            return;
            //stb_ret:
        }



        //7861-7881
        //;==============================================================================
        //;	巻き戻し処理
        //;==============================================================================
        private void rew()
        {
            r.ah = pw.rew_sp_key;
            check_grph();
            if (!r.carry) goto rew_ret;
            r.dx = pw.syousetu;
            r.al = pw.syousetu_lng;
            r.al = (byte)((sbyte)r.al >> 1);
            r.al = (byte)((sbyte)r.al >> 1);
            if (pw.opncount >= r.al)
            {
                ff_music_main();
                return;
            }
            if (r.dx == 0)
            {
                ff_music_main();
                return;
            }
            r.dx--;
            {
                ff_music_main();
                return;
            }
        rew_ret:;
            return;
        }



        //7882-7921
        //;==============================================================================
        //;	GRPH key check
        //;		in	AH sp_key
        //;		out	CY	1で押されている
        //;==============================================================================
        private void check_grph()
        {
            if (pw.key_check == 0)//cy=0
                return;
            //cgr_main:
            r.carry = pc98.GetGraphKey();
        }



        //8414-
        private void comstart()
        {
            //;==============================================================================
            //; ＰＭＤコマンドスタート
            //;==============================================================================

            print_mes(pw.mes_title);// タイトル表示

            //;==============================================================================
            //; ＰＭＤ常駐CHECK
            //;==============================================================================
            //略

            //;==============================================================================
            //; 常駐処理
            //;==============================================================================
            //resident_main:
            //;==============================================================================
            //; オプション初期設定
            //;==============================================================================
            r.ax = 0;

            pw.mmldat_lng = (byte)pw.mdata_def;//; Default 16K
            pw.voicedat_lng = (byte)pw.voice_def;//; Default 8K
            pw.effecdat_lng = (byte)pw.effect_def;//; Default 4K
            pw.key_check = (byte)pw.key_def;// Keycheck ON

            pw.fm_voldown = (byte)pw.fmvd_init;//; FM_VOLDOWN
            pw._fm_voldown = (byte)pw.fmvd_init;//; FM_VOLDOWN
            pw.ssg_voldown = r.al;//; SSG_VOLDOWN
            pw._ssg_voldown = r.al;//; SSG_VOLDOWN
            pw.pcm_voldown = r.al;//; PCM_VOLDOWN
            pw._pcm_voldown = r.al;//; PCM_VOLDOWN
            pw.ppz_voldown = r.al;//; PPZ_VOLDOWN
            pw._ppz_voldown = r.al;//; PPZ_VOLDOWN
            pw.rhythm_voldown = r.al;//; RHYTHM_VOLDOWN
            pw._rhythm_voldown = r.al;//; RHYTHM_VOLDOWN
            pw.kp_rhythm_flag = 0xff;//; SSGDRUMでRHYTHM音源を鳴らすか FLAG

            r.di = 0;//offset rshot_bd
            pw.rshot[0] = 0;// _bd = 0;
            pw.rshot[1] = 0;// _sd = 0;
            pw.rshot[2] = 0;// _sym = 0;
            pw.rshot[3] = 0;// _hh = 0;
            pw.rshot[4] = 0;// _tom = 0;
            pw.rshot[5] = 0;// _rim = 0;

            r.di = (ushort)pw.part1;//offset part1
            r.cx = (ushort)pw.max_part1;
            do
            {
                pw.partWk[r.di++].Clear();
                r.cx--;
            } while (r.cx != 0);

            pw.disint = r.al;//; INT Disable FLAG
            pw.rescut_cant = r.al;//; 常駐解除禁止 FLAG
            pw.adpcm_wait = r.al;//; ADPCM定義速度
            pw.pcm86_vol = r.al;//; PCM音量合わせ
            pw._pcm86_vol = r.al;//; PCM音量合わせ
            pw.fade_stop_flag = 1;//; FADEOUT後MSTOPするか FLAG
            pw.ppsdrv_flag = 0xff;//; PPSDRV FLAG

            if (pw.va != 0)
            {
                pw.grph_sp_key = 0x80;// GRPH + CTRL key code
                pw.rew_sp_key = 0x40;// GPPH + SHIFTkey code
                pw.esc_sp_key = 0x80;// ESC + CTRL key code
            }
            else
            {
                pw.grph_sp_key = 0x10;// GRPH + CTRL key code
                pw.rew_sp_key = 0x1;// GPPH + SHIFTkey code
                pw.esc_sp_key = 0x10;// ESC + CTRL key code
                pw.port_sel = 0xff;// ポート選択 = 自動
            }
            pw.ff_tempo = 250;
            pw.music_flag = r.al;
            pw.message_flag = 1;

            //;==============================================================================
            //; ＦＭ音源のcheck(INT / PORT選択)
            //;==============================================================================

            //TBD

            //;==============================================================================
            //; オプションを取り込む
            //;==============================================================================

            //TBD "PMDOPT=" 検索
            set_option(pw.pmdOption);

            //;==============================================================================
            //; vmapエリアに"PMD"文字列書込み
            //;==============================================================================

            //TBD


            //;==============================================================================
            //; Memory Check &Init
            //;==============================================================================

            //TBD

            //;==============================================================================
            //; 曲データ，音色データ格納番地を設定
            //;==============================================================================

            r.ax = 1;//offset dataarea+1
            pw.mmlbuf = r.ax;
            r.ax--;

            r.bh = pw.mmldat_lng;
            r.bl = 0;
            r.bx <<= 2;
            r.ax += r.bx;
            pw.tondat = r.ax;
            r.bh = pw.voicedat_lng;
            r.bl = 0;
            r.bx <<= 2;
            r.ax += r.bx;
            pw.efcdat = r.ax;
            pw.efcdat = -1;//効果音は未使用

            Random rnd = new System.Random();
            pw.seed = (ushort)rnd.Next(0, 0xffff);

            //;==============================================================================
            //; 効果音 / FMINT / EFCINTを初期化
            //;==============================================================================
            r.ax = 0;
            pw.fmint_seg = r.ax;
            pw.fmint_ofs = r.ax;
            pw.efcint_seg = r.ax;
            pw.efcint_ofs = r.ax;
            pw.intfook_flag = r.al;
            pw.skip_flag = r.al;
            pw.effon = r.al;
            pw.fm_effec_flag = r.al;
            pw.pcmflag = r.al;

            r.al--;

            pw.psgefcnum = r.al;
            pw.fm_effec_num = r.al;
            pw.pcm_effec_num = r.al;

            //;==============================================================================
            //; 割り込み設定
            //;==============================================================================
            if (pw.board == 0) goto not_set_opnvec;

            //;==============================================================================
            //; OPN 初期化
            //;==============================================================================
            int_init();

            //; ------------------------------------------------------------------------------
            //; 088 / 188 / 288 / 388(同INT番号のみ) を初期設定
            //; ------------------------------------------------------------------------------
            if (pw.va != 0)
            {
                r.ax = 0x2900;
                opnset44();
                r.ax = 0x2400;
                opnset44();
                r.ax = 0x2500;
                opnset44();
                r.ax = 0x2600;
                opnset44();
                r.ax = 0x273f;
                opnset44();
            }
            else
            {
                r.cx = 4;
                r.dx = 0x88;

                r.cx = 1;//KUMA:0x188のみ
                r.dx = 0x188;//KUMA:0x188のみ

            //opninit_loop:;
                do
                {
                    r.stack.Push(r.cx);
                    r.ah = 0xff;//-1
                    r.cx = 256;

                //opninit_loop2:;
                    do
                    {
                        r.al = pc98.InPort(r.dx);
                        r.ah &= r.al;
                        if ((r.ah & 0x80) == 0)
                            goto opninit_exec;
                        r.cx--;
                    } while (r.cx != 0);
                    goto opninit_next;// ; 音源無し

                opninit_exec:;
                    //pushf
                    //cli

                    rdychk();
                    r.al = 0xe;
                    pc98.OutPort(r.dx, r.al);
                    r.cx = 256;
                    do { r.cx--; } while (r.cx != 0);
                    r.dx += 2;
                    r.al = pc98.InPort(r.dx);

                    //popf

                    r.dx -= 2;
                    r.al &= 0xc0;
                    if (r.al != pw.opn_0eh)//; int番号を比較
                        goto opninit_next;// ; 非一致なら初期化しない

                    r.ax = 0x2900;
                    opnset_fmc();
                    r.ax = 0x2400;
                    opnset_fmc();
                    r.ax = 0x2500;
                    opnset_fmc();
                    r.ax = 0x2600;
                    opnset_fmc();
                    r.ax = 0x273f;
                    opnset_fmc();

                opninit_next:;
                    r.cx = r.stack.Pop();
                    r.dh++;

                    r.cx--;
                } while (r.cx != 0);
            }

            //;==============================================================================
            //; ＯＰＮ 割り込みベクトル 退避
            //;==============================================================================
            //  cli
            r.ax = 0;
            //r.es = r.ax;
            //r.bx = pw.vector;
            r.bx = 0;//les bx, es:[bx]
            pw.int5ofs = r.bx;
            pw.int5seg = 0;// r.es;

            //;==============================================================================
            //; ＯＰＮ 割り込みベクトル 設定
            //;==============================================================================
            //r.es = r.ax;
            //r.bx = pw.vector;
            //es:[bx] = 0;//offset opnint
            //es:[bx+2] = r.cs;
        not_set_opnvec:;

            //;==============================================================================
            //; INT60 割り込みベクトル 退避
            //;==============================================================================
            //cli
            r.ax = 0;
            //r.es = r.ax;
            //r.bx = es:[pmdvector*4];
            pw.int60ofs = r.bx;
            pw.int60seg = 0;// r.es;

            //;==============================================================================
            //; INT60 割り込みベクトル 設定
            //;==============================================================================
            //r.es = r.ax;
            //es:[pmdvector*4] = 0;//offset int60_head
            //es:[pmdvector*4 + 2] = r.cs;

            //;==============================================================================
            //; ＯＰＮ割り込み開始
            //;==============================================================================
            opnint_start();
            //sti
        }



        //8896-
        private void int_init()
        {
            //不要?

            pps_chk();
        }



        //8970-9029
        //;------------------------------------------------------------------------------
        //;	ppsdrv/ppz8常駐CHECK
        //;------------------------------------------------------------------------------
        private void pps_chk()
        {
            if (pw.ppsdrv_flag == 0xff)
                goto pps_check;

            if (pw.kp_rhythm_flag != 0xff)
                goto ppschk_exit;

            r.al = pw.ppsdrv_flag;
            r.al ^= 1;
            pw.kp_rhythm_flag = r.al;
            goto ppschk_exit;

        pps_check:;
            ppsdrv_check();
            if (r.carry) goto ppschk_01;

            pw.ppsdrv_flag = 1;
            if (pw.kp_rhythm_flag != 0xff)
                goto ppschk_exit;

            pw.kp_rhythm_flag = 0;
            goto ppschk_exit;

        ppschk_01:;
            pw.ppsdrv_flag = 0;
            if (pw.kp_rhythm_flag != 0xff)
                goto ppschk_exit;

            pw.kp_rhythm_flag = 1;
            goto ppschk_exit;

        ppschk_exit:;
            if (pw.message_flag == 0)
                goto ppschk_end;
            if (pw.ppsdrv_flag != 1)
                goto ppschk_end;
            print_mes(pw.mes_ppsdrv);

        ppschk_end:;

            if (pw.ppz != 0)
            {
                ppz8_check();
                if (r.carry) goto ppzchk_end;
                pw.ppz_call_seg = 1;
                r.ax = 0x410;
                ChipDatum cd = new ChipDatum(0x04, r.al, 0);
                ppz8em(cd);//.ReadStatus(r.al);//int ppz_vec
                r.ah = (byte)pw.int_level;
                r.ah += 8;
                if (r.al != r.ah)
                    goto ppzchk_next;
                //push es
                r.ax = 0x409;
                cd = new ChipDatum(0x04, r.al, 0);
                ppz8em(cd);//.ReadStatus(r.al);//int ppz_vec
                r.ax = 0;// r.es;
                         // pop es
                pw.ppz_call_ofs = r.bx;
                pw.ppz_call_seg = r.ax;
            ppzchk_next:;
                r.ax = 0x1901;
                cd = new ChipDatum(0x19, 0, r.al);
                ppz8em(cd);//.SetReleaseFlag(r.al);//int ppz_vec; 常駐解除禁止
                if (pw.message_flag == 0)
                {
                    mask_eoi_set();
                    return;
                }
                print_mes(pw.mes_ppz8);
                ppzchk_end:;
            }
        }


        //9030-9079
        //;------------------------------------------------------------------------------
        //;	MASK/EOIの出力先の設定
        //;------------------------------------------------------------------------------
        private void mask_eoi_set()
        {
            //なにもしない
        }



        //9080-9105
        //;==============================================================================
        //;	ppsdrv常駐CHECK
        //;==============================================================================
        public void ppsdrv_check()
        {
            r.carry = !pw.usePPSDRV;//PPSDRV常駐しています！
        }



        //9106-9132
        //;==============================================================================
        //;	ppz8常駐CHECK
        //;==============================================================================
        private void ppz8_check()
        {
            if (pw.ppz != 0)
            {
                r.carry = false;//PPZ8常駐しています！(TBD)
            }
        }



        //9133-9166
        //;==============================================================================
        //;	ＯＰＮ割り込み許可処理
        //;==============================================================================
        private void opnint_start()
        {
            if (pw.board == 0)
                goto not_opnint_start;// ; ボードがない

            //r.ax = r.cs;
            //r.es = r.ax;
            r.di = (ushort)pw.part1;
            r.cx = (ushort)pw.max_part1;//max_part1*type qq
            r.al = 0;
            do
            {
                pw.partWk[r.di++].Clear();
                r.cx--;
            } while (r.cx != 0); //; Partwork All Reset

            r.al--;
            pw.rhythmmask = 255;//; Rhythm Mask解除
            pw.rhydmy = r.al;//	;R part Dummy用
            pw.rd = pw.rdDmy;
            data_init();
            opn_init();
            r.dx = 0x07bf;//;07hPort Init
            opnset44();
            mstop();
            setint();
            r.al = (byte)pw.int_level;
            intset();
            if (pw.va != 0)
            {
                r.al = pc98.InPort(0x32);
                //jmp $+2
                r.al &= 0x7f;
                pc98.OutPort(0x32, r.al);
            }
            r.dx = 0x2983;
            opnset44();
        not_opnint_start:;
            return;
        }



        //9167-9189
        //;==============================================================================
        //;	OPN out for 088/188/288/388 INIT用
        //;		input ah  reg
        //;			al data
        //; dx port
        //;==============================================================================
        private void opnset_fmc()
        {
            if (pw.va == 0)
            {
                //pushf
                //cli

                r.cx = 256;
                do { r.cx--; } while (r.cx != 0);

                byte a = r.ah;
                r.ah = r.al;
                r.al = a;
                pc98.OutPort(r.dx, r.al);

                r.cx = 256;
                do { r.cx--; } while (r.cx != 0);

                r.dx += 2;
                a = r.ah;
                r.ah = r.al;
                r.al = a;
                pc98.OutPort(r.dx, r.al);

                r.dx -= 2;
                //popf
                //ret
            }
        }



        //9856-9894
        private void intset()
        {
            //不要?
        }



        //9982-10003
        //;==============================================================================
        //;	/D? option
        //;==============================================================================
        private void fmvd_set(string op)
        {
            char c = op[0];
            int n = 0;
            if(!int.TryParse(op.Substring(1),out n))
            {
                Log.WriteLine(LogLevel.ERROR, "/D オプションの解析に失敗しました");
            }
            switch(c)
            {
                case 'S':// DS option
                    pw.ssg_voldown = (byte)n;
                    pw._ssg_voldown = (byte)n;
                    break;
                case 'P':
                    pw.pcm_voldown = (byte)n;
                    pw._pcm_voldown = (byte)n;
                    break;
                case 'R':
                    pw.rhythm_voldown = (byte)n;
                    pw._rhythm_voldown = (byte)n;
                    break;
                case 'Z':
                    pw.ppz_voldown = (byte)n;
                    pw._ppz_voldown = (byte)n;
                    break;
                case 'F':// DF option
                    pw.fm_voldown = (byte)n;
                    pw._fm_voldown = (byte)n;
                    break;
                default:
                    Log.WriteLine(LogLevel.ERROR, "/D オプションの解析に失敗しました");
                    break;
            }
        }



        //10043-10105
        private void keycheck(string op)
        {
            char c = op[0];
            int n = 0;
            if (!int.TryParse(op.Substring(1), out n))
            {
                Log.WriteLine(LogLevel.ERROR, "/K オプションの解析に失敗しました");
            }
            switch (c)
            {
                case 'G':
                    r.al = (byte)n;
                    if (pw.va != 0)
                    {
                        r.al = r.ror(r.al, 1);
                        r.al = r.ror(r.al, 1);
                        r.al = r.ror(r.al, 1);
                        r.al &= 0b1110_0000;
                    }
                    pw.grph_sp_key = r.al;
                    break;
                case 'R':
                    r.al = (byte)n;
                    if (pw.va != 0)
                    {
                        r.al = r.ror(r.al, 1);
                        r.al = r.ror(r.al, 1);
                        r.al = r.ror(r.al, 1);
                        r.al &= 0b1110_0000;
                    }
                    pw.rew_sp_key = r.al;
                    break;
                case 'E':
                    r.al = (byte)n;
                    if (pw.va != 0)
                    {
                        r.al = r.ror(r.al, 1);
                        r.al = r.ror(r.al, 1);
                        r.al = r.ror(r.al, 1);
                        r.al &= 0b1110_0000;
                    }
                    pw.esc_sp_key = r.al;
                    break;
                default:
                    Log.WriteLine(LogLevel.ERROR, "/K オプションの解析に失敗しました");
                    break;
            }
        }



        //9921-9954
        //;==============================================================================
        //;	オプション処理
        //;		input cs:bx option_data
        //; ds:si command_line
        //; es pmd_segment
        //;==============================================================================
        private void set_option(string[] pmdOption)
        {
            if (pmdOption == null) return;
            for(int i=0;i<pmdOption.Length;i++)
            {
                string op = pmdOption[i].ToUpper();
                if (string.IsNullOrEmpty(op)) continue;
                if (op.Length < 1 || (op[0] != '/' && op[0] != '-')) continue;

                char c = op[1];//1文字目
                switch (c)
                {
                    case 'D'://音量
                        if (op.Length > 2) fmvd_set(op.Substring(2));
                        break;
                    case 'N'://ssgドラム
                        if(op.Length>2 && op[2]=='-') pw.kp_rhythm_flag = 1;
                        else pw.kp_rhythm_flag = 0;
                        break;
                    case 'P'://ppsdrv
                        if (op.Length > 2 && op[2] == '-')
                        {
                            ppsdrv_check();
                            pw.ppsdrv_flag = 1;
                        }
                        else pw.ppsdrv_flag = 0;
                        break;
                    case 'C'://no message
                        pw.message_flag = 0;
                        break;
                    case 'G':
                        int n = 0;
                        if (!int.TryParse(op.Substring(1), out n)) pw.ff_tempo = 250;
                        else pw.ff_tempo = (byte)n;
                        break;
                    case 'K':
                        if (op.Length > 2) keycheck(op.Substring(2));
                        break;
                    case 'H':
                    case '?'://help
                        throw new NotImplementedException();
                        break;
                    case 'M':
                    case 'V':
                    case 'E':
                    case 'F':
                    case 'I':
                    case 'W':
                    case 'A':
                    case 'S':
                    case 'R':
                    case 'Z':
                    case 'O':
                        Log.WriteLine(LogLevel.WARNING, string.Format("PMDDotNETは指定のオプションをサポートしません。無視します。({0})", op));
                        break;
                    default:
                        Log.WriteLine(LogLevel.ERROR, string.Format("オプションの解析に失敗しました。無視します。({0})", op));
                        break;
                }
            }
        }



        public void WriteDummy(ChipDatum cd)
        {
            switch (pw.currentWriter)
            {
                case 0:
                case 1:
                case 2:
                    WriteOPNARegister(cd);
                    break;
                case 3:
                    ppz8em(cd);
                    break;
            }
        }




    }
}
