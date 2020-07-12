using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Cache;
using System.Text;
using musicDriverInterface;

//;==============================================================================
//;	Professional Music Driver[P.M.D.] version 4.8
//;					FOR PC98(+ Speak Board)
//; By M.Kajihara
//;==============================================================================

namespace PMDDotNET.Driver
{
    public class PMD
    {
        private PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;
        private PPZDRV ppz = null;
        private PCMDRV86 pcmdrv86 = null;
        private PPSDRV ppsdrv = null;
        private EFCDRV efcdrv = null;

        public PMD(MmlDatum[] mmlData)
        {
            pw = new PW();
            pw.md = mmlData;
            r = new x86Register();
            pc98 = new Pc98();
            ppz = new PPZDRV();
            pcmdrv86 = new PCMDRV86();
            ppsdrv = new PPSDRV();
            efcdrv = new EFCDRV();

            Set_int60_jumptable();
            Set_n_int60_jumptable();
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
            r.cx = (ushort)pw.wait_clock;
            do
            {
                r.cx--;
            } while (r.cx > 0);
        }

        public void _waitP()
        {
            ushort p = r.cx;
            r.cx = (ushort)pw.wait_clock;
            do
            {
                r.cx--;
            }
            while (r.cx > 0);
            r.cx = p;
        }

        public void _rwait()//リズム連続出力用wait
        {
            ushort p = r.cx;
            r.cx = (ushort)(pw.wait_clock * 32);
            do
            {
                r.cx--;
            }
            while (r.cx > 0);
            r.cx = p;
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



        private void int60_main()
        {
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
                    ppz.ppz8_call();// ADPCMEmulate OFF
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
                    ppz.intrpt();// 常駐解除禁止
                    r.ah = 0;
                    ppz.intrpt();
                    r.ah = 6;
                    ppz.intrpt();
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
            if (pw.md[r.si].dat != pw.max_part2 + 1)
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

            //data_init2:

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
                pw.ZeroClearPartWk(pw.partWk[r.bx]);

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
            pw.omote_key = new byte[] { 0, 0, 0 };
            pw.omote_key1Ptr = 0;
            pw.omote_key2Ptr = 1;
            pw.omote_key3Ptr = 2;
            pw.ura_key = new byte[] { 0, 0, 0 };
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

            if (pw.board2 == 0)
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
                        ppz.ppz8_call();
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
            mstop();
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
                pcmdrv86.pcmmain();//; ADPCM/PCM(IN "pcmdrv.asm"/"pcmdrv86.asm")
            }


            if (pw.ppz != 0)
            {
                r.di = (ushort)pw.part10a;//offset part10a
                pw.partb = 0;
                ppz.ppzmain();

                r.di = (ushort)pw.part10b;//offset part10b
                pw.partb = 1;
                ppz.ppzmain();

                r.di = (ushort)pw.part10c;//offset part10c
                pw.partb = 2;
                ppz.ppzmain();

                r.di = (ushort)pw.part10d;//offset part10d
                pw.partb = 3;
                ppz.ppzmain();

                r.di = (ushort)pw.part10e;//offset part10e
                pw.partb = 4;
                ppz.ppzmain();

                r.di = (ushort)pw.part10f;//offset part10f
                pw.partb = 5;
                ppz.ppzmain();

                r.di = (ushort)pw.part10g;//offset part10g
                pw.partb = 6;
                ppz.ppzmain();

                r.di = (ushort)pw.part10h;//offset part10h
                pw.partb = 7;
                ppz.ppzmain();
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
            r.si = (ushort)pw.part_data_table[r.di]; //; si = PART DATA ADDRESS
            if (r.si == 0) return;

            if (pw.partWk[r.di].partmask != 0)
            {
                if (fmmain_nonplay()) goto mp10;
                else goto mnp_ret;
            }

            //; 音長 -1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //; KEYOFF CHECK & Keyoff
            if (pw.partWk[r.di].keyoff_flag != 3)//; 既にkeyoffしたか？
                goto mp0;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                goto mp0;

            keyoff();//; ALは壊さない
            pw.partWk[r.di].keyoff_flag = 0xff;//-1

        mp0:;//; LENGTH CHECK
            if (r.al != 0) goto mpexit;

            mp10:;
            pw.partWk[r.di].lfoswi &= 0xf7;//; Porta off

        mp1:;//; DATA READ

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al < 0x80) goto mp2;
            if (r.al == 0x80) goto mp15;

            //; ELSE COMMANDS
            commands();
            goto mp1;

        //; END OF MUSIC["L"があった時はそこに戻る]
        mp15:;
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) goto mpexit;

            //; "L"があった時
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            goto mp1;

        mp2:;//; F-NUMBER SET
            lfoinit();
            oshift();
            fnumset();

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            calc_q();

            //porta_return:;
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
                goto mnp_ret;
            pw.partWk[r.di].keyoff_flag = 2;
            goto mnp_ret;

        mpexit:;//; LFO & Portament & Fadeout 処理 をして終了
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
                goto mnp_ret;
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
                // pushf
                //    cli
                lfo_change();
                lfo();
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
                if (pw.fadeout_speed == 0) goto mnp_ret;
            }
            //vol_set:
            volset();
        mnp_ret:;
            r.al = pw.loop_work;
            r.al &= pw.partWk[r.di].loopcheck;
            pw.loop_work = r.al;
            _ppz();
            return;
        }



        //1211-1267
        //;==============================================================================
        //;	Q値の計算
        //;		break	dx
        //;==============================================================================
        private void calc_q()
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
        private bool fmmain_nonplay()
        {
            pw.partWk[r.di].keyoff_flag = 0xff;// -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return false;// goto mnp_ret;

            if ((pw.partWk[r.di].partmask & 2) != 0)//; bit1(FM効果音中？)をcheck
            {
                if (pw.fm_effec_flag == 0)//	; 効果音終了したか？
                {
                    pw.partWk[r.di].partmask &= 0xfd;//;bit1をclear
                    if (pw.partWk[r.di].partmask == 0) return true;// goto mp10;//;partmaskが0なら復活させる
                }
            }
            //fmmnp_1:
            do
            {
                do
                {
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;
                    if (r.al < 0x80) return fmmnp_3();
                    commands();
                } while (true);

                //fmmnp_2:
                //	; END OF MUSIC["L"があった時はそこに戻る]
                r.si--;
                pw.partWk[r.di].address = r.si;
                pw.partWk[r.di].loopcheck = 3;
                pw.partWk[r.di].onkai = 0xff;//-1
                r.bx = pw.partWk[r.di].partloop;
                if ((r.bx & r.bx) == 0) return fmmnp_4();
                //    ; "L"があった時
                r.si = r.bx;
                pw.partWk[r.di].loopcheck = 1;
            } while (true);
        }

        private bool fmmnp_3()
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
            return fmmnp_4();
        }

        private bool fmmnp_4()
        {
            pw.tieflag = 0;
            pw.volpush_flag = 0;
            return false;//	jmp mnp_ret
        }



        //1323-1459
        //;==============================================================================
        //;	ＳＳＧ音源 演奏 メイン
        //;==============================================================================
        //psgmain_ret:
        //	ret

        private void psgmain()
        {
            r.si = (ushort)pw.part_data_table[r.di]; //; si = PART DATA ADDRESS
            if (r.si == 0) return;

            if (pw.partWk[r.di].partmask != 0)
            {
                int ret = psgmain_nonplay();
                switch (ret)
                {
                    case 0: goto mnp_ret;
                    case 1: goto mp1cp;
                    case 2: goto mp2p;
                }
            }

            //; 音長 -1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //; KEYOFF CHECK & Keyoff
            if (pw.partWk[r.di].keyoff_flag != 3)//; 既にkeyoffしたか？
                goto mp0p;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                goto mp0p;

            keyoffp();//; ALは壊さない
            pw.partWk[r.di].keyoff_flag = 0xff;//-1

        mp0p:;//; LENGTH CHECK
            if (r.al != 0) goto mpexitp;

            pw.partWk[r.di].lfoswi &= 0xf7;//; Porta off

        mp1p:;//; DATA READ

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al < 0x80) goto mp2p;
            if (r.al == 0x80) goto mp15p;

            //; ELSE COMMANDS
            mp1cp:;
            commandsp();
            goto mp1p;

        //; END OF MUSIC["L"があった時はそこに戻る]
        mp15p:;
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) goto mpexitp;

            //; "L"があった時
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            goto mp1p;

        mp2p:;//; TONE SET
            lfoinitp();
            oshiftp();
            fnumsetp();

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            calc_q();

            //porta_returnp:
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
                goto mnp_ret;
            pw.partWk[r.di].keyoff_flag = 2;
            goto mnp_ret;

        mpexitp:;
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
                    if (pw.fadeout_speed == 0) goto mnp_ret;
                }
            }
            //volsp2:;
            volsetp();

        mnp_ret:;
            r.al = pw.loop_work;
            r.al &= pw.partWk[r.di].loopcheck;
            pw.loop_work = r.al;
            _ppz();
            return;
        }



        //1460-1502
        //;==============================================================================
        //;	ＳＳＧ音源演奏メイン：パートマスクされている時
        //;==============================================================================
        // 0 : goto mnp_ret
        // 1 : goto mp1cp;
        // 2 : goto mp2p;
        private int psgmain_nonplay()
        {
            pw.partWk[r.di].keyoff_flag = 0xff;// -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return 0;// goto mnp_ret;

            pw.partWk[r.di].lfoswi &= 0xf7;//;Porta off
                                           //psgmnp_1:
            do
            {
                do
                {
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;
                    if (r.al < 0x80) goto psgmnp_4;

                    if (r.al != 0xda) goto psgmnp_3;//;Portament?
                    ssgdrum_check();//;の場合だけSSG復活Check
                    if (r.carry) return 1;// goto mp1cp;//;復活の場合はメインの処理へ
                    psgmnp_3:;
                    commandsp();
                } while (true);

                //    ; END OF MUSIC["L"があった時はそこに戻る]
                //psgmnp_2:
                r.si--;
                pw.partWk[r.di].address = r.si;
                pw.partWk[r.di].loopcheck = 3;
                pw.partWk[r.di].onkai = 0xff;//-1
                r.bx = pw.partWk[r.di].partloop;

                if ((r.bx & r.bx) == 0)
                {
                    fmmnp_4();
                    return 0;
                }

                //    ; "L"があった時
                r.si = r.bx;
                pw.partWk[r.di].loopcheck = 1;
            } while (true);
        psgmnp_4:;
            ssgdrum_check();
            if (!r.carry)
            {
                fmmnp_3();
                return 0;
            }

            return 2;// goto mp2p;//; SSG復活
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
            r.si = (ushort)pw.part_data_table[r.di]; //; si = PART DATA ADDRESS
            if (r.si == 0) return;

            //; 音長 -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) goto mnp_ret;

            //rhyms0:	
            r.bx = (ushort)pw.rhyadr;
        rhyms00:;
            r.al = 0;//	mov al,[bx]
            r.bx++;

            if (r.al == 0xff) goto reom;
            if ((r.al & 0x80) != 0) goto rhythmon;

            pw.kshot_dat = 0;//; rest
            //rlnset:
            r.al = 0;// mov al,[bx]
            r.bx++;

            pw.rhyadr = r.bx;
            pw.partWk[r.di].leng = r.al;
            pw.partWk[r.di].keyon_flag++;

            fmmnp_4();
        mnp_ret:;
            r.al = pw.loop_work;
            r.al &= pw.partWk[r.di].loopcheck;
            pw.loop_work = r.al;
            _ppz();
            return;

        reom:;
            do
            {
                r.al = (byte)pw.md[r.si++].dat;
                if (r.al == 0x80) goto rfin;
                if (r.al < 0x80) break;
                commandsr();
            } while (true);

            //re00:
            pw.partWk[r.di].address = r.si;
            r.ah = 0;
            r.ax += r.ax;
            r.ax += (ushort)pw.radtbl;
            r.bx = r.ax;
            r.ax = 0;// mov ax,[bx]

            r.ax += (ushort)pw.mmlbuf;
            pw.rhyadr = r.ax;
            r.bx = r.ax;
            goto rhyms00;

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

            fmmnp_4();
            goto mnp_ret;
        }



        //1710-1742
        //;==============================================================================
        //;	各種特殊コマンド処理
        //;==============================================================================
        private void commands()
        {
            pw.currentCommandTable = cmdtbl;
            r.bx = 0;//offset cmdtbl
            command00();
        }

        private void commandsr()
        {
            pw.currentCommandTable = cmdtblr;
            r.bx = 0;//offset cmdtblr
            command00();
        }

        private void commandsp()
        {
            pw.currentCommandTable = cmdtblp;
            r.bx = 0;//offset cmdtblp
            command00();
        }

        private void command00()
        {
            if (r.al < pw.com_end)
                goto out_of_commands;
            
            r.bx = (byte)~r.al;
            if (pw.ppz != 0)
            {
                r.stack.Push(r.ax);
                _ppz();
                r.ax = r.stack.Pop();
            }

            pw.currentCommandTable[r.bx]();
            return;

        out_of_commands:;
            r.si--;
            pw.md[r.si].dat = 0x80; //;Part END
            return;
        }



        //3064-3081
        //;==============================================================================
        //;	ポルタメント計算なのね
        //;==============================================================================
        private void porta_calc()
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



        //3475-3496
        //;==============================================================================
        //;	T->t 変換
        //; input[tempo_d]
        //;		output[tempo_48]
        //;==============================================================================
        private void calc_tb_tempo()
        {
            //;	TEMPO = 112CH / [ 256 - TB] timerB -> tempo
            r.bl = 0;
            r.bl -= pw.tempo_d;
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
        }



        //3766-3809
        //;==============================================================================
        //;	LFO1<->LFO2 change
        //;==============================================================================
        private void lfo_change()
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



        //4205-4261
        //;==============================================================================
        //;	SHIFT[di] 分移調する
        //;==============================================================================
        private void oshift()
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
        spm1:;
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
            r.ch = (byte)((r.ch >> 1) | (byte)(r.ch << 7));//ror ch,1
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

        private void fnrest()
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
            r.ax >>= r.cl;//    shr ax,cl

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
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry)
                goto fmvs_01;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_01:;
            r.si++;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry)
                goto fmvs_02;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_02:;
            r.si++;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
            if (!r.carry)
                goto fmvs_03;

            pw.vol_tbl[r.si] = r.cl;

        fmvs_03:;
            r.si++;
            r.carry = (r.bl & 0x80) != 0;
            r.bl = (byte)((r.bl << 1) | (r.bl >> 7));
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
            r.dh +=(byte)pw.partWk[pw.partb].address;//; dh=FM Port Address
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry)
                goto fmvm_01;
            r.dl = pw.partWk[r.di].slot4;
            volset_slot();
        fmvm_01:;
            r.dh -= 8;
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry)
                goto fmvm_02;
            r.dl = pw.partWk[r.di].slot3;
            volset_slot();
        fmvm_02:;
            r.dh += 4;
            r.al = pw.vol_tbl[r.si++];//lodsb
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
            if (!r.carry)
                goto fmvm_03;
            r.dl = pw.partWk[r.di].slot2;
            volset_slot();
        fmvm_03:;
            r.carry = (r.bh & 0x80) != 0;
            r.bh = (byte)((r.bh << 1) | (r.bh >> 7));
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



        //4995-5035
        //;==============================================================================
        //;	ＦＭ ＫＥＹＯＮ
        //;==============================================================================
        private void keyon()
        {
            if (pw.partWk[r.di].onkai != 0xff) //-1
                goto ko1;
            keyon_ret:;
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
            r.al = pw.omote_key[r.bx];
            r.al |= pw.partWk[r.di].slotmask;
            if (pw.partWk[r.di].sdelay_c == 0)
                goto no_sdm;
            r.al &= pw.partWk[r.di].sdelay_m;
        no_sdm:;
            pw.omote_key[r.bx] = r.al;
            r.dl |= r.al;
            opnset44();
            return;

        ura_keyon:;
            if (pw.board2 != 0)
            {
                r.bx += 0;//offset ura_key1
                r.al = pw.ura_key[r.bx];
                r.al |= pw.partWk[r.di].slotmask;
                if (pw.partWk[r.di].sdelay_c == 0)
                    goto no_sdm2;
                r.al &= pw.partWk[r.di].sdelay_m;
            no_sdm2:;
                pw.ura_key[r.bx] = r.al;
                r.dl |= r.al;
                r.dl |= 0b0000_0100;//;Ura Port
                opnset44();
                return;
            }
        }



        //5087-5137
        //;==============================================================================
        //;	KEY OFF
        //; don't Break AL
        //;==============================================================================
        private void keyoff()
        {
            if (pw.partWk[r.di].onkai != 0xff)
                goto kof1;
            return;//; ｷｭｳﾌ ﾉ ﾄｷ

        kof1:;

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

        private void keyoffp()
        {
            if (pw.partWk[r.di].onkai != 0xff)
                goto kofp1;
            return;//; ｷｭｳﾌ ﾉ ﾄｷ

        kofp1:;
            if (pw.partWk[r.di].envf == 0xff)
                goto kofp1_ext;
            pw.partWk[r.di].envf = 2;
            return;

        kofp1_ext:;
            pw.partWk[r.di].eenv_count = 4;
            return;
        }



        //5336-5495
        //;==============================================================================
        //;	ＬＦＯ処理
        //;		Don't Break cl
        //;		output cy = 1    変化があった
        //;==============================================================================
        private void lfo()
        {
        lfop:;
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
            r.ax = (ushort)(r.al * r.ah); //; lfowave=5の場合 1step = step×｜step｜
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
            if (pw.partWk[r.di].mdepth != 0x80)
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
            if (pw.partWk[r.di].mdepth != 0x80)
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
            r.ax = 32767;//0x7fff

            pw.seed = r.ax;
            int ans = (ushort)(r.ax * r.cx);
            r.cx = 32767;
            r.ax = (ushort)(ans / r.cx);
            r.dx = (ushort)(ans % r.cx);

            return;
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
                goto lfo_exit;
            pw.partWk[r.di].porta_num = 0;//    mov porta_num[di],0	;ポルタメントは初期化

            if ((pw.tieflag & 1) == 0)//; ﾏｴ ｶﾞ & ﾉ ﾄｷ ﾓ INIT ｼﾅｲ｡
                goto lfin1;

            lfo_exit:;
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
            ppsdrv.intrpt();// ppsdrv keyoff

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
                        ppz.intrpt();// FIFO割り込み停止
                        r.ax = 0x0200;
                    ppz_off_loop:;
                        r.stack.Push(r.ax);
                        ppz.intrpt();// ppz keyoff
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
        private void opnset44()
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
        private void opnset46()
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
        private void get07()
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
                if (pw.int60flag != 0) { int60_error(); return; }
            }

            if (r.ah != 0xf) int60_jumptable[r.ah]();
            else
            {
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
                //mstop_f,//1
                //fout,//2
                //eff_on,//3
                //effoff,//4
                //get_ss,//;5
                //get_musdat_adr,//6
                //get_tondat_adr,//7
                //get_fv,//8
                //drv_chk,//9
                //get_status,// A
                //get_efcdat_adr,//B
                //fm_effect_on,//C
                //fm_effect_off,//D
                //get_pcm_adr,//E
                //pcm_effect,//F
                //get_workadr,//10
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

            //stb_ff:
            if (r.dl == pw.TimerB_speed) return;

            pw.TimerB_speed = r.dl;
            r.dh = 0x26;
            opnset44();
            return;
            //stb_ret:
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

    }
}
