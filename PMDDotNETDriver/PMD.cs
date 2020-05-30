using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using musicDriverInterface;

namespace PMDDotNET.Driver
{
    public class PMD
    {
        private PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;

        public PMD(MmlDatum[] mmlData)
        {
            pw = new PW();
            pw.md = mmlData;
            r = new x86Register();
            pc98 = new Pc98();

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
                if (pw.ademu!=0)
                {
                    r.ax = 0x1800;
                    pw.adpcm_emulate = r.al;
                    ppz8_call();// ADPCMEmulate OFF
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
                    ppz_vec();// 常駐解除禁止
                    r.ah = 0;
                    ppz_vec();
                    r.ah = 6;
                    ppz_vec();
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
            pw.omote_key1 = 0;
            pw.omote_key2 = 0;
            pw.omote_key3 = 0;
            pw.ura_key1 = 0;
            pw.ura_key2 = 0;
            pw.ura_key3 = 0;
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
                        ppz8_call();
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

            //oploop:
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
                if (r.ah != 0) goto oploop;
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
            ppsdrv();// ppsdrv keyoff

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
                    stop_86pcm();
                }
            pcm_ef:;
                if (pw.ppz != 0)
                {
                    if (pw.ppz_call_seg != 0)
                    {
                        r.ah = 0x12;
                        ppz_vec();// FIFO割り込み停止
                        r.ax = 0x0200;
                    ppz_off_loop:;
                        r.stack.Push(r.ax);
                        ppz_vec();// ppz keyoff
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
