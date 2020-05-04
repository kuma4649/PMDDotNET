using System;
using System.Collections.Generic;
using System.Text;
using musicDriverInterface;
using PMDDotNET.Common;

namespace PMDDotNET.Compiler
{
    public class mc
    {
        private Compiler compiler = null;
        private string[] args = null;
        private work work = null;
        private lc lc = null;
        private m_seg m_seg = null;
        private mml_seg mml_seg = null;
        private fnumdat_seg fnumdat_seg = null;
        private hs_seg hs_seg = null;
        private voice_seg voice_seg = null;

        //;==============================================================================
        //;
        //;	MML Compiler/Effect Compiler FOR PC-9801/88VA 
        //;							ver 4.8r
        //;
        //;==============================================================================
        //	.186

        public static string ver = "4.8s";// version
        public static int vers = 0x48;
        public static string date = "2020/01/22";// date

#if !hyouka
        public int hyouka = 0;//1で評価版(save機能cut)
#endif

#if !efc
        public int efc = 0;//ＦＭ効果音コンパイラかどうか
#endif

        //;==============================================================================
        //;
        //;	ＭＭＬコンパイラです．
        //;	MC filename[.MML](CR)
        //;	で，コンパイルして，filename.Mというファイルを作成します．
        //;
        //;	また、
        //;	MC filename[.MML] voice_filename[.FF]
        //;	で，音色データ付きのfilename.Mを作成します．
        //;	このデータは，音色データを読まなくても，単体で演奏できます．
        //;	また、音色定義コマンド(行頭@)が使用可能になります．
        //;
        //;	下のefcの値を１にすると、ＦＭ効果音コンパイラになります。
        //;	EFC filename[.EML] voice_filename[.FF] (CR)
        //;	で，コンパイルして，filename.EFCというファイルを作成します．
        //;
        //;==============================================================================

        public int olddat = 0;// v2.92以前のデータ作成
        public int split = 0;//音色データがＳＰＬＩＴ形式かどうか
        public int tempo_old_flag = 0;//テンポ処理 新旧flag
        public int pmdvector = 0x60;//VRTC.割り込み
        public static byte cr = 13;
        public static byte lf = 10;
        public static char eof = '$';

        //;==============================================================================
        //;	macros
        //;==============================================================================

        public void msdos_exit()
        {
            //プログラム終了(エラーコード0)
            throw new PMDDotNET.Common.PmdDosExitException("msdos_exit");
        }

        public void error_exit(int qq)
        {
            //プログラム終了(エラーコードqq)
            throw new PMDDotNET.Common.PmdErrorExitException(string.Format("error code:{0}", qq));
        }

        public void print_mes(string qq)
        {
            //コンソールへメッセージ表示
            string[] a = qq.Split(new string[] { "" + mc.cr + mc.lf }, StringSplitOptions.None);
            foreach (string s in a)
                musicDriverInterface.Log.WriteLine(musicDriverInterface.LogLevel.INFO, s);
        }

        public void print_chr(string qq)
        {
            //コンソールへ文字表示
            musicDriverInterface.Log.WriteLine(musicDriverInterface.LogLevel.INFO, qq);
        }

        public void print_line(string bx)
        {
            //コンソールへメッセージ表示(bx位置から0まで)
            musicDriverInterface.Log.WriteLine(musicDriverInterface.LogLevel.INFO, bx);
        }

        public void pmd(int qq)
        {
            //mov ah, qq
            //int pmdvector
            //endm
            //ベクターからファンクション呼び出し
        }

        public int mstart = 0;
        public int mstop = 1;
        public int fout = 2;
        public int efcon = 3;
        public int efcoff = 4;
        public int getss = 5;
        public int get_music_adr = 6;
        public int get_tone_adr = 7;
        public int getfv = 8;
        public int board_check = 9;
        public int get_status = 10;
        public int get_efc_adr = 11;
        public int fm_efcon = 12;
        public int fm_efcoff = 13;
        public int get_pcm_adr = 14;
        public int pcm_efcon = 15;
        public int get_workadr = 16;
        public int get_fmefc_num = 17;
        public int get_pcmefc_num = 18;
        public int set_fm_int = 19;
        public int set_efc_int = 20;
        public int get_effon = 21;
        public int get_joystick = 22;
        public int get_pcmdrv_flag = 23;
        public int set_pcmdrv_flag = 24;
        public int set_fout_vol = 25;
        public int pause_on = 26;
        public int pause_off = 27;
        public int ff_music = 28;
        public int get_memo = 29;


        //;==============================================================================
        //;	main program
        //;==============================================================================

        //code segment para public	'code'
        //assume cs:code,ss:stack

        //mc  proc

        public mc(Compiler compiler, string[] args, string srcBuf, byte[] ffBuf, work work, string[] kankyo_seg)
        {
            this.compiler = compiler;
            this.args = args;
            this.work = work;
            this.kankyo_seg = kankyo_seg;

            voice_seg = new voice_seg();
            m_seg = new m_seg();
            lc = new lc(this, work, m_seg);
            mml_seg = new mml_seg();
            mml_seg.mml_buf = srcBuf;
            voiceTrancer(ffBuf);
            setupComTbl();
            setupRcomtbl();
            fnumdat_seg = new fnumdat_seg();
            hs_seg = new hs_seg();
        }

        private void voiceTrancer(byte[] ffBuf)
        {
            voice_seg.voice_buf = new byte[8192];
            if (ffBuf == null || ffBuf.Length < 1) return;
            for(int i=0;i< ffBuf.Length;i++)
                voice_seg.voice_buf[i] = ffBuf[i];
        }

        //;==============================================================================
        //; 	compile start
        //;==============================================================================

        public MmlDatum[] compile_start()
        {
            print_mes(mml_seg.titmes);

            if (args == null || args.Length == 0)
            {
                usage();
                return null;
            }


            //コンパイルプロセス開始
            {
                int i = ReadOption();
                ReadMMLFileName(i);
                //ReadMML();//不要
                ChangeMMLToMFileName();
                TransVoiceDataFromPMD();
                get_ff();
                clear_voicetable();
                CheckPrgFlgOnEfc();
                InitVariableBuffer();
                SetFromEnvironment();
            }



            enmPass2JumpTable ret = enmPass2JumpTable.Pass1;

            do
            {
                ret = Jumper(ret);

                if (ret == enmPass2JumpTable.exit)
                    break;

            } while (true);

            List < MmlDatum > dst = new List<MmlDatum>();
            dst.Add(new MmlDatum(m_seg.m_start));
            for (int i = 0; i < m_seg.m_buf.Count; i++) dst.Add(m_seg.m_buf.Get(i));
            for (int i = 0; i < dst.Count; i++) m_seg.m_buf.Set(i, dst[i]);

            return m_seg.m_buf.GetByteArray();
        }

        private enmPass2JumpTable Jumper(enmPass2JumpTable ret)
        {
            Log.WriteLine(LogLevel.TRACE, string.Format("jp:{0}", ret));
            switch (ret)
            {
                //Pass1

                case enmPass2JumpTable.Pass1:
                    ret = Pass1();
                    break;

                case enmPass2JumpTable.mainLoopPass1:
                    ret = mainLoopPass1();
                    break;

                case enmPass2JumpTable.InitLoopCount:
                    ret = InitLoopCount();
                    break;



                //Pass2

                case enmPass2JumpTable.Pass2CompileStart:
                    ret = Pass2CompileStart();
                    break;
                case enmPass2JumpTable.cmloop:
                    ret = cmloop();
                    break;
                case enmPass2JumpTable.part_stadr_set:
                    ret = part_stadr_set();
                    break;
                case enmPass2JumpTable.cmloop2:
                    ret = cmloop2();
                    break;
                case enmPass2JumpTable.cloop:
                    ret = cloop();
                    break;
                case enmPass2JumpTable.part_end:
                    ret = part_end();
                    break;
                case enmPass2JumpTable.check_lopcnt:
                    ret = check_lopcnt();
                    break;

                case enmPass2JumpTable.hsset:
                    ret = hsset();
                    break;
                case enmPass2JumpTable.one_line_compile:
                    ret = one_line_compile();
                    break;
                case enmPass2JumpTable.rem_set:
                    ret = rem_set();
                    break;

                case enmPass2JumpTable.vdat_set:
                    ret = vdat_set();
                    break;
                case enmPass2JumpTable.nd_s_loop:
                    ret = nd_s_loop();
                    break;
                case enmPass2JumpTable.nd_s_exit:
                    ret = nd_s_exit();
                    break;

                case enmPass2JumpTable.memo_write:
                    ret = memo_write();
                    break;

                case enmPass2JumpTable.hscom_exit:
                    ret = hscom_exit();
                    break;

                case enmPass2JumpTable.forceReturn:
                    return ret;
                //throw new Exception("ここでreturnにはならないはず");

                case enmPass2JumpTable.olc0:
                    ret = olc0();
                    break;

                case enmPass2JumpTable.olc00:
                    ret = olc00();
                    break;

                case enmPass2JumpTable.olc02:
                    ret = olc02();
                    break;

                case enmPass2JumpTable.olc03:
                    ret = olc03();
                    break;

                case enmPass2JumpTable.ots002:
                    ret = ots002();
                    break;

                case enmPass2JumpTable.olc_skip2:
                    ret = olc_skip2();
                    break;

                case enmPass2JumpTable.skip_mml:
                    ret = skip_mml();
                    break;

                case enmPass2JumpTable.parset:
                    ret = parset();
                    break;

                case enmPass2JumpTable.vset:
                    ret = vset();
                    break;

                case enmPass2JumpTable.vsetm:
                    ret = vsetm();
                    break;

                case enmPass2JumpTable.vsetm1:
                    ret = vsetm1();
                    break;

                case enmPass2JumpTable.vss:
                    ret = vss();
                    break;

                case enmPass2JumpTable.vss2:
                    ret = vss2();
                    break;

                case enmPass2JumpTable.vss2m:
                    ret = vss2m();
                    break;

                case enmPass2JumpTable.lngrew:
                    ret = lngrew();
                    break;

                case enmPass2JumpTable.lng_dec:
                    ret = lng_dec();
                    break;

                case enmPass2JumpTable.tieset_2:
                    ret = tieset_2();
                    break;

                case enmPass2JumpTable.lngmul:
                    ret = lngmul();
                    break;

                case enmPass2JumpTable.p1c_fin:
                    line_skip();
                    ret = enmPass2JumpTable.mainLoopPass1;
                    break;

                case enmPass2JumpTable.rskip:
                    ret = rskip();
                    break;

                case enmPass2JumpTable.rtloop:
                    ret = rtloop();
                    break;

                case enmPass2JumpTable.rtlp2:
                    ret = rtlp2();
                    break;

            }

            return ret;
        }

        private enum enmPass2JumpTable
        {
            Pass1,
            mainLoopPass1,
            InitLoopCount,

            Pass2CompileStart,
            cmloop,
            part_stadr_set,
            cmloop2,
            cloop,
            part_end,
            check_lopcnt,

            hsset,
            one_line_compile,
            rem_set,

            vdat_set,
            memo_write,
            nd_s_loop,
            nd_s_exit,
            forceReturn,
            hscom_exit,
            olc0,
            olc00,
            olc02,
            olc03,
            ots002,
            olc_skip2,
            skip_mml,
            parset,

            vset,
            vsetm,
            vsetm1,
            vss,
            vss2,
            vss2m,

            lngrew,
            lng_dec,
            tieset_2,
            lngmul,

            p1c_fin,
            rskip,
            rtloop,
            rtlp2,

            exit,
            bunsan_end
        }



        //158-206
        //;==============================================================================
        //; 	コマンドラインから /optionの読みとり
        //;==============================================================================
        private int ReadOption()
        {
            mml_seg.part = 0;
            mml_seg.ff_flg = 0;
            mml_seg.x68_flg = 0;
            mml_seg.dt2_flg = 0;
            mml_seg.opl_flg = 0;

            mml_seg.save_flg = 1;
            mml_seg.memo_flg = 1;
            mml_seg.pcm_flg = 1;

#if	hyouka
            mml_seg.play_flg = 1;
            mml_seg.prg_flg = 2;
#else
            mml_seg.play_flg = 0;
            mml_seg.prg_flg = 0;
#endif

            string envVal = "";
            bool cry = search_env(mml_seg.mcopt_txt, kankyo_seg, out int index, out int col);
            if (cry)
            {
                envVal = kankyo_seg[index].Substring(col);
                if (get_option(envVal))
                {
                    print_mes(mml_seg.warning_mes);
                    print_mes(mml_seg.mcopt_err_mes);
                }
            }

            int i = 0;
            for (i = 0; i < args.Length; i++)
            {
                if (get_option(args[i])) break;
            }

            return i;
        }



        //207-252
        //;==============================================================================
        //; 	コマンドラインから.mmlのファイル名の取り込み
        //;==============================================================================
        private void ReadMMLFileName(int i)
        {
            mml_seg.mml_filename = args[i];
            if (mml_seg.mml_filename.LastIndexOf('.') == -1)
            {
#if efc
                mml_seg.mml_filename += ".EML";
#else
                mml_seg.mml_filename += ".MML";
#endif
            }

        }



        //253-337
        //;==============================================================================
        //; 	.mmlファイルの読み込み
        //;==============================================================================
        private void ReadMMLFile()
        {
            ;
        }



        //338-373
        //;==============================================================================
        //; 	.mmlを.mに変更して設定
        //;==============================================================================
        private void ChangeMMLToMFileName()
        {
#if !hyouka
#if efc
            m_seg.m_filename = mml_seg.mml_filename.Substring(0, mml_seg.mml_filename.LastIndexOf('.'))+".EFC";            
#else
            m_seg.m_filename = mml_seg.mml_filename.Substring(0, mml_seg.mml_filename.LastIndexOf('.')) + ".M";
#endif    
#endif
        }


        //374-429
        //;==============================================================================
        //;	音色データ領域を転送してくる（PMD常駐時）又はクリア
        //;==============================================================================
        private void TransVoiceDataFromPMD()
        {

            //PMDからもらってくる機能は省略

            mml_seg.pmd_flg = 0;
            if (voice_seg.voice_buf == null)
            {
                voice_seg.voice_buf = new byte[8192];
                for (int i = 0; i < voice_seg.voice_buf.Length; i++) voice_seg.voice_buf[i] = 0;
            }
        }



        //430-439
        //;==============================================================================
        //; 	コマンドラインから.ffのファイル名を取り込む
        //;==============================================================================
        private void get_ff()
        {
            //なにもしない
        }



        //440-453
        //;==============================================================================
        //; 	音色テーブルの初期化
        //;==============================================================================
        private void clear_voicetable()
        {
            for (int i = 0; i < mml_seg.prg_num.Length; i++)
            {
                mml_seg.prg_num[i] = 0;
            }
        }


        //454-463
        //;==============================================================================
        //; 	compile main
        //;==============================================================================
        private void CheckPrgFlgOnEfc()
        {
#if efc
            if (mml_seg.prg_flg == 1)
            {
                error(0, 28, 0);
            }
#endif
        }



        //464-485
        //;==============================================================================
        //;	変数バッファ/文字列offsetバッファ初期化
        //;==============================================================================
        private void InitVariableBuffer()
        {

            for (int i = 0; i < hs_seg.hsbuf2.Length; i++)
            {
                hs_seg.hsbuf2[i] = 0;
            }
            for (int i = 0; i < hs_seg.hsbuf3.Length; i++)
            {
                hs_seg.hsbuf3[i] = 0;
            }

            for (int i = 0; i < 128 * 8; i++)
            {
                work.ppzfile_buf[mml_seg.ppzfile_adr + i] = 0;
            }

        }



        //486-509
        //;==============================================================================
        //;	環境変数 user = , composer = , arranger = を検索して設定
        //;==============================================================================
        private void SetFromEnvironment()
        {
            //"USER=" 検索
            if (search_env(mml_seg.user_txt, kankyo_seg, out int index, out int col))
            {
                mml_seg.composer_adr = 0;
                mml_seg.composer_seg = kankyo_seg[index].Substring(col);
                mml_seg.arranger_adr = 0;
                mml_seg.arranger_seg = kankyo_seg[index].Substring(col);
            }

            //"COMPOSER=" 検索
            if (search_env(mml_seg.composer_txt, kankyo_seg, out index, out col))
            {
                mml_seg.composer_adr = 0;
                mml_seg.composer_seg = kankyo_seg[index].Substring(col);
            }

            //"ARRANGER=" 検索
            if (search_env(mml_seg.arranger_txt, kankyo_seg, out index, out col))
            {
                mml_seg.arranger_adr = 0;
                mml_seg.arranger_seg = kankyo_seg[index].Substring(col);
            }
        }



        //511-528
        //;==============================================================================
        //;	Pass1
        //;==============================================================================
        //;==============================================================================
        //;	Workの初期化(pass1)
        //;==============================================================================
        private enmPass2JumpTable Pass1()
        {
            work.si = 0;// mml_seg.mml_buf;

            mml_seg.part = 0;
            mml_seg.pass = 0;

            m_seg.mbuf_end = 0x7f;//check code

            mml_seg.skip_flag = 0;

            return enmPass2JumpTable.mainLoopPass1;
        }



        //529-574
        //;==============================================================================
        //;	Ｍain Ｌoop(pass1)
        //;==============================================================================
        private enmPass2JumpTable mainLoopPass1()
        {
            do
            {
                //p1cloop:
                mml_seg.linehead = work.si;

            p1c_next:
                char al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al == 0x1a) break;

                if (al <= ' ') goto p1c_fin;

                if (al == ';') goto p1c_fin;

                if (al == '`')
                {
                    mml_seg.skip_flag ^= 2;
                    goto p1c_next;
                }

                //p1c_no_skip:
                if ((mml_seg.skip_flag & 2) != 0) goto p1c_next;

                if (al == '!')
                {
                    return enmPass2JumpTable.hsset;
                }
                else if (al == '#')
                {
                    return macro_set();
                }
                else if (al == '@')
                {
                    return new_neiro_set();
                }
                else
                {
                    goto p1c_next;
                }

            p1c_fin:;
                line_skip();
            } while (work.si < mml_seg.mml_buf.Length);

            //p1_end:
            return enmPass2JumpTable.InitLoopCount;
        }



        //575-580
        //;==============================================================================
        //;	ループカウント初期化
        //;==============================================================================
        private enmPass2JumpTable InitLoopCount()
        {
            mml_seg.lopcnt = 0;
            return enmPass2JumpTable.Pass2CompileStart;
        }


        //581-606
        //;==============================================================================
        //;	Pass2 Compile Start
        //;==============================================================================
        private enmPass2JumpTable Pass2CompileStart()
        {

#if !efc || !olddat
            m_seg.m_start = (byte)(mml_seg.opl_flg * 2 | mml_seg.x68_flg);// 音源flag set
#endif

            work.di = (mml_seg.max_part + 1) * 2;//KUMA: ? -> ver48sで理解w
            work.di += 0;//offset m_buf
            if ((mml_seg.prg_flg & 1) != 0)
            {
                work.di += 2;
            }

            mml_seg.hsflag = 0;
            mml_seg.prsok = 0;

            mml_seg.part = 1;
            mml_seg.pass = 1;

            return enmPass2JumpTable.cmloop;
        }


        //607-641
        //;==============================================================================
        //;	音源の選択
        //;==============================================================================
        private enmPass2JumpTable cmloop()
        {
#if !efc
            int al = mml_seg.opl_flg;
            al |= mml_seg.x68_flg;
            if (al != 0)
            {
                //;==============================================================================
                //;	ＯＰＭ/ＯＰＬの場合
                //;==============================================================================
                if (mml_seg.part != 10)
                {
                    mml_seg.ongen = mml_seg.fm;
                }
                else
                {
                    mml_seg.ongen = mml_seg.pcm;
                }
            }
            else
            {
                //;==============================================================================
                //;	ＯＰＮの場合
                //;==============================================================================
                al = mml_seg.part;
                byte ah = 0;
                while (al >= 4)
                {
                    al -= 3;
                    ah++;
                }
                mml_seg.ongen = ah;
            }
#endif
            return enmPass2JumpTable.part_stadr_set;
        }



        //642-655
        //;==============================================================================
        //;	パートのスタートアドレスのセット
        //;==============================================================================
        private enmPass2JumpTable part_stadr_set()
        {
            int bx = mml_seg.part;
            bx--;
            bx *= 2;
            bx += 0;//offset m_buf

            int dx = work.di;
            dx -= 0;//offset m_buf

            MmlDatum ml = new MmlDatum((byte)dx);
            MmlDatum mh = new MmlDatum((byte)(dx >> 8));
            m_seg.m_buf.Set(bx, ml, mh);

            return enmPass2JumpTable.cmloop2;
        }



        //656-812
        //;==============================================================================
        //;	Workの初期化
        //;==============================================================================
        private enmPass2JumpTable cmloop2()
        {
            work.si = 0;//offset mml_buf
            cm_init();

            byte ah, al;

#if !efc
            if (mml_seg.part == 1)
            {
                if (mml_seg.fm3_partchr1 != 0)
                {

                    al = 0xc6;//FM3 拡張パートの指定(partA)
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                    mml_seg.fm3_ofsadr = work.di;

                    for (int cx = 0; cx < 3; cx++)
                    {
                        m_seg.m_buf.Set(work.di++, new MmlDatum(0));
                        m_seg.m_buf.Set(work.di++, new MmlDatum(0));
                    }
                }
            }

            if (mml_seg.part == mml_seg.pcmpart)
            {
                if (mml_seg.pcm_partchr[0] != 0)
                {

                    al = 0xb4;//PCM 拡張パートの指定(partJ)
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                    mml_seg.pcm_ofsadr = work.di;

                    for (int cx = 0; cx < 8; cx++)
                    {
                        m_seg.m_buf.Set(work.di++, new MmlDatum(0));
                        m_seg.m_buf.Set(work.di++, new MmlDatum(0));
                    }
                }
            }

            if (mml_seg.part != 7) goto not_partG;

            if (mml_seg.zenlen != 96)
            {
                ah = (byte)mml_seg.zenlen;
                al = 0xdf;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Zenlenが指定されている場合は Zコマンド発行(partG)
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
            }

#if !tempo_old_flag

            if (mml_seg.tempo != 0)
            {
                ah = 0xff;
                al = 0xfc;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Tempoが指定されている場合は tコマンド発行(partG)
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
                al = (byte)mml_seg.tempo;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));
            }

#endif

            if (mml_seg.timerb != 0)
            {
                ah = (byte)mml_seg.timerb;
                al = 0xfc;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Timerが指定されている場合は Tコマンド発行(partG)
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
            }

            if (mml_seg.towns_flg == 1) goto not_partG;// TOWNSは4.6f @@@@

            if (mml_seg.fm_voldown != 0)
            {
                ah = 0xfe;
                al = 0xc0;
                ah += (byte)mml_seg.fm_voldown_flag;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                al = (byte)mml_seg.fm_voldown;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Voldownが指定されている場合は DFコマンド発行(partG)
            }

            if (mml_seg.ssg_voldown != 0)
            {
                ah = 0xfc;
                al = 0xc0;
                ah += (byte)mml_seg.ssg_voldown_flag;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                al = (byte)mml_seg.ssg_voldown;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Voldownが指定されている場合は DSコマンド発行(partG)
            }

            if (mml_seg.pcm_voldown != 0)
            {
                ah = 0xfa;
                al = 0xc0;
                ah += (byte)mml_seg.pcm_voldown_flag;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                al = (byte)mml_seg.pcm_voldown;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Voldownが指定されている場合は DPコマンド発行(partG)
            }

            if (mml_seg.ppz_voldown != 0)
            {
                ah = 0xf5;
                al = 0xc0;
                ah += (byte)mml_seg.ppz_voldown_flag;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                al = (byte)mml_seg.ppz_voldown;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Voldownが指定されている場合は DZコマンド発行(partG)
            }

            if (mml_seg.rhythm_voldown != 0)
            {
                ah = 0xf8;
                al = 0xc0;
                ah += (byte)mml_seg.rhythm_voldown_flag;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                al = (byte)mml_seg.rhythm_voldown;
                m_seg.m_buf.Set(work.di++, new MmlDatum(al));//#Voldownが指定されている場合は DRコマンド発行(partG)
            }

        not_partG:;

            al = (byte)(mml_seg.opl_flg | mml_seg.x68_flg);
            if (al == 0)//OPM/OPL=DX/EXを却下
            {
                if (mml_seg.ext_detune != 0)
                {
                    if (mml_seg.ongen == mml_seg.psg)
                    {
                        ;//PSGのみ
                        ah = 0x01;
                        al = 0xcc;
                        m_seg.m_buf.Set(work.di++, new MmlDatum(al));//Extend Detune Set(Partの頭)
                        m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
                    }
                }

                if (mml_seg.ext_env != 0)
                {
                    if (mml_seg.ongen >= mml_seg.psg)//FMは却下
                    {
                        if (mml_seg.part != mml_seg.rhythm2)//Rhythmは却下
                        {
                            ah = 0x01;
                            al = 0xc9;
                            m_seg.m_buf.Set(work.di++, new MmlDatum(al));//Extend Envelope Set(Partの頭)
                            m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
                        }
                    }
                }
            }

            if (mml_seg.ext_lfo != 0)
            {
                if (mml_seg.part != mml_seg.rhythm2)//Rhythmは却下
                {
                    ah = 0x01;
                    al = 0xca;
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al));//Extend LFO Set(Partの頭)
                    m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                    if (mml_seg.towns_flg != 1)// TOWNSは4.6f @@@@
                    {
                        ah = 0x01;
                        al = 0xbb;
                        m_seg.m_buf.Set(work.di++, new MmlDatum(al));//Extend LFO Set(Partの頭)
                        m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
                    }
                }
            }

            if (mml_seg.towns_flg != 1)// TOWNSは4.6f @@@@
            {
                if (mml_seg.adpcm_flag != 255)
                {
                    if (mml_seg.part == mml_seg.pcmpart)//pcmのみ
                    {
                        ah = 0xf7;
                        al = 0xc0;
                        m_seg.m_buf.Set(work.di++, new MmlDatum(al));//ADPCM set(partの頭)
                        m_seg.m_buf.Set(work.di++, new MmlDatum(ah));

                        al = (byte)mml_seg.adpcm_flag;
                        m_seg.m_buf.Set(work.di++, new MmlDatum(al));
                    }
                }
            }

            if (mml_seg.transpose != 0)
            {
                if (mml_seg.part != mml_seg.rhythm2)//Rhythmは却下
                {
                    ah = (byte)mml_seg.transpose;
                    al = 0xb2;
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al));//Master Transpose(Partの頭)
                    m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
                }
            }

#endif
            return enmPass2JumpTable.cloop;
        }



        //813-874
        //;==============================================================================
        //;	Ｍain Ｌoop
        //;==============================================================================

        private enmPass2JumpTable cloop()
        {

        c_next:;

            byte ah_b, al_b;
            char al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

            if (al == 0x1a) 
                return enmPass2JumpTable.part_end;
            if (al < (' ' + 1)) goto c_fin;
            if (al == ';') goto c_fin;
            if (al != '`') goto c_no_skip;

            mml_seg.skip_flag ^= 2;
            goto c_next;

        c_no_skip:;
            if ((mml_seg.skip_flag & 2) == 0)
            {

                if (al == '!') return enmPass2JumpTable.hsset;
                if (al == '"')
                {
                    mml_seg.skip_flag ^= 1;

                    al_b = 0xc0;
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al_b));
                    al_b = (byte)mml_seg.skip_flag;
                    al_b &= 1;
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al_b));
                    goto c_next;
                }

                if (al == '\'')
                {
                    mml_seg.skip_flag &= 0xfe;
                    ah_b = 0x00;
                    al_b = 0xc0;
                    m_seg.m_buf.Set(work.di++, new MmlDatum(al_b));
                    m_seg.m_buf.Set(work.di++, new MmlDatum(ah_b));
                    goto c_next;
                }
            }

#if	efc
            work.si--;
            if (lngset(out int bx, out al_b)) goto c_fin;
            al_b++;
            if (al_b == mml_seg.part) goto one_line_compile;
#else
            al_b = (byte)al;
            ah_b = (byte)mml_seg.part;
            ah_b += (byte)(char)('A' - 1);
            if (al_b == ah_b) return enmPass2JumpTable.one_line_compile;
#endif
            goto c_next;

        c_fin:;
            line_skip();
            goto c_next;
        }



        //875-880
        //;==============================================================================
        //;	Error Checks
        //;==============================================================================
        private enmPass2JumpTable part_end()
        {
            work.si = 0;//エラー位置は不定
            return enmPass2JumpTable.check_lopcnt;
        }



        //881-1114
        private enmPass2JumpTable check_lopcnt()
        {
            do
            {
                if (mml_seg.lopcnt == 0) goto loop_ok;

                print_mes(mml_seg.warning_mes);
                put_part();
                print_mes(mml_seg.loop_err_mes);

                m_seg.m_buf.Set(work.di, new MmlDatum(0xf8));
                work.di++;

                work.bx = (work.bx & 0xff00) + 1;
                edl00();

            } while (true);

        loop_ok:;

            if (mml_seg.porta_flag != 0)
            {
                error('{', 9, work.si);
            }

            if (mml_seg.allloop_flag == 0) goto non_allloop_error;
            if (mml_seg.length_check1 == 0)
            {
                error('L', 10, work.si);
            }

        non_allloop_error:;

            //;==============================================================================
            //;	Part Endmark をセット
            //;==============================================================================
            m_seg.m_buf.Set(work.di, new MmlDatum(0x80));
            work.di++;
            //;==============================================================================
            //;	PART INC. & LOOP
            //;==============================================================================
            mml_seg.part++;
#if efc
            if (mml_seg.part < mml_seg.max_part + 2) goto cmloop;
#else
            if (mml_seg.part < mml_seg.max_part + 1) return enmPass2JumpTable.cmloop;
#endif

#if efc
            goto vdat_set;
#else
            if (mml_seg.part != mml_seg.max_part + 1) goto fm3_check;
            byte al = (byte)mml_seg.maxprg;
            if (mml_seg.towns_flg == 1)
            {
                al = 0;//TOWNSはRパート無し
            }

            //maxprg_towns_chk:;
            mml_seg.kpart_maxprg = al;//K partのmaxprgを保存
            al = (byte)mml_seg.deflng;
            mml_seg.deflng_k = al;//l 値を保存

        //;==============================================================================
        //; FM3 拡張パートがあればそれをcompile
        //;==============================================================================
        fm3_check:;
            if (mml_seg.fm3_ofsadr == 0)
            {
                goto pcm_check;// 無し
            }

            al = (byte)mml_seg.fm3_partchr1;
            mml_seg.fm3_partchr1 = 0;
            if (al != 0) goto fm3c_main;

            al = (byte)mml_seg.fm3_partchr2;
            mml_seg.fm3_partchr2 = 0;
            if (al != 0) goto fm3c_main;

            al = (byte)mml_seg.fm3_partchr3;
            mml_seg.fm3_partchr3 = 0;
            if (al == 0) goto pcm_check;

            fm3c_main:;
            int bx = mml_seg.fm3_ofsadr;
            int dx = work.di;
            dx -= 0;//offset m_buf
            m_seg.m_buf.Set(bx + 0, new MmlDatum((byte)dx));
            m_seg.m_buf.Set(bx + 1, new MmlDatum((byte)(dx >> 8)));
            bx += 2;
            mml_seg.fm3_ofsadr = bx;
            al -= (byte)(char)('A' - 1);
            mml_seg.part = al;
            mml_seg.ongen = mml_seg.fm;
            return enmPass2JumpTable.cmloop2;

        //;==============================================================================
        //;	PCM 拡張パートがあればそれをcompile
        //;==============================================================================
        pcm_check:;
            if (mml_seg.pcm_ofsadr == 0) goto rt;// 無し

            bx = 0;//offset pcm_partchr1

            //pcmc_loop:;
            for (int cx = 0; cx < 8; cx++)
            {
                al = (byte)mml_seg.pcm_partchr[bx];
                mml_seg.pcm_partchr[bx] = (char)0;
                bx++;
                if (al != 0) goto pcmc_main;
            }

            goto rt;

        pcmc_main:;
            bx = mml_seg.pcm_ofsadr;
            dx = work.di;
            dx -= 0;//offset m_buf
            m_seg.m_buf.Set(bx + 0, new MmlDatum((byte)dx));
            m_seg.m_buf.Set(bx + 1, new MmlDatum((byte)(dx >> 8)));
            bx += 2;
            mml_seg.pcm_ofsadr = bx;
            al -= (byte)(char)('A' - 1);
            mml_seg.part = al;
            mml_seg.ongen = mml_seg.pcm_ex;
            return enmPass2JumpTable.cmloop2;

        //;==============================================================================
        //;	R part Compile(efc.exeはしない)
        //;==============================================================================
        rt:;
            //;==============================================================================
            //;	Ｒパートのスタートアドレスをセット
            //;==============================================================================

            bx = 0;//offset m_buf
            bx += 2 * mml_seg.max_part;
            mml_seg.part = mml_seg.rhythm;
            mml_seg.ongen = mml_seg.pcm;
            dx = work.di;
            dx -= 0;//offset m_buf
            m_seg.m_buf.Set(bx + 0, new MmlDatum((byte)dx));
            m_seg.m_buf.Set(bx + 1, new MmlDatum((byte)(dx >> 8)));

            //;==============================================================================
            //;	リズムデータスタートアドレスを計算してｂｘへ
            //;==============================================================================
            work.bx = (byte)mml_seg.kpart_maxprg;
            work.bx += work.bx;
            work.bx += work.di;

            //;==============================================================================
            //;	Ｒパートコンパイル開始
            //;==============================================================================
            mml_seg.pass = 2;
            work.si = 0;//offset mml_buf
            cm_init();
            al = (byte)mml_seg.deflng_k;
            mml_seg.deflng = al;// l 値だけKパートから引用

            return enmPass2JumpTable.rtloop;
        }

        //;==============================================================================
        //;	データスタートアドレスをセット
        //;==============================================================================
        private enmPass2JumpTable rtloop()
        {
            if (mml_seg.kpart_maxprg != 0)
            {
                work.dx = work.bx;
                work.dx -= 0;//offset m_buf
                m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)work.dx));
                m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)(work.dx >> 8)));
                work.di += 2;
            }

            return enmPass2JumpTable.rtlp2;
        }

        private enmPass2JumpTable rtlp2()
        {
            char al_c = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (al_c == 0x1a) goto rend;

            //rtlp3:;
            do
            {
                al_c = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al_c < ' ' + 1) return enmPass2JumpTable.rskip;
                if (al_c == ';') return enmPass2JumpTable.rskip;
                if (al_c == '`')
                {
                    mml_seg.skip_flag ^= 2;
                    continue;
                }

                if ((mml_seg.skip_flag & 2) == 0)
                {
                    if (al_c == '!') return enmPass2JumpTable.hsset;
                }
                //rt_no_skip2:;
                if (mml_seg.kpart_maxprg == 0) return enmPass2JumpTable.rskip;
                if (al_c == 'R') break;
            } while (true);

            int aa = work.bx;
            work.bx = work.di;
            work.di = aa;

            //push bx
            int bx_p = work.bx;
            mml_seg.hsflag = 1;
            mml_seg.prsok = 0;
            enmPass2JumpTable ret = one_line_compile();
            //if (ret != enmPass2JumpTable.forceReturn)
            {
                do
                {
                    ret = Jumper(ret);

                    if (ret == enmPass2JumpTable.exit) break;
                    if (ret == enmPass2JumpTable.hscom_exit)
                    {
                        if (mml_seg.hsflag > 1)
                        {
                            ret = hscom_exit();
                        }
                        else
                        {
                            mml_seg.hsflag--;
                            break;
                        }
                    }

                } while (ret != enmPass2JumpTable.forceReturn);

                //throw new Exception("リターンできていない!");//KUMA:ここでは戻ってくる必要あり
            }
            work.bx = bx_p;
            //pop bx

            m_seg.m_buf.Set(work.di, new MmlDatum(0xff));
            work.di++;

            aa = work.bx;
            work.bx = work.di;
            work.di = aa;

            mml_seg.kpart_maxprg--;
            return enmPass2JumpTable.rtloop;

        rend:;
            work.di = work.bx;
            //rend2:;
            if (mml_seg.kpart_maxprg == 0) return enmPass2JumpTable.rem_set;

            work.dx = 29;
            work.si = 0;
            error(0, 29, work.si);

            throw new PMDDotNET.Common.PmdException();//ダミー：ここに来ることは無い
        }

        private enmPass2JumpTable rskip()
        {
            line_skip();
            return enmPass2JumpTable.rtlp2;
        }
#endif

        //1115-1152
        //;==============================================================================
        //;	Part Init.
        //;==============================================================================
        private void cm_init()
        {
            mml_seg.maxprg = 0;
            mml_seg.volss = 0;
            mml_seg.volss2 = 0;
            mml_seg.octss = 0;
            mml_seg.skip_flag = 0;
            mml_seg.tie_flag = 0;
            mml_seg.porta_flag = 0;
            mml_seg.ss_speed = 0;
            mml_seg.ss_depth = 0;
            mml_seg.ss_length = 0;
            mml_seg.ge_delay = 0;
            mml_seg.ge_depth = 0;
            mml_seg.ge_depth2 = 0;
            mml_seg.pitch = 0;
            mml_seg.master_detune = 0;
            mml_seg.detune = 0;
            mml_seg.def_a = 0;
            mml_seg.def_b = 0;
            mml_seg.def_c = 0;
            mml_seg.def_d = 0;
            mml_seg.def_e = 0;
            mml_seg.def_f = 0;
            mml_seg.def_g = 0;
            mml_seg.prsok = 0;
            mml_seg.alldet = 0x8000;
            mml_seg.octave = 3;
            mml_seg.deflng = mml_seg.zenlen / 4;
        }



        //1153-1189
        //;==============================================================================
        //;	Remark文箇所の設定
        //;==============================================================================
        private enmPass2JumpTable rem_set()
        {
#if !efc
            mml_seg.part = 0;
            if (mml_seg.towns_flg == 1) goto tclc_towns_chk;

            byte al = (byte)mml_seg.lc_flag;
            lc.lc_proc(al);

            work.si = 0;//offset max_all;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)lc.max_all));// TC/LC書き込み(4.8a～)
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(lc.max_all >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(lc.max_all >> 16)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(lc.max_all >> 24)));

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)lc.max_loop));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(lc.max_loop >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(lc.max_loop >> 16)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(lc.max_loop >> 24)));

        tclc_towns_chk:;
            work.bp = work.di;
            work.di += 2;
            al = (byte)vers;
            if (mml_seg.towns_flg != 1) goto vers_towns_chk;
            al = 0x46;//Townsは4.6f @@@@
        vers_towns_chk:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(al));
            al = 0xfe;// -2;
            m_seg.m_buf.Set(work.di++, new MmlDatum(al));// Remarks Check Code(0feh)
#endif
            return enmPass2JumpTable.vdat_set;
        }



#if !hyouka
        //1190-1215
        //;==============================================================================
        //;	Ｖ２．６以降用／音色データのセット
        //;==============================================================================
        private enmPass2JumpTable vdat_set()
        {
            if ((mml_seg.prg_flg & 1) == 0) return enmPass2JumpTable.memo_write;

            work.si = 0;//offset m_buf
            work.si += 2 * (mml_seg.max_part + 1);//KUMA:? -> v48sで理解w
            int dx = work.di;
            dx -= 0;//offset m_buf
            m_seg.m_buf.Set(work.si, new MmlDatum((byte)dx));
            m_seg.m_buf.Set(work.si + 1, new MmlDatum((byte)(dx >> 8)));
            work.bx = 0;//mml_seg.prg_num;
            work.si = 0;//offset voice_buf
#if split
            work.si++;
#endif

            work.al = 0;
            int cx = 256;

            if (mml_seg.opl_flg != 1) return enmPass2JumpTable.nd_s_loop;

            //;==============================================================================
            //; OPL用
            //;==============================================================================
            //nd_s_opl_loop:;
            do
            {
                if (mml_seg.prg_num[work.bx] == 0) goto nd_s_opl_00;

                m_seg.m_buf.Set(work.di, new MmlDatum(work.al));
                work.di++;

                for (int rep = 0; rep < 9; rep++)//１音色９ｂｙｔｅｓ
                {
                    m_seg.m_buf.Set(work.di, new MmlDatum(voice_seg.voice_buf[work.si]));
                    work.di++;
                    work.si++;
                }

                work.si += 16 - 9;
                goto nd_s_opl_01;

            nd_s_opl_00:;
                work.si += 16;
            nd_s_opl_01:;
                work.al++;
                work.bx++;

                cx--;
            } while (cx > 0);

            return enmPass2JumpTable.nd_s_exit;
        }



        //1248-1281
        //;==============================================================================
        //;	OPN用
        //;==============================================================================
        private enmPass2JumpTable nd_s_loop()
        {
            int cx = 256;
            do
            {
                if (mml_seg.prg_num[work.bx] == 0) goto nd_s_00;

                m_seg.m_buf.Set(work.di, new MmlDatum(work.al));
                work.di++;

                for (int rep = 0; rep < 25; rep++)//１音色２５ｂｙｔｅｓ
                {
                    m_seg.m_buf.Set(work.di, new MmlDatum(voice_seg.voice_buf[work.si]));
                    work.di++;
                    work.si++;
                }

                work.si += 32 - 25;
                goto nd_s_01;

            nd_s_00:;
                work.si += 32;
            nd_s_01:;
                work.al++;
                work.bx++;

                cx--;
            } while (cx > 0);
            return enmPass2JumpTable.nd_s_exit;
        }

        private enmPass2JumpTable nd_s_exit()
        {
            int ax = 0xff00;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8))); // 音色終了マーク

            return enmPass2JumpTable.memo_write;
        }
#endif



        //1282-1404
        //;==============================================================================
        //;	その他メモ系文字列の書込み
        //;==============================================================================
        private enmPass2JumpTable memo_write()
        {
#if !efc
            work.bx = 0;// mml_seg.ppzfile_adr;
            int cx = 3;// #PPZFile / #PPSFile / #PCMFile

            if (mml_seg.towns_flg == 1)
            {
                work.bx = mml_seg.ppsfile_adr;
                cx--;//Townsは4.6f @@@@
            }

            //ppz_towns_chk:;
            //memow_loop0:;
            int ax;
            string ret;
            byte[] bret;
            do
            {
                ax = work.di;
                ax -= 0;//offset m_buf
                switch (cx)
                {
                    case 3:
                        work.si = mml_seg.ppzfile_adr;// si<文字列先頭番地
                        mml_seg.ppzfile_adr = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく
                        break;
                    case 2:
                        work.si = mml_seg.ppsfile_adr;// si<文字列先頭番地
                        mml_seg.ppsfile_adr = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく
                        break;
                    case 1:
                        work.si = mml_seg.pcmfile_adr;// si<文字列先頭番地
                        mml_seg.pcmfile_adr = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく
                        break;
                }

                if (work.si != 0) goto memow_trans0;
                work.al = 0;
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.al));
                goto memow_exit0;
            memow_trans0:;
                ret = set_strings2();//小文字＞大文字変換付き
                bret = compiler.enc.GetSjisArrayFromString(ret);
                foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
                m_seg.m_buf.Set(work.di++, new MmlDatum(0));
            memow_exit0:;
                work.bx += 2;
                cx--;
            } while (cx > 0);//loop memow_loop0

            //	#Title
            work.si = mml_seg.title_adr;// si<文字列先頭番地
            ax = work.di;
            ax -= 0;//offset m_buf
            mml_seg.title_adr = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく
            if (work.si == 0)
            {
                work.al = 0;
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.al));
                goto memow_exit;
            }
            //memow_trans:
            ret = set_strings();
            bret = compiler.enc.GetSjisArrayFromString(ret);
            foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
            m_seg.m_buf.Set(work.di++, new MmlDatum(0));

        memow_exit:;
            work.bx += 2;

            //	#Composer
            work.si = mml_seg.composer_adr;// si<文字列先頭番地
            ax = work.di;
            ax -= 0;//offset m_buf
            mml_seg.composer_adr = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく
            if (work.si == 0)
            {
                if (mml_seg.composer_seg == null)
                {
                    work.al = 0;
                    m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.al));
                }
                else
                {
                    bret = compiler.enc.GetSjisArrayFromString(mml_seg.composer_seg);
                    foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
                    m_seg.m_buf.Set(work.di++, new MmlDatum(0));
                }
            }
            else
            {
                ret = set_strings();
                bret = compiler.enc.GetSjisArrayFromString(ret);
                foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
                m_seg.m_buf.Set(work.di++, new MmlDatum(0));
            }

            //memow_exit_composer:;
            work.bx += 2;
            //	#Arranger
            work.si = mml_seg.arranger_adr;// si<文字列先頭番地
            ax = work.di;
            ax -= 0;//offset m_buf
            mml_seg.arranger_adr = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく
            if (work.si == 0)
            {
                if (mml_seg.arranger_seg == null)
                {
                    work.al = 0;
                    m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.al));
                }
                else
                {
                    bret = compiler.enc.GetSjisArrayFromString(mml_seg.arranger_seg);
                    foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
                    m_seg.m_buf.Set(work.di++, new MmlDatum(0));
                }
            }
            else
            {
                ret = set_strings();
                bret = compiler.enc.GetSjisArrayFromString(ret);
                foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
                m_seg.m_buf.Set(work.di++, new MmlDatum(0));
            }

            //memow_exit_arranger:;
            work.bx += 2;
            //memow_loop2:;
            //	mov si,[bx]; si<文字列先頭番地
            int memoInd = 0;
            while (mml_seg.memo_adr[memoInd] != 0)
            {
                work.si = mml_seg.memo_adr[memoInd];// si<文字列先頭番地
                ax = work.di;
                ax -= 0;//offset m_buf
                mml_seg.memo_adr[memoInd] = ax;//[bx] に替わりに転送先のアドレス(ofs)を入れておく

                ret = set_strings();
                bret = compiler.enc.GetSjisArrayFromString(ret);
                foreach (byte b in bret) m_seg.m_buf.Set(work.di++, new MmlDatum(b));
                m_seg.m_buf.Set(work.di++, new MmlDatum(0));

                work.bx += 2;
                memoInd++;
            }
            //memow_allexit:;
            ax = work.di;
            ax -= 0;//offset m_buf

            m_seg.m_buf.Set(work.bp + 0, new MmlDatum((byte)ax));//KUMA: tagのアドレステーブルへのアドレスをセット
            m_seg.m_buf.Set(work.bp + 1, new MmlDatum((byte)(ax >> 8)));

            work.si = mml_seg.ppzfile_adr;//offset ppzfile_adr
            if (mml_seg.towns_flg == 1)
            {
                work.si = mml_seg.ppsfile_adr;//    mov si, offset ppsfile_adr		;Townsは4.6f @@@@
            }
            //memo_towns_chk:
            //memoofsset_loop:

            if (mml_seg.towns_flg == 0)
            {
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.ppzfile_adr));
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.ppzfile_adr >> 8)));
            }
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.ppsfile_adr));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.ppsfile_adr >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.pcmfile_adr));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.pcmfile_adr >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.title_adr));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.title_adr >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.composer_adr));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.composer_adr >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.arranger_adr));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.arranger_adr >> 8)));
            for (int i = 0; i < mml_seg.memo_adr.Length; i++)
            {
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)mml_seg.memo_adr[i]));
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(mml_seg.memo_adr[i] >> 8)));
                if (mml_seg.memo_adr[i] == 0) break;
            }

#endif
            return enmPass2JumpTable.exit;
        }



        //1562-1566
        private void line_skip()
        {
            do
            {
                work.si++;
            } while (work.si - 1 < mml_seg.mml_buf.Length
            && ((work.si - 1 < mml_seg.mml_buf.Length) ? mml_seg.mml_buf[work.si - 1] : 0x1a) != 0xa);
        }



        //1799-1905
        //;==============================================================================
        //;	FF File read
        //;		in.ds:si strings
        //;==============================================================================
        private void read_fffile()
        {
            mml_seg.ff_flg = 1;
            byte ah = 0;
            //int di = 0;//offset v_filename
            voice_seg.v_filename = "";

            //g_vfn_loop:;
            do
            {
                char al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
                if (al == ' ') break;
                if (al == 13) break;
                if (al == '\\') ah = 0;
                //g_vfn_notyen:;
                if (al == '.') ah = 1;
                //g_vfn_store:;
                voice_seg.v_filename += al;
            } while (true);

            //g_vfn_next:;
            if (ah == 0)
            {
                voice_seg.v_filename += ".FF";
                if (mml_seg.opl_flg == 1)
                {
                    voice_seg.v_filename += "L";
                }
            }

            //vfn_ofs_notset:;

            //;==============================================================================
            //; 	.ffファイルの読み込み
            //;==============================================================================

            try
            {
                voice_seg.voice_buf = compiler.ReadFile(voice_seg.v_filename);
                if (voice_seg.voice_buf == null)
                    print_mes(mml_seg.warning_mes + string.Format(msg.get("E0200"), voice_seg.v_filename));// mml_seg.ff_readerr_mes);
                voiceTrancer(voice_seg.voice_buf);
            }
            catch
            {
            }


#if !hyouka
            mml_seg.prg_flg |= 1;
#endif
        }



        //1906-1967
        //;==============================================================================
        //;	オプション文字列読み取り
        //;==============================================================================
        private bool get_option(string val)
        {
            for (int col = 0; col < val.Length; col++)
            {
                char a = val[col];
                if (a == ' ') continue;
                if (a != '/' && a != '-')
                {
                    return true;//-,/で始まらないオプションはエラー
                }
                if (col + 1 == val.Length) return true;//-,/のみのオプションは無いのでエラー

                col++;
                string sw = val[col].ToString().ToUpper();
                switch (sw)
                {
                    case "V":
                        prgflg_set(val, ref col);
                        break;
                    case "P":
                        playflg_set(val, ref col);
                        break;
                    case "S":
                        saveflg_reset(val, ref col);
                        break;
                    case "M":
                        x68flg_set(val, ref col);
                        break;
                    case "T":
                        townsflg_set(val, ref col);
                        break;
                    case "N":
                        x68flg_reset(val, ref col);
                        break;
                    case "L":
                        oplflg_set(val, ref col);
                        break;
                    case "O":
                        memoflg_reset(val, ref col);
                        break;
                    case "A":
                        pcmflg_reset(val, ref col);
                        break;
                    case "C":
                        lcflg_set(val, ref col);
                        break;
                    default:
                        return true;//知らないオプションはエラー
                }
            }

            return false;
        }



        //1968-1986
        //;==============================================================================
        //;	/v,/vw option
        //;==============================================================================
        private void prgflg_set(string val, ref int col)
        {
            string c = col + 1 == val.Length ? "" : val[col + 1].ToString().ToUpper();

            if (c == "W")
            {
                col++;
#if !hyouka
                mml_seg.prg_flg |= 2;
#endif
            }
            else
            {
#if !hyouka
                mml_seg.prg_flg |= 1;
#endif
            }
        }



        //1987-1995
        //;==============================================================================
        //;	/p option
        //;==============================================================================
        private void playflg_set(string val, ref int col)
        {
#if !hyouka
            mml_seg.play_flg = 1;
#endif
        }



        //1996-2005
        //;==============================================================================
        //;	/s option
        //;==============================================================================
        private void saveflg_reset(string val, ref int col)
        {
#if !hyouka
            mml_seg.save_flg = 0;
            mml_seg.play_flg = 1;
#endif
        }



        //2006-2015
        //;==============================================================================
        //;	/m option
        //;==============================================================================
        private void x68flg_set(string val, ref int col)
        {
            mml_seg.towns_flg = 0;
            mml_seg.x68_flg = 1;
            mml_seg.opl_flg = 0;
            mml_seg.dt2_flg = 1;
        }



        //2016-2025
        //;==============================================================================
        //;	/n option
        //;==============================================================================
        private void x68flg_reset(string val, ref int col)
        {
            mml_seg.towns_flg = 0;
            mml_seg.x68_flg = 0;
            mml_seg.opl_flg = 0;
            mml_seg.dt2_flg = 0;
        }



        //2026-2035
        //;==============================================================================
        //;	/t option
        //;==============================================================================
        private void townsflg_set(string val, ref int col)
        {
            mml_seg.towns_flg = 1;
            mml_seg.x68_flg = 0;
            mml_seg.opl_flg = 0;
            mml_seg.dt2_flg = 0;
        }



        //2036-2042
        //;==============================================================================
        //;	/l option
        //;==============================================================================
        private void oplflg_set(string val, ref int col)
        {
            mml_seg.opl_flg = 1;
        }



        //2043-2049
        //;==============================================================================
        //;	/o option
        //;==============================================================================
        private void memoflg_reset(string val, ref int col)
        {
            mml_seg.memo_flg = 0;
        }



        //2050-2056
        //;==============================================================================
        //;	/a option
        //;==============================================================================
        private void pcmflg_reset(string val, ref int col)
        {
            mml_seg.pcm_flg = 0;

        }



        //2057-2063
        //;==============================================================================
        //;	/c option
        //;==============================================================================
        private void lcflg_set(string val, ref int col)
        {
            mml_seg.lc_flag = 1;
        }



        //2071-2127
        //;==============================================================================
        //;	マクロコマンド
        //;==============================================================================
        private enmPass2JumpTable macro_set()
        {
            int bx = work.si; //bxに現在のsiを保存
            string al = mml_seg.mml_buf[work.si++].ToString().ToUpper();//小文字＞大文字変換(1文字目)
            string ah = mml_seg.mml_buf[work.si].ToString().ToUpper();//小文字＞大文字変換(2文字目)

            //siを先にパラメータの位置まで進める
            if (move_next_param())
                error((int)'#', 6, work.si);//KUMA:パラメータがみつからなかった

            switch (al)
            {
#if !efc
                case "P":
                    pcmfile_set(ah, bx);
                    break;
                case "T":
                    title_set(ah, bx);
                    break;
                case "C":
                    composer_set();
                    break;
                case "A":
                    arranger_set(ah);
                    break;
                case "M":
                    memo_set();
                    break;
                case "Z":
                    zenlen_set();
                    break;
                case "L":
                    LFOExtend_set(ah);
                    break;
                case "E":
                    EnvExtend_set();
                    break;
                case "V":
                    VolDown_set();
                    break;
                case "J":
                    JumpFlag_set();
                    break;
#endif
                case "F":
                    FM3Extend_set(ah);
                    break;
                case "D":
                    dt2flag_set(ah);
                    break;
                case "O":
                    octrev_set(ah);
                    break;
                case "B":
                    bend_set();
                    break;
                case "I":
                    include_set();
                    break;
                default:
                    //ps_error:
                    error((int)'#', 7, work.si);
                    break;
            }

            //macro_normal_ret:
            bx = mml_seg.linehead;
            mml_seg.mml_buf = (bx != 0 ? mml_seg.mml_buf.Substring(0, bx) : "")
                + ";"
                + (bx + 1 < mml_seg.mml_buf.Length ? mml_seg.mml_buf.Substring(bx + 1) : "")
                ;//"#"を";"に変換

            return enmPass2JumpTable.p1c_fin;
        }


#if !efc
        //2129-2152
        //;==============================================================================
        //;	#PCMFile
        //;==============================================================================
        private void pcmfile_set(string ah, int bx)
        {
            if (ah == "P")
            {
                ppsfile_set(bx);
                return;
            }
            else if (ah != "C")
            {
                error((int)'#', 7, work.si);
            }

            string al = mml_seg.mml_buf[bx + 2].ToString().ToUpper();//小文字＞大文字変換(3文字目)
            if (al != "M")
            {
                error((int)'#', 7, work.si);
            }

            al = mml_seg.mml_buf[bx + 3].ToString().ToUpper();//小文字＞大文字変換(4文字目)
            if (al == "V")
            {
                pcmvolume_set();
                return;
            }
            else if (al == "E")
            {
                pcmextend_set(bx);
                return;
            }
            else if (al != "F")
            {
                error((int)'#', 7, work.si);
            }

            //ppcfile_set:
            mml_seg.pcmfile_adr = work.si;
        }



        //2153-2168
        //;==============================================================================
        //;	#PCMVolume	Extend/Normal
        //;==============================================================================
        private void pcmvolume_set()
        {
            string al = mml_seg.mml_buf[work.si].ToString().ToUpper();//小文字＞大文字変換

            if (al == "N")
            {
                mml_seg.pcm_vol_ext = 0;
                return;
            }
            else if (al == "E")
            {
                mml_seg.pcm_vol_ext = 1;
                return;
            }

            error((int)'#', 7, work.si);//ps_error
        }



        //2169-2213
        //;==============================================================================
        //;	#PCMExtend
        //;==============================================================================
        private void pcmextend_set(int bx)
        {
#if !efc
            string al = mml_seg.mml_buf[bx + 3].ToString().ToUpper();//小文字＞大文字変換(4文字目)
            if (al == "F")
            {
                ppzfile_set();
                return;
            }

            if (mml_seg.mml_buf[work.si] < ' ')
            {
                error((int)'#', 6, work.si);//ps_error
            }

            char a = mml_seg.mml_buf[work.si++];
            if (partcheck(a))
            {
                error((int)'#', 6, work.si);//ps_error
            }

            int di = 0;//offset pcm_partchr1
            bx = 0;//offset _pcm_partchr1;in cs
            mml_seg.pcm_partchr[di] = a;
            lc._pcm_partchr[bx] = a;
            di++;
            bx++;

            for (int cx = 0; cx < 7; cx++)//1+7 = 8 parts
            {
                a = mml_seg.mml_buf[work.si++];
                if (partcheck(a))
                {
                    work.si--;
                    return;
                }
                mml_seg.pcm_partchr[di] = a;
                lc._pcm_partchr[bx] = a;
                di++;
                bx++;
            }

            return;
#else
            error((int)'#', 6, work.si);//ps_error
#endif
        }



        //2214-2220
        //;==============================================================================
        //;	#PPZFile
        //;==============================================================================
        private void ppzfile_set()
        {
            mml_seg.ppzfile_adr = work.si;
        }



        //2221-2235
        //;==============================================================================
        //;	#PPSFile
        //;==============================================================================
        private void ppsfile_set(int bx)
        {
            string al = mml_seg.mml_buf[bx + 2].ToString().ToUpper();//小文字＞大文字変換(3文字目)

            if (al == "C")
            {
                mml_seg.pcmfile_adr = work.si;//#PPC
                return;
            }
            else if (al == "Z")
            {
                pcmextend_set(bx);//#PPZ
                return;
            }
            else if (al != "S")
            {
                error((int)'#', 6, work.si);//ps_error
            }

            mml_seg.ppsfile_adr = work.si;
        }


        //2236-2250
        //;==============================================================================
        //;	#Title
        //;==============================================================================
        private void title_set(string ah, int bx)
        {
            if (ah == "E")//;#TEmpo
            {
                tempo_set();
                return;
            }

            if (ah == "R")//;#TRanspose
            {
                transpose_set();
                return;
            }

            string al = mml_seg.mml_buf[bx + 2].ToString().ToUpper();//小文字＞大文字変換(3文字目)

            if (al == "M")//#TIMer
            {
                tempo_set2();
                return;
            }

            mml_seg.title_adr = work.si;
        }



        //2251-2258
        //;==============================================================================
        //;	#Composer
        //;==============================================================================
        private void composer_set()
        {
            mml_seg.composer_adr = work.si;
            //   mov[composer_seg],0
        }

        private string GetString(string buf, int index)
        {
            string ret = buf.Substring(index);
            if (ret.IndexOf((char)0x1a) >= 0) return ret = ret.Substring(0, ret.IndexOf((char)0x1a));
            if (ret.IndexOf((char)0x0a) >= 0) return ret = ret.Substring(0, ret.IndexOf((char)0x0a));
            if (ret.IndexOf((char)0x0d) >= 0) return ret = ret.Substring(0, ret.IndexOf((char)0x0d));
            return ret;
        }


        //2259-2268
        //;==============================================================================
        //;	#Arranger
        //;==============================================================================
        private void arranger_set(string ah)
        {
            if (ah == "D")
            {
                adpcm_set(ah);
                return;
            }

            mml_seg.arranger_adr = work.si;
        }



        //2269-2288
        //;==============================================================================
        //;	#ADPCM	on/off
        //;==============================================================================
        private void adpcm_set(string ah)
        {
            string al = mml_seg.mml_buf[work.si++].ToString().ToUpper();//小文字＞大文字変換

            if (al != "O")
            {
                error('#', 7, work.si);
            }

            al = mml_seg.mml_buf[work.si++].ToString().ToUpper();//小文字＞大文字変換
            ah = ((char)1).ToString();
            if (al == "N")
            {
                mml_seg.adpcm_flag = 1;
                return;
            }

            ah = ((char)0).ToString();
            if (al != "F")
            {
                error('#', 7, work.si);
            }

            mml_seg.adpcm_flag = 1;//kuma:OFFでもflagたてる？
        }



        //2289-2300
        //;==============================================================================
        //;	#Memo
        //;==============================================================================
        private void memo_set()
        {
            int bx = -1;//offset memo_adr-2
            do
            {
                bx++;
            } while (mml_seg.memo_adr[bx] != 0);

            mml_seg.memo_adr[bx] = work.si;
        }



        //2301-2309
        //;==============================================================================
        //;	#Transpose
        //;==============================================================================
        private void transpose_set()
        {
            int bx = 0;
            byte al = 0;
            if (lngset(out bx, out al))
            {
                error('#', 7, work.si);
            }

            mml_seg.transpose = al;
        }



        //2310-2325
        //;==============================================================================
        //;	#Detune	Normal/Extend
        //;==============================================================================
        private void detune_select()
        {
            string al = mml_seg.mml_buf[work.si++].ToString().ToUpper();//小文字＞大文字変換
            if (al == "N")
            {
                mml_seg.ext_detune = 0;
                return;
            }
            else if (al == "E")
            {
                mml_seg.ext_detune = 1;
                return;
            }

            error('#', 7, work.si);
        }



        //2326-2343
        //;==============================================================================
        //;	#LFOSpeed	Normal/Extend
        //;==============================================================================
        private void LFOExtend_set(string ah)
        {
            if (ah == "O")
            {
                loopdef_set();
                return;
            }

            string al = mml_seg.mml_buf[work.si++].ToString().ToUpper();//小文字＞大文字変換
            if (al == "N")
            {
                mml_seg.ext_lfo = 0;
                return;
            }
            else if (al == "E")
            {
                mml_seg.ext_lfo = 1;
                return;
            }

            error('#', 7, work.si);
        }



        //2344-2352
        //;==============================================================================
        //;	#LoopDefault	n
        //;==============================================================================
        private void loopdef_set()
        {
            if (lngset(out int bx, out byte al))
            {
                error('#', 7, work.si);
            }

            mml_seg.loop_def = al;
        }



        //2353-2368
        //;==============================================================================
        //;	#EnvSpeed	Normal/Extend
        //;==============================================================================
        private void EnvExtend_set()
        {
            string al = mml_seg.mml_buf[work.si++].ToString().ToUpper();//小文字＞大文字変換
            if (al == "N")
            {
                mml_seg.ext_env = 0;
                return;
            }
            else if (al == "E")
            {
                mml_seg.ext_env = 1;
                return;
            }

            error('#', 7, work.si);
        }



        //2369-2455
        //;==============================================================================
        //;	#VolumeDown
        //;==============================================================================
        private void VolDown_set()
        {
            do
            {
                int bh = 0;// FSPR select bit clear
                byte bl = 0;// 絶対/相対flag clear
                string al;
                byte al_b;

                //voldown_loop:
                do
                {
                    al_b = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                    if (al_b < ((byte)'9' + 1)) break;
                    al = ((char)al_b).ToString().ToUpper();//小文字＞大文字変換

                    if (al == "F") bh |= 0x01;
                    else if (al == "S") bh |= 0x02;
                    else if (al == "P") bh |= 0x04;
                    else if (al == "R") bh |= 0x08;
                    else if (al == "Z") bh |= 0x10;
                    else error('#', 1, work.si);
                } while (true);

                //vd_noppz:
                //vd_numget:;
                work.si--;
                al = ((char)al_b).ToString();
                if (al != "+" && al != "-")
                    bl++;// 絶対指定

                //vd_numget2:;
                if (bh == 0)//KUMA:指定なしの場合はエラー
                    error('#', 1, work.si);

                getnum(out int bx_dmy, out byte dl);

                if ((bh & 0x01) != 0)
                {
                    mml_seg.fm_voldown = dl;
                    mml_seg.fm_voldown_flag = bl;
                }

                if ((bh & 0x02) != 0)
                {
                    mml_seg.ssg_voldown = dl;
                    mml_seg.ssg_voldown_flag = bl;
                }

                if ((bh & 0x04) != 0)
                {
                    mml_seg.pcm_voldown = dl;
                    mml_seg.pcm_voldown_flag = bl;
                }

                if ((bh & 0x08) != 0)
                {
                    mml_seg.rhythm_voldown = dl;
                    mml_seg.rhythm_voldown_flag = bl;
                }

                if ((bh & 0x10) != 0)
                {
                    mml_seg.ppz_voldown = dl;
                    mml_seg.ppz_voldown_flag = bl;
                }

                al_b = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
                if (al_b != ',')
                {
                    return;
                }

                work.si++;
            } while (true);
        }



        //2456-2465
        //;==============================================================================
        //;	#Jump
        //;==============================================================================
        private void JumpFlag_set()
        {
            if (lngset(out int bx, out byte al))
            {
                error('#', 6, work.si);
            }

            mml_seg.jump_flag = bx;
        }



        //2466-2480
        //;==============================================================================
        //;	#Tempo
        //;==============================================================================
        private void tempo_set()
        {
            if (lngset(out int bx, out byte al))
            {
                error('#', 6, work.si);
            }

            if (tempo_old_flag != 0)
            {
                timerb_get(al, out byte dl);
                mml_seg.timerb = dl;
            }
            else
            {
                mml_seg.tempo = al;
            }

        }



        //2481-2490
        //;==============================================================================
        //;	#Timer
        //;==============================================================================
        private void tempo_set2()
        {
            if (lngset(out int bx, out byte al))
            {
                error('#', 6, work.si);
            }

            mml_seg.timerb = al;
        }



        //2491-2505
        //;==============================================================================
        //;	#Zenlength
        //;==============================================================================
        private void zenlen_set()
        {
            if (lngset(out int bx, out byte al))
            {
                error('#', 6, work.si);
            }

            mml_seg.zenlen = al;
            mml_seg.deflng = al / 4;
        }

#endif



        //2506-2541
        //;==============================================================================
        //;	#FM3Extend
        //;==============================================================================
        private void FM3Extend_set(string ah)
        {
            if (ah == "I")
            {
                file_name_set();
                return;
            }
            if (ah == "F")
            {
                fffile_set();
                return;
            }
#if !efc
            if ((work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a) < ' ')
            {
                error('#', 6, work.si);
            }

            char al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
            if (partcheck(al))
            {
                //ps_error
                error('#', 7, work.si);
            }

            mml_seg.fm3_partchr1 = al;
            lc._fm3_partchr[0] = al;

            al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
            if (partcheck(al))
            {
                work.si--;
                return;
            }

            mml_seg.fm3_partchr2 = al;
            lc._fm3_partchr[1] = al;

            al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
            if (partcheck(al))
            {
                work.si--;
                return;
            }

            mml_seg.fm3_partchr3 = al;
            lc._fm3_partchr[2] = al;

            return;

#else
            //ps_error
            error('#', 7, work.si);
#endif
        }



        //2542-2566
        //;==============================================================================
        //;	#Filename
        //;==============================================================================
        private void file_name_set()
        {
#if !hyouka

            int di = m_seg.file_ext_adr;
            char al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;

            if (al != '.')
            {
                work.si--;
                m_seg.m_filename = "";
            }
            else
            {
                m_seg.m_filename = m_seg.m_filename.Substring(0, di);
            }

            //file_name_set_main:;
            do
            {
                m_seg.m_filename += work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;

                if ((work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a) == ';')
                {
                    break;
                }

            } while ((work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a) >= '!');

            //file_name_set_exit:;
            //al = (char)0;
            //    stosb
#endif
        }



        //2567-2577
        //;==============================================================================
        //;	#FFFile
        //;==============================================================================
        private void fffile_set()
        {
            read_fffile();

        }



        //2578-2601
        //;==============================================================================
        //;	#DT2flag on/off
        //;==============================================================================
        private void dt2flag_set(string ah)
        {
#if !efc

            if (ah == "E")
            {
                detune_select();
                return;
            }
#endif

            string al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a).ToString().ToUpper(); //小文字＞大文字変換
            if (al != "O")
            {
                //ps_error
                error('#', 7, work.si);
            }

            al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a).ToString().ToUpper(); //小文字＞大文字変換
            if (al == "N")
            {
                mml_seg.dt2_flg = 1;
                return;
            }
            if (al != "F")
            {
                //ps_error
                error('#', 7, work.si);
            }

            mml_seg.dt2_flg = 0;
            return;
            //dt2flag_norm:;
        }



        //2602-2621
        //;==============================================================================
        //;	#octave	rev/norm
        //;==============================================================================
        private void octrev_set(string ah)
        {
            if (ah == "P")
            {
                option_set();
                return;
            }

            string al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a).ToString().ToUpper(); //小文字＞大文字変換
            if (al == "R")
            {
                comtbl[ou00] = new Tuple<string, Func<enmPass2JumpTable>>("<", octup);
                comtbl[od00] = new Tuple<string, Func<enmPass2JumpTable>>(">", octdown);
                return;
            }
            if (al != "N")
            {
                //ps_error
                error('#', 7, work.si);
            }

            comtbl[ou00] = new Tuple<string, Func<enmPass2JumpTable>>(">", octup);
            comtbl[od00] = new Tuple<string, Func<enmPass2JumpTable>>("<", octdown);
            return;
        }



        //2622-2628
        //;==============================================================================
        //;	#Option
        //;==============================================================================
        private void option_set()
        {
            string val = "";
            char v = (char)0;
            do
            {
                v = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a); //小文字＞大文字変換
                val += v;
            } while (v < 0x20);

            get_option(val);
        }



        //2629-2638
        //;==============================================================================
        //;	#Bendrange
        //;==============================================================================
        private void bend_set()
        {
            if (lngset(out int bx, out byte al))
            {
                error('#', 6, work.si);
            }

            mml_seg.bend = al;
        }



        //2639-2801
        //;==============================================================================
        //;	#Include
        //;==============================================================================
        private void include_set()
        {
            //;------------------------------------------------------------------------------
            //;	ファイル名の取り込み
            //;------------------------------------------------------------------------------
            //push es
            //push di
            //mov ax, ds
            //mov es, ax
            //assume es:mml_seg

            mml_seg.mml_filename2 = set_strings();

            int p_si = work.si;//SI= CR位置 を保存
            work.si += 2;// SI= 次の行の先頭位置(に読み込む予定)

            //;------------------------------------------------------------------------------
            //;	現在のMML残りをMMLバッファ末端に移動
            //;	I---------------I---------------I---------------I
            //;	mml_buf SI[mml_endadr]    mmlbuf_end
            //;			-------CX-------SI DI  にしてstd/movsw
            //;------------------------------------------------------------------------------

            string back = mml_seg.mml_buf.Substring(work.si);//移動する代わりにバックアップ
            mml_seg.mml_buf = mml_seg.mml_buf.Substring(0, work.si);
            work.si = p_si;

            //;------------------------------------------------------------------------------
            //;	Include開始check codeをMMLに書く
            //;------------------------------------------------------------------------------
            p_si = work.si;
            work.si += 2;
            int di = work.si;
            mml_seg.mml_buf += (char)1;//IncludeFile開始 CheckCode

            //;------------------------------------------------------------------------------
            //;	FileをOpenしてFile名をMMLに書く
            //;------------------------------------------------------------------------------
            //;------------------------------------------------------------------------------
            //;	Fileの読み込み
            //;------------------------------------------------------------------------------
            string inc = "";
            try
            {
                inc = compiler.ReadFileText(mml_seg.mml_filename2);

                while (inc.Length > 1 && inc[inc.Length - 1] == 0x1a)
                {
                    inc = inc.Substring(0, inc.Length - 1);
                }
            }
            catch
            {
                work.si = p_si;
                error('#', 3, work.si);
            }

            mml_seg.mml_buf += (char)0x0a;//LFの書込み = Line_skipに引っ掛かるようにする

            //;------------------------------------------------------------------------------
            //;	File終端のEOFを削ってCR/LFが無ければ書き足す
            //;------------------------------------------------------------------------------
            if (inc.Length > 1 && (inc[inc.Length - 2] != 13 || inc[inc.Length - 1] != 10))
            {
                inc += "" + (char)mc.cr + "" + (char)mc.lf;
            }

            //;------------------------------------------------------------------------------
            //;	Include->MainのCheckCodeの書込み
            //;------------------------------------------------------------------------------
            inc += "" + (char)2 + "" + (char)0xa;//IncludeFile終了 CheckCode

            //;------------------------------------------------------------------------------
            //;	転送した残りMMLを元に戻す
            //;------------------------------------------------------------------------------
            mml_seg.mml_buf += inc + back;
            work.si--;

            if (mml_seg.mml_buf.Length > 61 * 1024)//サイズが大き過ぎる
            {
                work.si = p_si;
                error('#', 18, work.si);
            }

        }



        //2801-2817
        //;==============================================================================
        //;	alの文字が使用中のパートかどうかcheck
        //;==============================================================================
        private bool partcheck(char al)
        {
            if (al < 'L')
            {
                return true;
            }

            if (al == 'R')
            {
                return true;
            }

            if (al >= (char)0x7f)
            {
                return true;
            }

            return false;
        }



        //2818-2860
        //;==============================================================================
        //;	文字列のセット
        //;		crlfが来るまで
        //;==============================================================================
        private string set_strings()
        {
            string ret = "";

            do
            {
                char al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al == 9 || al == 0x1b)//TAB or ESC
                {
                    ret += al;
                    continue;
                }

                if (al < ' ') break;

                ret += al;
            } while (true);

            //setstr_exit:;
            work.si--;
            return ret;
        }

        private string set_strings2()
        {
            string ret = "";
            //小文字＞大文字変換付き

            do
            {
                char al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al == 9 || al == 0x1b)//TAB or ESC
                {
                    ret += al;
                    continue;
                }
                if (al < ' ') break;

                ret += al.ToString().ToUpper();
            } while (true);

            //setstr_exit2:
            work.si--;
            return ret;
        }



        //2861-2891
        //==============================================================================
        //	次のパラメータに強制移動する
        //		1.space又はtabをsearch
        //		2.文字列をsearch
        //==============================================================================
        private bool move_next_param()
        {
            char al;

            do
            {
                al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al == (char)9) break;
                if (al == ' ') break;
                if (al < ' ') return true;
            } while (work.si < mml_seg.mml_buf.Length);

            if (work.si == mml_seg.mml_buf.Length)
            {
                return true;
            }

            do
            {
                al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al == (char)0x1b)//ESC
                {
                    work.si--;
                    return false;
                }
                if (al == (char)9) continue;
                if (al == ' ') continue;
                if (al < ' ') return true;
                break;
            } while (work.si < mml_seg.mml_buf.Length);

            work.si--;
            return false;
        }



        //2892-2974
        //;==============================================================================
        //;	MML 変数の設定
        //;==============================================================================
        private enmPass2JumpTable hsset()
        {
            //	push es
            int bx_p = work.bx;
            //    mov ax, hs_seg
            //    mov es, ax
            //    assume es:hs_seg

            bool cy = lngset(out int bx, out byte al);
            work.bx = bx;
            work.al = al;
            if (cy) goto hsset3;

            //hsset2:;
            int ax = work.al * 2;
            ax += 0;//offset hsbuf2
            hs_seg.currentBuf = hs_seg.hsbuf2;
            work.bx = ax;
            goto hsset_main;

        hsset3:;
            int si_pp = work.si;
            cy = search_hs3();
            work.si = si_pp;
            if (!cy) goto hsset_main;// 上書き
            work.bx = 0;//offset hsbuf3
            hs_seg.currentBuf = hs_seg.hsbuf3;

            int cx = 256;
            //hsset3_loop:;
            do
            {
                if ((hs_seg.currentBuf[work.bx] | hs_seg.currentBuf[work.bx + 1]) == 0) goto hsset3b;
                work.bx += hs_seg.hs_length;
                cx--;
            } while (cx > 0);
            error('!', 33, work.si);
        hsset3b:;

            int bx_pp = work.bx;
            int di_p = work.di;

            work.di = work.bx + 2;//    lea di,2[bx]
            cx = hs_seg.hs_length - 2;
            //hsset3b_loop:;
            char alc;
            do
            {
                alc = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (alc < '!') goto hsset3c;
                hs_seg.currentBuf[work.di++] = (byte)alc;
                cx--;
            } while (cx > 0);
            //hsset3b_loop2:;
            do
            {
                alc = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            } while (alc >= '!');
        hsset3c:;
            work.si--;

            work.di = di_p;
            work.bx = bx_pp;

        hsset_main:;

            int si_p = work.si;
            //hsset_loop:;
            do
            {
                alc = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (alc == 0xd)//cr
                {
                    goto hsset_fin;
                }
            } while (alc >= (char)(' ' + 1));
            hs_seg.currentBuf[work.bx + 0] = (byte)work.si;
            hs_seg.currentBuf[work.bx + 1] = (byte)(work.si >> 8);
        hsset_fin:;
            work.si = si_p;

            work.bx = bx_p;
            //    pop es
            //    assume es:m_seg

            work.al = (byte)mml_seg.pass;
            if (work.al == 0) return enmPass2JumpTable.p1c_fin;
#if !efc
            work.al--;
            if (work.al == 0)
            {
                line_skip();
                return enmPass2JumpTable.cloop;
            }
            return enmPass2JumpTable.rskip;
#else
            line_skip();
            return enmPass2JumpTable.cloop;
#endif
        }



        //2975-3087
        //;==============================================================================
        //;	音色の設定
        //;		@ num,alg,fb
        //;		  ar,dr,sr,rr,sl,tl,ks,ml,dt,[dt2,] ams
        //;		  ar,dr,sr,rr,sl,tl,ks,ml,dt,[dt2,] ams
        //;		  ar,dr,sr,rr,sl,tl,ks,ml,dt,[dt2,] ams
        //;		  ar,dr,sr,rr,sl,tl,ks,ml,dt,[dt2,] ams
        //;	(OPL)
        //;		@ num,alg,fb
        //;		  ar,dr,rr,sl,tl,ksl,ml,ksr,egt,vib,am
        //;		  ar,dr,rr,sl,tl,ksl,ml,ksr,egt,vib,am
        //;==============================================================================
        private enmPass2JumpTable new_neiro_set()
        {
            int di_p = work.di;
            int bx_p = work.bx;
            nns();
            work.bx = bx_p;
            work.di = di_p;

            return enmPass2JumpTable.p1c_fin;
        }

        private void nns()
        {
            if (mml_seg.opl_flg == 1)
            {
                opl_nns();
                return;
            }

            mml_seg.newprg_num = 0;
            for (int i = 0; i < 6; i++)
            {
                mml_seg.slot[0][i] = 0;
                mml_seg.slot[1][i] = 0;
                mml_seg.slot[2][i] = 0;
                mml_seg.slot[3][i] = 0;
            }

            get_param();
            mml_seg.newprg_num = work.al;

            Log.WriteLine(LogLevel.TRACE, string.Format("@ num:{0}", work.al));

            get_param();
            work.al &= 7;
            byte ch = work.al;
            get_param();
            work.al &= 7;
            work.al <<= 3;
            work.al |= ch;
            mml_seg.alg_fb = work.al;

            mml_seg.prg_name = "";

            work.di = 0;//offset slot_1
            slot_get(0);
            work.di = 0;//offset slot_2
            slot_get(1);
            work.di = 0;//offset slot_3
            slot_get(2);
            work.di = 0;//offset slot_4
            slot_get(3);

            work.bx = 0;//offset voice_buf
#if split
            work.bx++;
#endif

            work.dx = mml_seg.newprg_num * 32;
            work.dx += work.bx;

            //    assume es:voice_seg

            work.bx = 0;//offset slot_1
            slot_trans(0);
            work.dx++;

            work.bx = 0;//offset slot_3
            slot_trans(2);
            work.dx++;

            work.bx = 0;//offset slot_2
            slot_trans(1);
            work.dx++;

            work.bx = 0;//offset slot_4
            slot_trans(3);

            work.bx = 21 + work.dx;

            voice_seg.voice_buf[work.bx] = mml_seg.alg_fb;
            work.bx++;

            nns_pname_set();
        }



        private void nns_pname_set()
        {
            work.bp = 0;//offset prg_name
            int cx = 7;

            //nns_loop:
            for (int i = 0; i < 7; i++) voice_seg.voice_buf[work.bx + i] = 0;
            byte[] bret = compiler.enc.GetSjisArrayFromString(mml_seg.prg_name);

            while (work.bp < bret.Length && cx > 0)
            {
                voice_seg.voice_buf[work.bx] = bret[work.bp++];
                work.bx++;
                cx--;
            }
        }



        //3088-3151
        //;==============================================================================
        //;	OPL版音色設定
        //;==============================================================================
        private void opl_nns()
        {
            //push es
            int si_p = work.si;
            // assume es:mml_seg
            //work.di = 0;//offset oplbuf
            //int cx = 8;
            //int ax = 0;
            for (int i = 0; i < 16; i++) mml_seg.oplbuf[i] = 0;

            get_param();
            mml_seg.newprg_num = work.al;
            work.bx = 0;//offset oplprg_table
            work.di = 0;//offset oplbuf
            int cx = 2 + 11 * 2;
            //oplset_loop:;
            do
            {
                int bx_p = work.bx;
                get_param();
                work.bx = bx_p;
                work.al &= mml_seg.oplprg_table[work.bx + 1];//max
                byte cl = mml_seg.oplprg_table[work.bx + 2];//rot
                for (int i = 0; i < cl; i++) work.al = (byte)((work.al << 1) | ((work.al & 0x80) != 0 ? 1 : 0));
                mml_seg.oplbuf[work.di + mml_seg.oplprg_table[work.bx]] |= work.al;//設定

                work.bx += 3;
                cx--;
            } while (cx > 0);

            //    assume es:voice_seg
            work.si = 0;//offset oplbuf
            work.di = 0;//offset voice_buf
            work.dx = mml_seg.newprg_num;
            work.dx *= 16;
            work.di += work.dx;
            for (int i = 0; i < 9; i++) voice_seg.voice_buf[work.di++] = mml_seg.oplbuf[work.si++];
            work.si = si_p;
            work.bx = work.di;

            nns_pname_set();
        }



        //3152-3165
        //;==============================================================================
        //;	スロット毎のデータを転送
        //;==============================================================================
        private void slot_trans(int slot)
        {
            work.bp = work.dx;

            int cx = 6;
            //st_loop:
            do
            {
                work.al = mml_seg.slot[slot][work.bx];
                voice_seg.voice_buf[work.bp] = work.al;
                work.bx++;
                work.bp += 4;
                cx--;
            } while (cx > 0);
        }



        //3166-3228
        //;==============================================================================
        //;	各スロットの数値を読む
        //;==============================================================================
        private void slot_get(int slot)
        {
            get_param();// AR
            work.al &= 0b0001_1111;
            mml_seg.slot[slot][2] = work.al;

            get_param();// DR
            work.al &= 0b0001_1111;
            mml_seg.slot[slot][3] = work.al;

            get_param();// SR
            work.al &= 0b0001_1111;
            mml_seg.slot[slot][4] = work.al;

            get_param();// RR
            work.al &= 0b0000_1111;
            mml_seg.slot[slot][5] = work.al;

            get_param();// SL
            work.al &= 0b0000_1111;
            mml_seg.slot[slot][5] |= (byte)(work.al << 4);

            get_param();// TL
            work.al &= 0b0111_1111;
            mml_seg.slot[slot][1] = work.al;

            get_param();// KS
            work.al &= 0b0000_0011;
            mml_seg.slot[slot][2] |= (byte)(work.al << 6);

            get_param();// ML
            work.al &= 0b0000_1111;
            mml_seg.slot[slot][0] = work.al;

            get_param();// DT
            if ((work.al & 0x80) != 0)
            {
                work.al = (byte)-work.al;
                work.al &= 3;
                work.al |= 4;
            }
            work.al &= 0b0000_0111;
            mml_seg.slot[slot][0] |= (byte)(work.al << 4);

            if (mml_seg.dt2_flg != 0)
            {
                get_param();// DT2(for opm)
                work.al &= 0b0000_0011;
                mml_seg.slot[slot][4] |= (byte)(work.al << 6);
            }

            get_param();// AMS
            work.al &= 0b0000_0001;
            mml_seg.slot[slot][3] |= (byte)(work.al << 7);

        }



        //3229-3302
        //;==============================================================================
        //;	音色設定用パラメータの取り出し
        //;==============================================================================
        private void get_param()
        {

            char al;
            do
            {
                do
                {
                    al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

                    if (al == 9) continue;
                    if (al == ' ') continue;
                    if (al == ',') continue;
                    if (al < ' ' || al == ';')
                    {
                        //gp_skip
                        line_skip();
                        al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
                        if (al == 0x1a)
                        {
                            error('@', 6, work.si);
                        }
                        continue;
                    }
                    if (al != '`')
                    {
                        if ((mml_seg.skip_flag & 2) != 0) continue;
                        break;
                    }

                    mml_seg.skip_flag ^= 2;
                } while (true);

                bool cy;
                int bx;
                byte dl;
                //gp_no_skip:
                if (al == '=') goto get_vname;
                work.si--;
                if (al == '+' || al == '-')
                {
                    cy = getnum(out bx, out dl);
                    work.al = (byte)work.dx;
                    return;
                }
                cy = numget(out byte alb);
                if (cy)
                {
                    error('@', 1, work.si);
                }
                work.si--;
                //gp_gnm:
                cy = getnum(out bx, out dl);
                work.al = (byte)work.dx;
                return;

            get_vname:;
                work.si--;
            gsc_loop:;
                work.si++;
                char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
                if (ch == ' ') goto gsc_loop;
                if (ch == 9) goto gsc_loop;

                work.bp = 0;//offset prg_name
                int cx = 7;
                //gvn_loop:;
                do
                {
                    al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                    if (al == 9) break;
                    if (al == 13) break;
                    mml_seg.prg_name += al;
                    cx--;
                } while (cx > 0);
                //gv_skip:
                //mml_seg.prg_name[work.bp] = 0;
                //	jmp gp_skip
                line_skip();
                al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
                if (al == 0x1a)
                {
                    error('@', 6, work.si);
                }
            } while (true);
        }



        //3303-3415
        //;==============================================================================
        //;	一行 Compile
        //; INPUTS	-- ds:si to MML POINTER
        //;			-- es:di to M POINTER
        //;			-- [PART]
        //        to PART
        //;==============================================================================
        private enmPass2JumpTable one_line_compile()
        {
            char al = (char)0;
            do
            {
                do
                {
                    mml_seg.lastprg = 0;//Rパート用
                    al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                    if (al == 0xd)//cr
                    {
                        //olc_fin:;
                        work.si++;
                        //comend:;
                        if (mml_seg.hsflag != 0)
                            return enmPass2JumpTable.hscom_exit;
                        return enmPass2JumpTable.cloop;
                    }

                    if (al == ';')
                    {
                        //olc_skip:;//KUMA:ここに移動
                        line_skip();
                        if (mml_seg.hsflag != 0)
                            return enmPass2JumpTable.hscom_exit;
                        return enmPass2JumpTable.cloop;
                    }
                    if (al != '`') break;
                    mml_seg.skip_flag ^= 2;
                } while (true);

                //olc_no_skip:;
                if ((mml_seg.skip_flag & 2) != 0) return enmPass2JumpTable.olc_skip2;
            } while (al >= ' ' + 1);

            return enmPass2JumpTable.olc02;
        }

        private enmPass2JumpTable olc0()
        {
            char al = (char)0;
            mml_seg.prsok = al;
            return enmPass2JumpTable.olc02;
        }

        private enmPass2JumpTable olc02()
        {
            if (m_seg.mbuf_end != 0x7f)//check code
            {
                error(0x00, 19, work.si);// 容量オーバー
            }
            return enmPass2JumpTable.olc03;
        }

        private enmPass2JumpTable olc03()
        {
            do
            {
                work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (work.al == 9) continue;//tab
                if (work.al == (byte)' ') continue;
                if (work.al < (byte)' ') goto notend;
                if (work.al == (byte)';')
                {
                    //olc_skip:;//KUMA:ここに移動
                    line_skip();
                    if (mml_seg.hsflag != 0)
                        return enmPass2JumpTable.hscom_exit;
                    return enmPass2JumpTable.cloop;
                }
                if (work.al != (byte)'`') break;
                mml_seg.skip_flag ^= 2;
            } while (true);

            //olc_no_skip2:;
            if ((mml_seg.skip_flag & 2) != 0) return enmPass2JumpTable.olc_skip2;
            //notskp:;
            if (work.al != (byte)'"') goto nskp_01;
            mml_seg.skip_flag ^= 1;

            byte dh = 0xc0;
            byte dl = (byte)(mml_seg.skip_flag & 1);
            work.dx = dh * 0x100 + dl;
            return enmPass2JumpTable.parset;

        nskp_01:;
            if (work.al == (byte)'\'')
            {
                mml_seg.skip_flag &= 0xfe;

                dh = 0xc0;
                dl = 0x00;
                work.dx = dh * 0x100 + dl;
                return enmPass2JumpTable.parset;
            }
            //nskp_02:;
#if !efc
            if (work.al == (byte)'|') return enmPass2JumpTable.skip_mml;
#endif

            if (work.al == (byte)'/')
            {
                //part_end2:;//KUMA:ここに移動
                if (mml_seg.hsflag == 0) return enmPass2JumpTable.part_end;
                return enmPass2JumpTable.hscom_exit;
            }

        notend:;
            if (work.al != 13) return enmPass2JumpTable.olc00;
            //olc_fin:;
            work.si++;
            //comend:;
            if (mml_seg.hsflag != 0)
                return enmPass2JumpTable.hscom_exit;
            return enmPass2JumpTable.cloop;
        }

        private enmPass2JumpTable olc_skip2()
        {
            char al;
            do
            {
                al = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (al == 0xa)//lf
                {
                    if (mml_seg.hsflag != 0)
                        return enmPass2JumpTable.hscom_exit;
                    return enmPass2JumpTable.cloop;
                }
            } while (al != '`');

            mml_seg.skip_flag &= 0xfd;

            return enmPass2JumpTable.olc03;
        }



        //3416-3471
        //;==============================================================================
        //;	"|" command(Skip MML except selected Parts)
        //;==============================================================================
        private enmPass2JumpTable skip_mml()
        {
#if !efc
            byte ah = (byte)mml_seg.part;
            ah += (byte)('A' - 1);

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);

            if (ch < '!') return enmPass2JumpTable.olc03;// | only = Select All
            if (ch != '!') goto skm_loop;

            work.si++;

            //skm_reverse_loop:;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (ch == ah) goto part_not_found;
                if (ch == 13)
                {
                    work.si++;
                    if (mml_seg.hsflag != 0)
                        return enmPass2JumpTable.hscom_exit;
                    return enmPass2JumpTable.cloop;
                    //cr // line end
                }
            } while (ch >= (char)(' ' + 1));
            work.si--;
            goto part_found;

        skm_loop:;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (ch == ah) goto part_found;
                if (ch == 13)
                {
                    work.si++;
                    if (mml_seg.hsflag != 0)
                        return enmPass2JumpTable.hscom_exit;
                    return enmPass2JumpTable.cloop;
                    //cr // line end
                }
            } while (ch >= (char)(' ' + 1));

        //;==============================================================================
        //;	Not Found --- Skip to Next "|" or Next line
        //;==============================================================================
        part_not_found:;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (ch == 13)
                {
                    work.si++;
                    if (mml_seg.hsflag != 0)
                        return enmPass2JumpTable.hscom_exit;
                    return enmPass2JumpTable.cloop;
                    //cr // line end
                }
            } while (ch != '|');
            return enmPass2JumpTable.skip_mml;

        //;==============================================================================
        //;	Found --- Compile Next
        //;==============================================================================
        part_found:;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                if (ch == 13)
                {
                    work.si++;
                    if (mml_seg.hsflag != 0)
                        return enmPass2JumpTable.hscom_exit;
                    return enmPass2JumpTable.cloop;
                    //cr // line end
                }
            } while (ch >= (char)(' ' + 1));
#endif
            return enmPass2JumpTable.olc03;
        }


        //3472-3489
        //;==============================================================================
        //;	Command Jump
        //;==============================================================================
        private enmPass2JumpTable olc00()
        {
            work.bx = 0;//offset comtbl
            // olc1:
            do
            {
                if (((char)work.al).ToString() == comtbl[work.bx].Item1)
                {
                    break;
                }
                work.bx++;
                if (comtbl.Length == work.bx)
                {
                    error(0, 1, work.si);
                }
            } while (true);

            byte dh = (byte)work.al;
            work.dx = (dh * 0x100) | (byte)work.dx;
            if (comtbl[work.bx].Item2 != null)
            {
                Log.WriteLine(LogLevel.TRACE, string.Format("olc00:command:{0}", (char)dh));
                return comtbl[work.bx].Item2();
            }

            throw new PMDDotNET.Common.PmdErrorExitException(string.Format("まだ移植できてないコマンドを検出しました({0})", (char)dh));
        }



        //3490-3659
        //;==============================================================================
        //;	Command Table
        //;==============================================================================

        private int ou00 = 11;
        private int od00 = 12;
        private Tuple<string, Func<enmPass2JumpTable>>[] comtbl;

        private void setupComTbl()
        {
            comtbl = new Tuple<string, Func<enmPass2JumpTable>>[]{
                new Tuple<string, Func<enmPass2JumpTable>>("c"  , otoc)
                ,new Tuple<string, Func<enmPass2JumpTable>>("d" , otod)
                ,new Tuple<string, Func<enmPass2JumpTable>>("e" , otoe)
                ,new Tuple<string, Func<enmPass2JumpTable>>("f" , otof)
                ,new Tuple<string, Func<enmPass2JumpTable>>("g" , otog)
                ,new Tuple<string, Func<enmPass2JumpTable>>("a" , otoa)
                ,new Tuple<string, Func<enmPass2JumpTable>>("b" , otob)
                ,new Tuple<string, Func<enmPass2JumpTable>>("r" , otor)
                ,new Tuple<string, Func<enmPass2JumpTable>>("x" , otox)
                ,new Tuple<string, Func<enmPass2JumpTable>>("l" , lengthset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("o" , octset)
                ,new Tuple<string, Func<enmPass2JumpTable>>(">" , octup)
                ,new Tuple<string, Func<enmPass2JumpTable>>("<" , octdown)
                ,new Tuple<string, Func<enmPass2JumpTable>>("C" , zenlenset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("t" , tempoa)
                ,new Tuple<string, Func<enmPass2JumpTable>>("T" , tempob)
                ,new Tuple<string, Func<enmPass2JumpTable>>("q" , qset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("Q" , qset2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("v" , vseta)
                ,new Tuple<string, Func<enmPass2JumpTable>>("V" , vsetb)
                ,new Tuple<string, Func<enmPass2JumpTable>>("R" , neirochg)
                ,new Tuple<string, Func<enmPass2JumpTable>>("@" , neirochg)
                ,new Tuple<string, Func<enmPass2JumpTable>>("&" , tieset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("D" , detset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("[" , stloop)
                ,new Tuple<string, Func<enmPass2JumpTable>>("]" , edloop)
                ,new Tuple<string, Func<enmPass2JumpTable>>(":" , extloop)
                ,new Tuple<string, Func<enmPass2JumpTable>>("L" , lopset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("_" , oshift)
                ,new Tuple<string, Func<enmPass2JumpTable>>(")" , volup)
                ,new Tuple<string, Func<enmPass2JumpTable>>("(" , voldown)
                ,new Tuple<string, Func<enmPass2JumpTable>>("M" , lfoset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("*" , lfoswitch)
                ,new Tuple<string, Func<enmPass2JumpTable>>("E" , psgenvset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("y" , ycommand)
                ,new Tuple<string, Func<enmPass2JumpTable>>("w" , psgnoise)
                ,new Tuple<string, Func<enmPass2JumpTable>>("P" , psgpat)
                ,new Tuple<string, Func<enmPass2JumpTable>>("!" , hscom)
                ,new Tuple<string, Func<enmPass2JumpTable>>("B" , bendset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("I" , pitchset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("p" , panset)
                ,new Tuple<string, Func<enmPass2JumpTable>>("\\", rhycom)
                ,new Tuple<string, Func<enmPass2JumpTable>>("X" , octrev)
                ,new Tuple<string, Func<enmPass2JumpTable>>("^" , lngmul)
                ,new Tuple<string, Func<enmPass2JumpTable>>("=" , lngrew)
                ,new Tuple<string, Func<enmPass2JumpTable>>("H" , hardlfo_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("#" , hardlfo_onoff)
                ,new Tuple<string, Func<enmPass2JumpTable>>("Z" , syousetu_lng_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("S" , sousyoku_onp_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("W" , giji_echo_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("~" , status_write)
                ,new Tuple<string, Func<enmPass2JumpTable>>("{" , porta_start)
                ,new Tuple<string, Func<enmPass2JumpTable>>("}" , porta_end)
                ,new Tuple<string, Func<enmPass2JumpTable>>("n" , ssg_efct_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("N" , fm_efct_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("F" , fade_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("s" , slotmask_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("m" , partmask_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("O" , tl_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("A" , adp_set)
                ,new Tuple<string, Func<enmPass2JumpTable>>("0" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("1" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("2" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("3" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("4" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("5" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("6" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("7" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("8" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("9" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("%" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("$" , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("." , lngrew_2)
                ,new Tuple<string, Func<enmPass2JumpTable>>("-" , lng_dec)
                ,new Tuple<string, Func<enmPass2JumpTable>>("+" , tieset_2)
            };
        }



        //3660-3670
        //;==============================================================================
        //;	A command(ADPCM set)
        //;==============================================================================
        private enmPass2JumpTable adp_set()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xc0));
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf7));

            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            return enmPass2JumpTable.olc0;
        }



        //3671-3701
        //;==============================================================================
        //;	O command(TL set)
        //;==============================================================================
        private enmPass2JumpTable tl_set()
        {
            bool cy;
            int bx;
            byte dl;

            work.al = 0xb8;

            if (mml_seg.part == mml_seg.rhythm)
            {
                error('O', 17, work.si);
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            cy = getnum(out bx, out dl);
            work.al = (byte)work.bx;
            if (work.al >= 16)
            {
                error('O', 6, work.si);
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

            if (work.al != (byte)',')
            {
                error('O', 6, work.si);
            }

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == '+') goto tl_slide;
            if (ch != '-') goto tl_next;
            tl_slide:;
            byte d = (byte)m_seg.m_buf.Get(work.di - 1).dat;
            d |= 0xf0;
            m_seg.m_buf.Set(work.di-1, new MmlDatum(d));
        tl_next:;
            cy = getnum(out bx, out dl);
            work.al = dl;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            return enmPass2JumpTable.olc0;
        }



        //3702-3715
        //;==============================================================================
        //;	m command(part mask)
        //;==============================================================================
        private enmPass2JumpTable partmask_set()
        {
            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);

            if (cy)
            {
                error('m', 6, work.si);
            }

            if (work.al >= 2)
            {
                error('m', 2, work.si);
            }

            work.dx = 0xc000 + work.al;
            return enmPass2JumpTable.parset;
        }


        //3716-3743
        //;==============================================================================
        //;	s command(fm slot mask)
        //;==============================================================================
        private enmPass2JumpTable slotmask_set()
        {
            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == 'd') return slotdetune_set();
            if (ch == 'k') return slotkeyondelay_set();
            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xcf));
            work.ah = work.al;
            work.al = 0;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto not_car_set;
            work.si++;
            cy = lngset(out bx, out al);

        not_car_set:;
            work.ah <<= 4;
            work.ah &= 0xf0;
            work.al &= 0x0f;
            work.al |= work.ah;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;
        }



        //3744-3767
        //;==============================================================================
        //;	sd command(slot detune) / sdd command(slot detune 相対)
        //;==============================================================================
        private enmPass2JumpTable slotdetune_set()
        {
            bool cy;
            int bx;
            byte dl;

            work.al = 0xc8;
            work.si++;
            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != 'd') goto sds_set;
            work.al--;// al=0c7h
            work.si++;
        sds_set:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            cy = getnum(out bx, out dl);
            work.al = dl;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',')
            {
                error('s', 6, work.si);
            }
            work.si++;
            cy = getnum(out bx, out dl);
            work.al = (byte)work.bx;
            work.ah = (byte)(work.bx >> 8);
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));

            return enmPass2JumpTable.olc0;
        }



        //3768-3791
        //;==============================================================================
        //;	sk command(slot keyon delay)
        //;==============================================================================
        private enmPass2JumpTable slotkeyondelay_set()
        {
            bool cy;
            int bx;
            byte dl;

            work.al = 0xb5;
            work.si++;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            cy = getnum(out bx, out dl);
            work.al = dl;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto sks_err;

            work.si++;
            work.dx = ((byte)'s') * 0x100 + (byte)work.dx;
            get_clock();

        sks_exit:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        sks_err:;
            if (work.al == 0) goto sks_exit;
            error('s', 6, work.si);
            return enmPass2JumpTable.exit;//dummy
        }



        //3792-3803
        //;==============================================================================
        //;	n command(ssg effect)
        //;==============================================================================
        private enmPass2JumpTable ssg_efct_set()
        {
            bool cy;
            int bx;
            byte al;

            cy = lngset(out bx, out al);

            if (mml_seg.skip_flag != 0) return enmPass2JumpTable.olc03;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd4));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            return enmPass2JumpTable.olc0;
        }



        //3804-3815
        //;==============================================================================
        //;	N command(fm effect)
        //;==============================================================================
        private enmPass2JumpTable fm_efct_set()
        {
            bool cy;
            int bx;
            byte al;

            cy = lngset(out bx, out al);

            if (mml_seg.skip_flag != 0) return enmPass2JumpTable.olc03;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd3));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            return enmPass2JumpTable.olc0;
        }



        //3816-3855
        //;==============================================================================
        //;	F command(fadeout)
        //;==============================================================================
        private enmPass2JumpTable fade_set()
        {
            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == 'B') return fb_set();

            bool cy;
            int bx;
            byte al;

            cy = lngset(out bx, out al);

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd2));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            return enmPass2JumpTable.olc0;
        }

        //;==============================================================================
        //;	FB command(FeedBack set)
        //;==============================================================================
        private enmPass2JumpTable fb_set()
        {
            bool cy;
            int bx;
            byte dl;

            work.si++;

            work.al = 0xb6;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == '+') goto _fb_set;
            if (ch == '-') goto _fb_set;

            cy = getnum(out bx, out dl);

            if ((byte)work.bx >= 8)
            {
                error('F', 2, work.si);
            }

            work.al = (byte)work.bx;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        _fb_set:;
            cy = getnum(out bx, out dl);
            dl += 7;
            if (dl >= 15)
            {
                error('F', 2, work.si);
            }

            work.al = (byte)work.bx;
            work.al |= 0x80;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;
        }



        //3856-3869
        //;==============================================================================
        //;	"{" Command [Portament_start] / "{{" Command [分散和音開始]
        //;==============================================================================
        private enmPass2JumpTable porta_start()
        {
            if (mml_seg.skip_flag != 0) return enmPass2JumpTable.olc03;

            if (mml_seg.porta_flag != 0)
            {
                error('{', 9, work.si);
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xda));
            mml_seg.porta_flag = 1;

            //; 分散和音開始アドレスをセット  4.8r
            mml_seg.bunsan_start = 0;
            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != '{') return enmPass2JumpTable.olc0;
            work.si++;
            mml_seg.bunsan_start = work.di;

            //pst_end:
            return enmPass2JumpTable.olc0;
        }



        //3870-3937
        //;==============================================================================
        //;	"}" Command [Portament_end] / "}}" Command [分散和音終了]
        //;==============================================================================
        private enmPass2JumpTable porta_end()
        {
            bool cy;
            int bx;
            byte al;

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == '}') return bunsan_end();      // 4.8r

            if (mml_seg.skip_flag != 0) goto pe_skip;
            if (mml_seg.porta_flag != 1)
            {
                error('}', 13, work.si);
            }

            byte cch = (byte)m_seg.m_buf.Get(work.di - 5).dat;
            if (cch != 0xda)
            {
                error('}', 14, work.si);
            }
            cch = (byte)m_seg.m_buf.Get(work.di - 4).dat;
            if (cch == 0x0f)
            {
                error('}', 15, work.si);
            }
            cch = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            if (cch == 0x0f)
            {
                error('}', 15, work.si);
            }

            work.al = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            m_seg.m_buf.Set(work.di - 3, new MmlDatum(work.al));

            work.di -= 2;

            mml_seg.porta_flag = 0;
            cy = lngset2(out bx, out al);
            if ((work.bx & 0xff00) != 0)
            {
                error('}', 8, work.si);
            }
            lngcal();
            cy = futen();
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            if (cy)
            {
                error('}', 8, work.si);
            }

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto pe_exit;

            //; ディレイ指定
            work.si++;
            cy = lngset2(out bx, out al);
            if (cy)
            {
                error('}', 6, work.si);
            }
            lngcal();
            cy = futen();
            cch = (byte)m_seg.m_buf.Get(work.di - 1).dat;
            if (al >= cch)
            {
                error('}', 8, work.si);
            }
            work.dx = m_seg.m_buf.Get(work.di - 2).dat + m_seg.m_buf.Get(work.di - 1).dat * 0x100;
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)work.dx));
            m_seg.m_buf.Set(work.di + 2, new MmlDatum((byte)(work.dx >> 8)));
            work.dx = m_seg.m_buf.Get(work.di - 4).dat + m_seg.m_buf.Get(work.di - 3).dat * 0x100;//dh=start ontei
            m_seg.m_buf.Set(work.di - 1, new MmlDatum((byte)work.dx));
            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)(work.dx >> 8)));
            m_seg.m_buf.Set(work.di - 4, new MmlDatum((byte)(work.dx >> 8)));
            m_seg.m_buf.Set(work.di - 3, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di - 2, new MmlDatum(0xfb));// "&"
            cch = (byte)m_seg.m_buf.Get(work.di + 2).dat;
            cch -= work.al;
            m_seg.m_buf.Set(work.di + 2, new MmlDatum(cch));
            work.di += 3;
        pe_exit:;
            mml_seg.prsok = 9;//ポルタの音長
            return enmPass2JumpTable.olc02;
        pe_skip:;
            cy = lngset2(out bx, out al);
            if ((work.bx & 0xff00) != 0)
            {
                error('}', 8, work.si);
            }
            futen_skip();

            // ------ディレイスキップ処理を挿入 2019 / 12 / 28
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;
            cy = lngset2(out bx, out al);
            if (cy)
            {
                error('}', 6, work.si);
            }
            futen_skip();
            // ------ここまで
            return enmPass2JumpTable.olc03;
        }



        //;==============================================================================
        //;	"}}" Command[分散和音終了]	4.8r
        //;	{{cdeg
        //    }
        //}
        //lng[, cnt[, tie[, gate[, vol]]]]
        //;==============================================================================
        private enmPass2JumpTable bunsan_end()
        {
            bool cy;
            int bx;
            byte al, dl;

            work.si++;

            if (mml_seg.skip_flag != 0) return bunsan_skip();

            // 和音数のチェック
            work.bx = mml_seg.bunsan_start;
            if (work.bx == 0)
            {
                error('}', 34, work.si);
            }

            int cx = work.di;
            if (cx == work.bx)
            {
                error('}', 35, work.si);//音なし
            }
            cx -= work.bx;

            if ((cx & 1) != 0)
            {
                error('}', 35, work.si);// 間が奇数バイトでエラー
            }
            cx >>= 1;// cx = 分散和音数

            if (cx >= 17) error('}', 35, work.si);// 17音以上でエラー

            mml_seg.bunsan_count = (byte)cx;

            // bunsan_work に音階をセットしていく

            work.bp = 0;//offset bunsan_work

            //bend_loop:;
            do
            {
                work.al = (byte)m_seg.m_buf.Get(work.bx).dat;
                if ((work.al & 0x80) != 0) error('}', 35, work.si);//音階ではない

                mml_seg.bunsan_work[work.bp] = work.al;
                work.bx += 2;
                work.bp++;

            } while (work.di != work.bx);

            //	パラメータ取り込み
            cy = lngset2(out bx, out al);//prm1 全体音長
            if ((bx & 0xff00) != 0) error('}', 35, work.si);

            lngcal();
            cy = futen();
            if (cy) error('}', 35, work.si);

            mml_seg.bunsan_length = work.al;
            work.al = 0;
            mml_seg.bunsan_vol = 0;// 音量 def = ±0
            mml_seg.bunsan_gate = 0;//ゲート def = 0
            work.al++;
            mml_seg.bunsan_1cnt = work.al;//1cnt def = % 1
            mml_seg.bunsan_tieflag = work.al;//Tie def = ON(1)

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto bunsan_main;

            work.si++;

            cy = lngset2(out bx, out al);//prm2 1音符長
            if (cy) goto bunsan_prm3;
            if ((bx & 0xff00) != 0) error('}', 35, work.si);

            lngcal();
            cy = futen();
            if (cy) error('}', 35, work.si);

            mml_seg.bunsan_1cnt = work.al;

        bunsan_prm3:;
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto bunsan_main;

            work.si++;

            cy = lngset2(out bx, out al);//prm3 タイフラグ
            if (cy) goto bunsan_prm4;

            if ((bx & 0xff00) != 0) error('}', 35, work.si);
            if (work.al >= 2) error('}', 35, work.si);

            mml_seg.bunsan_tieflag = work.al;

        bunsan_prm4:;
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto bunsan_main;

            work.si++;

            cy = lngset2(out bx, out al);//prm4 ゲート長
            if (cy) goto bunsan_prm5;

            if ((bx & 0xff00) != 0) error('}', 35, work.si);
            if (work.al >= mml_seg.bunsan_length) error('}', 35, work.si);

            mml_seg.bunsan_gate = work.al;
            mml_seg.bunsan_length -= work.al;

        bunsan_prm5:;
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto bunsan_main;

            work.si++;

            cy = getnum(out bx, out dl);// prm5 音量±
            mml_seg.bunsan_vol = dl;

        //	分散和音展開
        bunsan_main:;
            work.di = mml_seg.bunsan_start;
            work.di--;//ポルタコマンドをつぶす

            //ループ回数チェック
            work.al = mml_seg.bunsan_1cnt;
            int ax = mml_seg.bunsan_count * work.al;//AX = 音符数 x 一音符の長さ = 1ループの長さ
            work.al = (byte)ax;
            if ((ax & 0xff00) != 0) error('}', 35, work.si);
            mml_seg.bunsan_1loop = work.al;

            cx = 0;
            cx = mml_seg.bunsan_length;

            int tmp = cx;// AX = 全体の長さ / CX = 1ループの長さ
            cx = ax;
            ax = tmp;
            if (cx >= ax) goto bunsan_last;//1ループに満たない場合

            work.al = (byte)(ax / cx);//AL = 全体の長さ \ 1ループの長さ = ループ回数
            work.ah = (byte)(ax % cx);
            if (mml_seg.bunsan_tieflag == 0) goto bunsan_setloop;

            if (work.ah != 0) goto bunsan_setloop;//タイありで割り切れた場合は
            work.al--;//ループ回数 -1

        bunsan_setloop:;
            if (work.al == 1) goto bunsan_nonloop;//ループ1回ならループ不要

            byte al_p = work.al;
            byte ah_p = work.ah;

            work.al = 0xf9;//"[" Loop Start
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.bx = work.di;// BX = 戻り先
            work.di += 2;

            int bx_p = work.bx;
            bunsan_set1loop();
            work.bx = bx_p;

            work.al = 0xf8;//"]" Loop End

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.ah = ah_p;
            work.al = al_p;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.al *= mml_seg.bunsan_1loop;
            mml_seg.bunsan_length -= work.al;
            ax = work.bx;
            ax -= 0;//offset m_buf

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

            ax = work.di - 4;  //    lea ax,[di-4]
            ax -= 0;//offset m_buf
            m_seg.m_buf.Set(work.bx + 0, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.bx + 1, new MmlDatum((byte)(ax >> 8)));//戻り先セット

            work.ah = (byte)(ax >> 8);
            work.al = (byte)ax;

            goto bunsan_last;

        //	1ループでいいのでループ不要
        bunsan_nonloop:;
            bunsan_set1loop();

            work.al = mml_seg.bunsan_1loop;
            mml_seg.bunsan_length -= work.al;

        //	ループ後最終セット
        bunsan_last:;
            if (mml_seg.bunsan_length == 0) goto bunsan_exit;// タイなしの場合ここで0になることがある

            work.bx = 0;//offset bunsan_work

        bunsan_last_loop:;
            work.al = mml_seg.bunsan_work[work.bx];
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.bx++;
            work.al = mml_seg.bunsan_1cnt;
            work.ah = mml_seg.bunsan_length;

            if (work.al >= work.ah) 
                goto bunsan_lastnote;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            mml_seg.bunsan_length -= work.al;

            if (mml_seg.bunsan_tieflag != 1) 
                goto bunsan_last_loop;

            work.al = 0xfb;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            goto bunsan_last_loop;

        bunsan_lastnote:;
            work.al = work.ah;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

        //	分散和音終了処理
        bunsan_exit:;
            work.ah = mml_seg.bunsan_gate;// Gateがある場合は休符追加
            if (work.ah == 0) goto bunsan_exit2;
            work.al = 0xf;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.al = work.ah;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

        bunsan_exit2:;
            work.al = 0;
            work.ah = 0;

            mml_seg.bunsan_start = 0;
            mml_seg.porta_flag = 0;

            return enmPass2JumpTable.olc0;
        }

        //	1ループ分音階をセット
        private void bunsan_set1loop()
        {
            work.bx = 0;//offset bunsan_work
            int cx = mml_seg.bunsan_count;//CX=音符数

            //bunsan_s1l_loop:;
            do
            {
                work.al = mml_seg.bunsan_work[work.bx];
                m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));//音階セット
                work.bx++;
                work.al = mml_seg.bunsan_1cnt;
                m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));//長さセット

                if (mml_seg.bunsan_tieflag == 1)
                {
                    work.al = 0xfb;//"&"セット
                    m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
                }
                //bunsan_s1l_fin:;
                cx--;
            } while (cx > 0);

            work.ah = mml_seg.bunsan_vol;
            if (work.ah == 0) goto bunsan_s1l_exit;
            work.al = 0xe3;//)x
            if ((work.ah & 0x80) == 0) goto bunsan_sl1_volset;
            work.al--;//(x
            work.ah = (byte)-work.ah;

        bunsan_sl1_volset:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.al = work.ah;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

        bunsan_s1l_exit:;
            return;

            //bunsan_error:			;分散和音／エラー時の処理
            //各々で処理
        }

        private enmPass2JumpTable bunsan_skip()//分散和音／スキップ時の処理
        {
            int bx;
            byte al, dl;

            lngset2(out bx, out al);
            futen_skip();
            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;
            lngset2(out bx, out al);
            futen_skip();
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;
            lngset2(out bx, out al);
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;
            lngset2(out bx, out al);
            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;
            getnum(out bx, out dl);

            //bskip_end:
            return enmPass2JumpTable.olc03;
        }



        //3938-3955
        //;==============================================================================
        //;	"~" Command[ＳＴＡＴＵＳの書き込み]
        //;	~[+,-] n
        //;==============================================================================
        private enmPass2JumpTable status_write()
        {
            bool cy;
            int bx;
            byte dl, al;

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == '-') goto sw_sweep;
            if (ch == '+') goto sw_sweep;

            cy = lngset(out bx, out al);
            work.dx = 0xdc00 + work.al;
            return enmPass2JumpTable.parset;

        sw_sweep:;
            cy = getnum(out bx, out dl);
            work.dx = 0xdb00 + dl;
            return enmPass2JumpTable.parset;
        }


        //3956-4000
        //;==============================================================================
        //;	"W" Command[擬似エコーの設定]
        //;	Wdelay[, +-depth][, tie / nextflag]
        //;==============================================================================
        private enmPass2JumpTable giji_echo_set()
        {
            bool cy;
            int bx;
            byte dl;

            work.dx = ((byte)'W') * 0x100 + (byte)work.dx;
            get_clock();

            mml_seg.ge_delay = work.al;
            if (work.al == 0) goto ge_cut;
            mml_seg.ge_tie = 0;
            mml_seg.ge_dep_flag = 0;
            mml_seg.ge_depth = -1;
            mml_seg.ge_depth2 = -1;

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != '%') goto ge_no_depf;

            work.si++;

            mml_seg.ge_dep_flag = 1;

        ge_no_depf:;
            cy = getnum(out bx, out dl);

            mml_seg.ge_depth = (sbyte)dl;
            mml_seg.ge_depth2 = (sbyte)dl;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;

            cy = lngset(out bx, out byte al);

            mml_seg.ge_tie = work.al;
            return enmPass2JumpTable.olc03;

        ge_cut:;

            mml_seg.ge_depth = work.al;
            mml_seg.ge_depth2 = work.al;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;

            cy = getnum(out bx, out dl);// dummy

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;

            cy = getnum(out bx, out dl);// dummy

            return enmPass2JumpTable.olc03;
        }



        //4001-4047
        //;==============================================================================
        //;	"S" Command[装飾音符の設定]
        //;	Sspeed[, depth]
        //;==============================================================================
        private enmPass2JumpTable sousyoku_onp_set()
        {
            bool cy;
            int bx;
            byte al,dl;

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == 'E') return ssgeg_set();

            work.dx = (byte)'S' * 0x100 + (byte)work.dx;
            get_clock();

            mml_seg.ss_speed = work.al;
            if (work.al == 0) goto ss_cut;

            mml_seg.ss_tie = 1;
            mml_seg.ss_depth = -1;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto ss_exit;

            work.si++;

            cy = getnum(out bx, out dl);

            mml_seg.ss_depth = (sbyte)dl;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto ss_exit;

            work.si++;

            cy = lngset(out bx, out al);

            mml_seg.ss_tie = al;

        ss_exit:;
            work.ah = (byte)mml_seg.ss_depth;
            if ((work.ah & 0x80) == 0) goto ss_exit_1;
            work.ah = (byte)-work.ah;

        ss_exit_1:;
            work.al = (byte)mml_seg.ss_speed;
            if (work.ah == 1) goto ss_exit_2;

            if (work.al * work.ah > 0xff)
            {
                error('S', 2, work.si);
            }
            work.al = (byte)(work.al * work.ah);

        ss_exit_2:;
            mml_seg.ss_length = work.al;
            return enmPass2JumpTable.olc03;

        ss_cut:;
            work.al = 0;
            mml_seg.ss_depth = 0;
            mml_seg.ss_length = 0;

            ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') return enmPass2JumpTable.olc03;

            work.si++;

            cy = lngset(out bx, out al);// dummy
            return enmPass2JumpTable.olc03;
        }



        //;==============================================================================
        //;	"SE" Command[SSGEG指定] →yコマンド変換
        //;	SEslot,num
        //;==============================================================================
        private enmPass2JumpTable ssgeg_set()
        {
            bool cy;
            int bx;
            byte al;

            //	mov dx,"S"*256+17
#if !efc
            if (mml_seg.ongen >= mml_seg.psg)//FMでなければエラー
            {
                error('S', 17, work.si);
            }
#endif
            work.al = (byte)mml_seg.opl_flg;// OPL/OPMではエラー
            if ((work.al | mml_seg.x68_flg) != 0)
            {
                error('S', 17, work.si);
            }

            work.si++;

            cy = lngset2(out bx, out al);
            if (cy)
            {
                error('S', 6, work.si);
            }

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',')
            {
                error('S', 6, work.si);
            }

            if (work.al == 0)
            {
                error('S', 2, work.si);
            }
            if (work.al >= 16)
            {
                error('S', 2, work.si);
            }

            work.si++;

            byte al_p = work.al;
            byte ah_p = work.ah;

            cy = lngset2(out bx, out al);//AL=num
            int cx = ah_p * 0x100 + al_p;//CL=slot

            if (cy)
            {
                error('S', 6, work.si);
            }

            //    mov dl,2

            if (work.al >= 16)
            {
                error('S', 2, work.si);
            }

            byte dh = (byte)mml_seg.part;// DHにFMのSSGEG reg取得
            dh--;
            if (dh < 6) goto sss_notfm3ex;
            dh = 2;//6以上はFM3exのみ

        sss_notfm3ex:;
            if (dh < 3) goto sss_notfm2;
            dh -= 3;//FM2ならpart -3

        sss_notfm2:;
            dh += 0x90;//SSGEG $90 + [part]
            if ((cx & 1) == 0)
            {
                cx = (cx & 0xff00) | (((byte)cx) >> 1);                // 指定slotに対応したyコマンド発行
                goto sss_slot2;
            }
            cx = (cx & 0xff00) | (((byte)cx) >> 1);
            sss_set1slot(dh);

        sss_slot2:;
            dh += 8;
            if ((cx & 1) == 0)
            {
                cx = (cx & 0xff00) | (((byte)cx) >> 1);
                goto sss_slot3;
            }
            cx = (cx & 0xff00) | (((byte)cx) >> 1);
            sss_set1slot(dh);

        sss_slot3:;
            dh -= 4;
            if ((cx & 1) == 0)
            {
                cx = (cx & 0xff00) | (((byte)cx) >> 1);
                goto sss_slot4;
            }
            cx = (cx & 0xff00) | (((byte)cx) >> 1);
            sss_set1slot(dh);

        sss_slot4:;
            dh += 8;
            if ((cx & 1) == 0)
            {
                cx = (cx & 0xff00) | (((byte)cx) >> 1);
                goto sss_fin;
            }
            cx = (cx & 0xff00) | (((byte)cx) >> 1);
            sss_set1slot(dh);

        sss_fin:;
            return enmPass2JumpTable.olc0;
        }

        private void sss_set1slot(byte dh)
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xef));
            m_seg.m_buf.Set(work.di++, new MmlDatum(dh));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
        }



        //4048-4057
        //;==============================================================================
        //;	"Z" Command[小節の長さ指定]
        //;==============================================================================
        private enmPass2JumpTable syousetu_lng_set()
        {
            lngset(out int bx, out byte al);
            return syousetu_lng_set_2();
        }

        private enmPass2JumpTable syousetu_lng_set_2()
        {
            work.dx = 0xdf00 + work.al;
            return enmPass2JumpTable.parset;
        }



        //4058-4234
        //;==============================================================================
        //;	"H" Command （ハードＬＦＯの設定）
        //;	 Hpms[, ams][, dly]
        //;==============================================================================
        private enmPass2JumpTable hardlfo_set()
        {
#if efc
            error('H', 11, work.si);
#else
            bool cy;
            int bx;
            byte al;

            cy = lngset(out bx, out al);
            if (work.al >= 8)
            {
                error('H', 2, work.si);
            }

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != ',') goto pmsonly;
            work.si++;
            int ax_p = work.ah * 0x100 + work.al;
            cy = lngset(out bx, out al);
            work.bx = ax_p;

            if (work.al >= 4)
            {
                error('H', 2, work.si);
            }

            goto bxset00;

        pmsonly:;
            work.bx = (work.bx & 0xff00) + work.al;
            work.al = 0;

        bxset00:;
            work.al <<= 4;
            work.al |= (byte)work.bx;
            work.al &= 0b0011_0111;
            work.ah = work.al;
            work.al = 0xe1;

            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)work.al));
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)work.ah));
            work.di += 2;

            work.dx = (byte)'H' * 0x100 + (byte)work.dx;
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

            if (work.al == (byte)',') return hdelay_set2();
            work.si--;

            return enmPass2JumpTable.olc0;
#endif
        }

        //;==============================================================================
        //;	Command "#" （ハードＬＦＯのスイッチ）
        //;	 #sw[,depth]
        //;	Command "#w/#p/#a/##" （OPM用）
        //;	 #w wf
        //;	 #p pmd
        //;	 #a amd
        //;	 ## wf,pmd,amd
        //;	Command "#D"	ハードＬＦＯディレイ
        //;==============================================================================
#if efc
        private enmPass2JumpTable hardlfo_onoff()
        {
            error('#', 11, work.si);
        }
#else
        private enmPass2JumpTable hardlfo_onoff()
        {
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al == (byte)'D') return hdelay_set();
            if (work.al == (byte)'f') return opm_hf_set();
            if (work.al == (byte)'w') return opm_wf_set();
            if (work.al == (byte)'p') return opm_pmd_set();
            if (work.al == (byte)'a') return opm_amd_set();
            if (work.al == (byte)'#') return opm_all_set();
            work.si--;
            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);
            if (work.al != 0) goto hlon;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') goto hloff_noparam;
            work.si++;

            cy = lngset(out bx, out al);// Dummy

        hloff_noparam:;
            work.dx = 0xe000;
            return enmPass2JumpTable.parset;

        hlon:;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',')
            {
                error('#', 6, work.si);
            }
            work.si++;

            cy = lngset(out bx, out al);

            if (work.al >= 8)
            {
                error('#', 2, work.si);
            }
            work.al |= 0b0000_1000;
            work.dx = 0xe000 + work.al;
            return enmPass2JumpTable.parset;
        }

        private enmPass2JumpTable hdelay_set()
        {
            work.dx = (byte)'#' * 0x100 + (byte)work.dx;
            return hdelay_set2();
        }

        private enmPass2JumpTable hdelay_set2()
        {
            get_clock();

            work.dx = 0xe400 + work.al;
            return enmPass2JumpTable.parset;
        }

        private enmPass2JumpTable opm_hf_set()
        {
            hf_set();
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable opm_wf_set()
        {
            wf_set();
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable opm_pmd_set()
        {
            pmd_set();
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable opm_amd_set()
        {
            amd_set();
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable opm_all_set()
        {
            hf_set();
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al != ',') error('#', 6, work.si);
            wf_set();
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al != ',') error('#', 6, work.si);
            pmd_set();
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al != ',') error('#', 6, work.si);
            amd_set();
            return enmPass2JumpTable.olc0;
        }

        private void hf_set()
        {
            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd7));
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
        }

        private void wf_set()
        {
            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);

            if (al >= 4)
            {
                error('#', 2, work.si);
            }
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd9));
            m_seg.m_buf.Set(work.di++, new MmlDatum(al));
        }

        private void pmd_set()
        {
            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);

            dl |= 0x80;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd8));
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
        }

        private void amd_set()
        {
            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);

            dl &= 0x7f;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd8));
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
        }

#endif



        //4235-4242
        //;==============================================================================
        //;	"<",">" の反転
        //;==============================================================================
        private enmPass2JumpTable octrev()
        {
            Tuple<string,Func<enmPass2JumpTable>> dmy = comtbl[ou00];
            comtbl[od00] = comtbl[od00];
            comtbl[od00] = dmy;

            return enmPass2JumpTable.olc03;
        }



        //4243-4298
        //;==============================================================================
        //;	"^" ... Length Multiple
        //;==============================================================================
        private enmPass2JumpTable lngmul()
        {
            bool cy;
            int bx;
            byte al;

            cy = lngset(out bx, out al);
            if (work.al == 0)
            {
                error('^', 2, work.si);
            }

            if (mml_seg.skip_flag != 0) return enmPass2JumpTable.olc03;

            if ((mml_seg.prsok & 1) == 0)//直前 = 音長?
            {
                error('^', 16, work.si);
            }

            if ((mml_seg.prsok & 2) != 0)//加工されているか?
            {
                error('^', 31, work.si);
            }

            work.al--;
            if (work.al == 0) return enmPass2JumpTable.olc03;

            int cx = work.al; //cx = 足す回数

            work.al = (byte)m_seg.m_buf.Get(work.di - 1).dat;// al = 足される数
            work.ah = work.al; // ah = 足す数

            //lnml00:;
            do
            {
                if (work.al + work.ah > 0xff)
                {
                    work.al += work.ah;
                    lm_over();
                }
                else
                {
                    work.al += work.ah;
                }
                //lnml01:;
                cx--;
            } while (cx > 0);

            m_seg.m_buf.Set(work.di - 1, new MmlDatum(work.al));

            return enmPass2JumpTable.olc03;
        }

        private void lm_over()
        {
            if ((mml_seg.prsok & 8) != 0)//ポルタ?
            {
                error('^', 8, work.si);
            }

            work.al++;

            m_seg.m_buf.Set(work.di - 1, new MmlDatum(0xff));

            if (mml_seg.part == mml_seg.rhythm)//R?
            {
                //lmo_r:;
                m_seg.m_buf.Set(work.di + 0, new MmlDatum(0x0f));//休符
                m_seg.m_buf.Set(work.di + 1, new MmlDatum(0x00));
                work.di += 2;
                mml_seg.prsok |= 2;//加工フラグ
                return;// goto lnml01;
            }

            m_seg.m_buf.Set(work.di, new MmlDatum(0xfb));
            byte bl = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            m_seg.m_buf.Set(work.di + 1, new MmlDatum(bl));
            m_seg.m_buf.Set(work.di + 2, new MmlDatum(0));
            work.di += 3;

            mml_seg.prsok |= 2;//加工フラグ
            return;// goto lnml01;
        }



        //4299-4337
        //;==============================================================================
        //;	"="  ... Length Rewrite
        //; 数値 ... Length Rewrite
        //;==============================================================================
        private enmPass2JumpTable lngrew()
        {
            bool cy;
            int bx;
            byte al;

            if (mml_seg.skip_flag != 0) return lng_skip_ret();

            if ((mml_seg.prsok & 1) == 0)//直前=音長?
            {
                error('=', 16, work.si);//= ?   
            }

            if ((mml_seg.prsok & 2) != 0)//直前=加工音長?
            {
                error('=', 31, work.si);//= ?   
            }

            work.di--;

            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch != '.') goto not_futen_rew;

            work.al = (byte)m_seg.m_buf.Get(work.di).dat;
            mml_seg.leng = work.al;
            goto futen_rew;

        not_futen_rew:;

            cy = lngset2(out bx, out al);

            if ((bx & 0xff00) != 0)
            {
                error('=', 8, work.si);
            }

            lngcal();

        futen_rew:;
            cy = futen();

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            if (!cy) return enmPass2JumpTable.olc03;

            if ((mml_seg.prsok & 8) == 0) return enmPass2JumpTable.olc03;//ポルタ?

            error('^', 8, work.si);

            return enmPass2JumpTable.exit;//dummy
        }


        private enmPass2JumpTable lngrew_2()
        {
            work.si--;
            return lngrew();
        }



        //4338-4371
        //;==============================================================================
        //;	"-"  ... Length 減算
        //;==============================================================================
        private enmPass2JumpTable lng_dec()
        {
            bool cy;
            int bx;
            byte al;

            if (mml_seg.skip_flag != 0) return lng_skip_ret();
            if ((mml_seg.prsok & 1) == 0)//直前=音長?
            {
                error('-', 16, work.si);
            }
            cy = lngset2(out bx, out al);
            if ((work.bx & 0xff00) != 0)
            {
                error('-', 8, work.si);
            }
            lngcal();
            cy = futen();
            if (cy)
            {
                error('-', 8, work.si);
            }
            byte d = (byte)m_seg.m_buf.Get(work.di - 1).dat;
            if (d <= work.al)
            {
                error('-', 8, work.si);
                // c or z=1
            }
            d -= work.al;
            m_seg.m_buf.Set(work.di - 1, new MmlDatum(d));
            return enmPass2JumpTable.olc03;
        }

        private enmPass2JumpTable lng_skip_ret()
        {
            bool cy;
            int bx;
            byte al;

            cy = lngset2(out bx, out al);
            if ((work.bx & 0xff00) != 0)
            {
                error('-', 8, work.si);
            }
            futen_skip();
            return enmPass2JumpTable.olc03;
        }



        //4372-4428
        //;==============================================================================
        //;	c ～ b の時
        //;==============================================================================
        private enmPass2JumpTable otoc()
        {
            work.al = 0;
            work.ah = mml_seg.def_c;

            return otoset();
        }

        private enmPass2JumpTable otod()
        {
            work.al = 2;
            work.ah = mml_seg.def_d;

            return otoset();
        }

        private enmPass2JumpTable otoe()
        {
            work.al = 4;
            work.ah = mml_seg.def_e;

            return otoset();
        }

        private enmPass2JumpTable otof()
        {
            work.al = 5;
            work.ah = mml_seg.def_f;

            return otoset();
        }

        private enmPass2JumpTable otog()
        {
            work.al = 7;
            work.ah = mml_seg.def_g;

            return otoset();
        }

        private enmPass2JumpTable otoa()
        {
            work.al = 9;
            work.ah = mml_seg.def_a;

            return otoset();
        }

        private enmPass2JumpTable otob()
        {
            work.al = 0xb;
            work.ah = mml_seg.def_b;

            return otoset();
        }

        private enmPass2JumpTable otox()
        {
            work.al = 0xc;

            return otoset();
        }

        private enmPass2JumpTable otor()
        {
            work.al = 0xf;

            return rest();
        }

        private enmPass2JumpTable otoset()
        {
#if !efc
            if (mml_seg.towns_flg == 1) goto otoset_towns_chk;// TOWNSならKパートcheckは要らない
            if (mml_seg.part == mml_seg.rhythm2)
            {
                error(work.dx >> 8, 17, work.si);// K part = error
            }
        otoset_towns_chk:;
            if (mml_seg.part != mml_seg.rhythm)
                return ots000();

            //;==============================================================================
            //;	リズム（Ｒ）パートで音程が指定された＝［＠ｎ ｃ］に変換
            //;==============================================================================
            int cx = mml_seg.lastprg;
            if (cx == 0)
            {
                error(work.dx >> 8, 30, work.si);
            }

            if (mml_seg.skip_flag != 0) return bp9();

            cx = (byte)cx * 0x100 + ((cx & 0xff00) >> 8);

            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)cx));
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)(cx >> 8)));
            work.di += 2;

            mml_seg.length_check1 = 1;//音長データがあったよ
            mml_seg.length_check2 = 1;
            mml_seg.prsok = 0;//prsok RESET
            return bp9();
#endif
        }



        //4429-4600
        //;==============================================================================
        //;	=,+,- 判定
        //;==============================================================================
        private enmPass2JumpTable ots000()
        {
            if (work.al == 0x0c)//x?
            {
                return otoset_x();//なら素直にそのまま設定
            }

            byte bh = (byte)mml_seg.octave;
            byte bl;
            bh &= 0xf;//bh<-オクターブ
            char ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            if (ch == '=')
            {
                //natural:;
                work.si++;
            }
            else if (work.ah != 0)
            {
                work.al += work.ah;
                //goto bp3;//範囲check //KUMA: doの中にjumpできない(ちょい無駄だけどそのまま突入する。。。)
            }

            //ots001:;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
                if (ch == '+')
                {
                    work.al++;
                    work.si++;
                }
                //bp2:;
                else if (ch == '-')//KUMA:ここもちょい無駄だけどループごとにひとつづつチェックさせる
                {
                    work.al--;
                    work.si++;
                }

                //;==============================================================================
                //;	c- は 1oct 下へ, b+ は 1oct 上へ
                //;==============================================================================

                //bp3:;
                work.al &= 0xf;
                bl = (byte)work.al;
                if (bl == 0xf)
                {
                    bh--;
                    if (bh == 0xff)
                    {
                        error(work.dx >> 8, 26, work.si);
                    }
                    bl = 0xb;
                }
                //bp4:;
                if (bl == 0xc)
                {
                    bh++;
                    if (bh == 0x8)
                    {
                        error(work.dx >> 8, 26, work.si);
                    }
                    bl = 0;
                }
                //bp5:;
                work.al = bl;
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            } while (ch == '+' || ch == '-');

            //;==============================================================================
            //;	音階データをblにセット
            //;==============================================================================
            bh = (byte)(bh << 4);
            bl |= bh;// bl=音階 DATA //KUMA: 上位4bit:オクターブ  下位4bit:音階
            work.bx = bh * 0x100 + bl;

            return enmPass2JumpTable.ots002;
        }

        private enmPass2JumpTable ots002()
        {
            if (mml_seg.bend == 0) return bp8();

            // PITCH/DETUNE SET
            int bx_p = work.bx;
            work.al = (byte)work.bx;
            work.bx = 0;
            if (mml_seg.pitch == 0) goto bp6;

#if !efc

            if (mml_seg.ongen < mml_seg.psg) goto fmpt;

            work.bx = mml_seg.pitch / 128;//PSG/PCMの時はPITCHを128で割る
            if (work.bx >= 0) goto bp6;
            work.bx++;
            goto bp6;

#endif

        fmpt:;
            work.al &= 0xf;
            int ax = work.al << 5;
            work.dx = ax; // DX = PITCHを掛けない状態のFnum値のある番地

            //int dx_p = work.dx; //KUMA:不要(x86ではidiv imulするとdxに影響がある)
            work.dx = 0;
            ax = 32;
            work.bx = (byte)mml_seg.bend;
            ax *= work.bx;
            work.bx = mml_seg.pitch;
            ax *= work.bx;
            work.bx = 8192;
            ax /= work.bx; // AX = PITCHでずらす番地 / 2
            //work.dx = dx_p; //KUMA:不要

            work.bx = work.dx;
            work.dx = fnumdat_seg.fnumTbl[work.bx];//DX = PITCHを掛けない状態の Fnum値
            ax *= 2;
            work.bx += ax;// BX = PITCHを掛けた後のFnum値のある番地

        bp50:;
            if (work.bx >= 0) goto bp51;// オクターブを下回ったか？
            work.bx += 32 * 12 * 2;
            work.dx += 0x26a;
            goto bp50;
        bp51:;
            if (work.bx < 32 * 12 * 2) goto bp52;// オクターブを上回ったか？
            work.bx -= 32 * 12 * 2;
            work.dx -= 0x26a;
            goto bp50;//KUMA:bp51のほうが無駄がないような気がする

        bp52:;
            work.dx -= fnumdat_seg.fnumTbl[work.bx];
            work.bx = work.dx;
            work.bx = -work.bx; // BX = PITCHをDETUNEに換算した値

        bp6:;
            work.bx += mml_seg.detune;
            work.bx += mml_seg.master_detune;
            if (work.bx == mml_seg.alldet) goto bp7;
            if (mml_seg.porta_flag == 1) goto porta_pitchset;
            work.al = 0xfa;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(work.bx >> 8)));

        bp6b:;
            mml_seg.alldet = work.bx;
        bp7:;
            work.bx = bx_p;
            return bp8();

        porta_pitchset:;
            if (m_seg.m_buf.Get(work.di - 1).dat != 0xda)
            {
                error(0, 14, work.si);
            }
            work.di--;
            work.al = 0xfa;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(work.bx >> 8)));
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xda));

            goto bp6b;
        }



        //4601-4616
        //;==============================================================================
        //;	REST 用 entry
        //;==============================================================================
        private enmPass2JumpTable rest()
        {
#if !efc

            if (mml_seg.towns_flg != 1)// TOWNSならKパートcheckは要らない
            {
                if (mml_seg.part == mml_seg.rhythm2)
                {
                    error('r', 17, work.si);// K part = error
                }
            }
            //rest_towns_chk:;
#endif
            return otoset_x();
        }

        private enmPass2JumpTable otoset_x()
        {
            work.bx = (work.bx & 0xff00) + (byte)work.al;
            return bp8();
        }



        //4617-4639
        //;==============================================================================
        //;	音階 DATA SET
        //;==============================================================================
        private enmPass2JumpTable bp8()
        {
            if (mml_seg.skip_flag == 0) goto bp8b;
            if (mml_seg.acc_adr == work.di)
            {
                //直前が(^ )^命令なら
                work.di -= 2;//それを削除
                mml_seg.acc_adr = 0;
            }
            //bp8a:;
            bool cy = lngset2(out int bx, out byte al);
            work.bx = bx;
            work.al = al;
            if ((bx & 0xff00) != 0)
            {
                error(0, 8, work.si);
            }

            futen_skip();

            return bp10();

        bp8b:;
            mml_seg.length_check1 = 1;//音長データがあった
            mml_seg.length_check2 = 1;

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));

            return bp9();
        }



        //4640-4686
        //;==============================================================================
        //;	音長計算
        //;==============================================================================

        private enmPass2JumpTable bp9()
        {
            bool cy = lngset2(out int bx, out byte al);
            work.bx = bx;
            work.al = al;

            if ((bx & 0xff00) != 0)
            {
                error(0, 8, work.si);
            }

            lngcal();

            mml_seg.prsok &= 0xfd; //加工音長フラグreset
            futen();
            press();

            //;==============================================================================
            //;	音長 DATA SET
            //;==============================================================================
            work.al = (byte)mml_seg.leng;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            mml_seg.prsok |= 1;// 音長flagをset
            mml_seg.prsok &= 0xf3;//音長+タイ,ポルタflagをreset

            work.al = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            work.al &= 0xf;
            if (work.al == 0xf) return bp10();//休符
            if (mml_seg.tie_flag != 0) return bp10();
            if (mml_seg.porta_flag != 0) return bp10();

            mml_seg.ge_flag1 = 0;
            mml_seg.ge_flag2 = 0;

            if (mml_seg.ge_delay != 0)
            {
                ge_set();// 擬似エコーセット＋装飾音符設定
                return bp10();
            }
            //bp9_2:;

            if (mml_seg.ss_length == 0) return bp10();

            ss_set();// 装飾音符設定
            return bp10();
        }

        private enmPass2JumpTable bp10()
        {
            mml_seg.tie_flag = 0;
            return enmPass2JumpTable.olc02;
        }



        //4687-4849
        //;==============================================================================
        //;	擬似エコーのセット
        //;==============================================================================
        private void ge_set()
        {
            mml_seg.ge_depth = mml_seg.ge_depth2;
            //ge_loop:;
            do
            {
                work.al = (byte)m_seg.m_buf.Get(work.di - 1).dat;
                if (work.al - (byte)mml_seg.ge_delay <= 0)
                {
                    work.al -= (byte)mml_seg.ge_delay;
                    break;//; 長さが足りない(cf or zf= 1)
                }
                work.al -= (byte)mml_seg.ge_delay;
                byte dh = work.al; // dh=length-delay
                byte dl = (byte)m_seg.m_buf.Get(work.di - 2).dat;//dl=onkai
                work.dx = dh * 0x100 + dl;

                work.al = (byte)mml_seg.ge_delay;
                m_seg.m_buf.Set(work.di - 1, new MmlDatum(work.al));

                if (mml_seg.ss_length == 0) goto ge_ss1;
                work.al = (byte)mml_seg.ss_length;// 長さが足りない
                byte d = (byte)m_seg.m_buf.Get(work.di - 1).dat;
                if (work.al - d >= 0)
                {
                    work.al -= d;
                    goto ge_ss1;
                }
                work.al -= d;
                work.al = (byte)mml_seg.ge_flag1;// (^の重複を避ける
                if (work.al == 0) goto no_dec_di;

                d = (byte)m_seg.m_buf.Get(work.di - 4).dat;
                if (work.al != d) goto no_dec_di;

                work.al = (byte)mml_seg.ge_flag2;
                d = (byte)m_seg.m_buf.Get(work.di - 3).dat;
                if (work.al != d) goto no_dec_di;

                int ax = (byte)m_seg.m_buf.Get(work.di - 2).dat;
                ax += (byte)m_seg.m_buf.Get(work.di - 1).dat * 0x100;
                m_seg.m_buf.Set(work.di - 4, new MmlDatum((byte)ax));
                m_seg.m_buf.Set(work.di - 3, new MmlDatum((byte)(ax >> 8)));
                work.di -= 2;

            no_dec_di:;
                int dx_p = work.dx;
                ss_set();
                work.dx = dx_p;
            ge_ss1:;
                if ((mml_seg.ge_tie & 1) == 0) goto ge_not_tie;
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)0xfb));//"&"

            ge_not_tie:;
                ge_set_vol();
                m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)work.dx));
                m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)(work.dx >> 8)));
                work.di += 2;
                mml_seg.prsok |= 2;//直前byte = 加工された音長
                if ((mml_seg.ge_tie & 2) != 0) break;
            } while (true);

            //ge_set_ret:;

            if (mml_seg.ss_length != 0)
            {
                ss_set();
            }
            //ge_ss2:;
            return;
        }

        private void ge_set_vol()
        {
            work.al = (byte)mml_seg.ge_depth;
            if ((work.al & 0x80) != 0) goto ge_minus;
            if (work.al == 0) goto gen_00;

            //;==============================================================================
            //;	音量が上がる
            //;==============================================================================
            if (mml_seg.ge_dep_flag == 1) goto gen_no_sel_vol;
            ongen_sel_vol();

        gen_no_sel_vol:;
            if (work.al == 0) goto gen_not_set;//0?

            m_seg.m_buf.Set(work.di, new MmlDatum((byte)0xde));
            work.di++;
            mml_seg.ge_flag1 = 0xde;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            mml_seg.ge_flag2 = work.al;

        gen_not_set:;
            work.al = (byte)mml_seg.ge_depth;
            work.al += (byte)mml_seg.ge_depth2;
            if (mml_seg.ge_dep_flag == 1) goto gen_01;
            if (work.al < 16) goto gen_00;
            work.al = 15;
        gen_00:;
            mml_seg.ge_depth = work.al;
            return;
        gen_01:;
            if ((work.al & 0x80) == 0) goto gen_00;
            mml_seg.ge_depth = 127; //    mov[ge_depth],+127
            return;

        //;==============================================================================
        //;	音量が下がる
        //;==============================================================================
        ge_minus:;
            work.al = (byte)-work.al;
            if (mml_seg.ge_dep_flag == 1) goto gem_no_sel_vol;
            ongen_sel_vol();

        gem_no_sel_vol:;
            if (work.al == 0) goto gem_not_set;//0?

            m_seg.m_buf.Set(work.di, new MmlDatum((byte)0xdd));
            work.di++;
            mml_seg.ge_flag1 = 0xdd;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            mml_seg.ge_flag2 = work.al;

        gem_not_set:;
            work.al = (byte)mml_seg.ge_depth;
            work.al += (byte)mml_seg.ge_depth2;
            if (mml_seg.ge_dep_flag == 1) goto gem_01;

            if ((sbyte)work.al >= -15) goto gem_00;
            work.al = 256 - 15;
        gem_00:;
            mml_seg.ge_depth = (sbyte)work.al;
            return;
        gem_01:;
            if ((work.al & 0x80) != 0) goto gem_00;
            mml_seg.ge_depth = -127; //    mov[ge_depth],-127
            return;
        }



        //4819-4849
        //;==============================================================================
        //;	各音源によって音量の増減を変える
        //;==============================================================================
        private void ongen_sel_vol()
        {
#if !efc
            if (mml_seg.part == mml_seg.pcmpart) goto sel_pcm;
            if (mml_seg.ongen == mml_seg.pcm_ex) goto sel_pcm;
            if (mml_seg.towns_flg != 1)
            {
                if (mml_seg.part == mml_seg.rhythm2) goto sel_pcm;
            }
            //osv_no_towns:;
            if (mml_seg.ongen != mml_seg.psg) goto sel_fm;
            return;
        sel_fm:;
#endif
            work.al *= 4;
            return;

#if !efc
        sel_pcm:;
            work.al *= 16;
            return;
#endif
        }



        //4850-4997
        //;==============================================================================
        //;	装飾音符のセット
        //;==============================================================================
        private void ss_set()
        {
            work.al = (byte)mml_seg.ss_length;
            byte d = (byte)m_seg.m_buf.Get(work.di - 1).dat;
            if (work.al - d >= 0)
            {
                work.al -= d;
                return;// goto ss_set_ret;// 長さが足りない
            }
            work.al -= d;

            d = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            if (d == 0x0c)//x?
            {
                return;// goto ss_set_ret;//なら装飾しない
            }

            work.di -= 2;

            work.dx = (byte)m_seg.m_buf.Get(work.di + 0).dat;
            work.dx += (byte)m_seg.m_buf.Get(work.di + 1).dat * 0x100;

            work.dx = ((byte)(work.dx >> 8) | (work.dx << 8)) & 0xffff; // Dh=Onkai/Dl=Length
            work.al = (byte)mml_seg.ss_depth;
            if ((work.al & 0x80) == 0) goto ss_plus;

            //;==============================================================================
            //;	下から上がる
            //;==============================================================================
            work.al = (byte)-work.al;
            int cx = (byte)work.al;            // cx = Depth
            work.bx = (work.dx & 0xff00) | (byte)work.bx;// bh = Onkai(for Move)
                                                         //ss_minus_loop:;
            do
            {
                one_down();
                cx--;
            } while (cx > 0);
        ss_minus_loop2:;
            if (mml_seg.ge_flag1 == 0) goto ssm_non_ge;
            work.al = (byte)mml_seg.ge_flag1;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.al = (byte)mml_seg.ge_flag2;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
        ssm_non_ge:;
            work.al = (byte)mml_seg.ss_speed;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.bx >> 8));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            if (mml_seg.ss_tie == 0) goto ssm_not_tie;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xfb));//"&"
        ssm_not_tie:;
            one_up();
            if ((work.bx & 0xff00) != (work.dx & 0xff00)) goto ss_minus_loop2;
            goto ss_fin;

        //;==============================================================================
        //;	上から下がる
        //;==============================================================================
        ss_plus:;
            cx = work.al;// cx = Depth
            work.bx = (work.dx & 0xff00) | (byte)work.bx;// bh = Onkai(for Move)
                                                         //ss_plus_loop:;
            do
            {
                one_up();
                cx--;
            } while (cx > 0);
        ss_plus_loop2:;
            if (mml_seg.ge_flag1 == 0) goto ssp_non_ge;
            work.al = (byte)mml_seg.ge_flag1;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.al = (byte)mml_seg.ge_flag2;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
        ssp_non_ge:;
            work.al = (byte)mml_seg.ss_speed;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.bx >> 8));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            if (mml_seg.ss_tie == 0) goto ssp_not_tie;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xfb));//"&"
        ssp_not_tie:;
            one_down();
            if ((work.bx & 0xff00) != (work.dx & 0xff00)) goto ss_plus_loop2;

            //;==============================================================================
            //;	最後の音符を書き込む
            //;==============================================================================
            ss_fin:;
            if (mml_seg.ge_flag1 == 0) goto ssf_non_ge;
            work.al = (byte)mml_seg.ge_flag1;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            work.al = (byte)mml_seg.ge_flag2;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
        ssf_non_ge:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.dx >> 8));
            byte dl = (byte)work.dx;
            dl -= (byte)mml_seg.ss_length;
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            work.dx = (work.dx & 0xff00) | dl;
            mml_seg.prsok |= 2;// 直前byte = 加工された音長
                               //ss_set_ret:;
            return;
        }

        //;==============================================================================
        //;	音階を一つ下げる
        //;		input/output bh to Onkai
        //;==============================================================================
        private void one_down()
        {
            byte bh = (byte)(work.bx >> 8);
            bh--;
            work.al = bh;
            work.al &= 0xf;
            if (work.al != 0xf)
            {
                work.bx = (bh << 8) | (byte)work.bx;
                return;
            }
            bh &= 0xf0;
            bh |= 0xb;
            if ((bh & 0x80) != 0)
            {
                error('S', 26, work.si);
            }
            work.bx = (bh << 8) | (byte)work.bx;
            //one_down_ret:;
        }

        //;==============================================================================
        //;	音階を一つ上げる
        //;		input/output bh to Onkai
        //;==============================================================================
        private void one_up()
        {
            byte bh = (byte)(work.bx >> 8);
            bh++;
            work.al = bh;
            work.al &= 0xf;
            if (work.al != 0xc)
            {
                work.bx = (bh << 8) | (byte)work.bx;
                return;
            }
            bh &= 0xf0;
            bh += 0x10;

            if ((bh & 0x80) != 0)
            {
                error('S', 26, work.si);
            }
            work.bx = (bh << 8) | (byte)work.bx;
            //one_up_ret:;
        }



        //4998-5057
        //;==============================================================================
        //;	前も同じ音符で、しかも"&"で繋がっていた場合は、圧縮する処理
        //;==============================================================================
        private void press()
        {
            byte d;
            if ((mml_seg.prsok & 0x80) != 0) return; //直前 = Rhythm？
            if ((mml_seg.prsok & 0x08) != 0) return; //直前 = ポルタ？
            if (mml_seg.skip_flag != 0) return;
            if ((mml_seg.prsok & 0x04) == 0) //直前 = +タイ？
            {
                d = (byte)m_seg.m_buf.Get(work.di - 1).dat;
                if (d != 0xf) return;
                if ((mml_seg.prsok & 0x01) != 0) goto restprs; //直前 = 音長？
                return;
            }

            //press_main:;
#if !efc
            if (mml_seg.part == mml_seg.rhythm) goto prs3;//リズムパートで圧縮可能＝無条件に圧縮
#endif

            //prs0:;
            byte ah = (byte)m_seg.m_buf.Get(work.di - 1).dat;
            d = (byte)m_seg.m_buf.Get(work.di - 4).dat;
            if (ah != d) return;
            //prs1:;
            work.di -= 3;
            mml_seg.prsok |= 2;//加工したflag

            d = (byte)m_seg.m_buf.Get(work.di).dat;
            if (work.al + d <= 255)
            {
                work.al += d;
                goto prs200;
            }
            work.al += d;

            m_seg.m_buf.Set(work.di, new MmlDatum(255));
            work.al++;//255 over
            work.di += 3;
            if (ah != 0xf) goto prs200;
            work.di--;
            m_seg.m_buf.Set(work.di - 1, new MmlDatum(ah));//r&r -> rr に変更
        prs200:;
            mml_seg.leng = work.al;
            return;

        restprs:;
#if !efc
            if (mml_seg.part == mml_seg.rhythm) goto prs3;
#endif

            d = (byte)m_seg.m_buf.Get(work.di - 3).dat;
            if (d != 0xf) return;

            prs3:;
            work.di -= 2;

            mml_seg.prsok |= 2;//加工したflag
            d = (byte)m_seg.m_buf.Get(work.di).dat;
            if (work.al + d <= 255)
            {
                work.al += d;
                goto prs200;
            }
            work.al += d;

            m_seg.m_buf.Set(work.di, new MmlDatum(255));
            work.al++;//255 over
            work.di += 2;
            goto prs200;
        }



        //5058-5067
        //;==============================================================================
        //;	数値の読み出し（書かれていない時は１）
        //;		output bx/al/[leng]
        //;==============================================================================
        private bool lngset(out int bx, out byte al)
        {
            if (!lngset2(out bx, out al)) return false;

            bx = 1;
            //lnexit相当
            al = (byte)1;
            work.bx = 1;
            work.al = (byte)1;
            mml_seg.leng = 1;
            return true;
        }



        //5068-5167
        //;==============================================================================
        //;	[si] から数値を読み出す
        //;	数字が書かれていない場合は[deflng] の値が返り、cy=1になる
        //;		output al/bx/[leng]
        //;==============================================================================
        private bool lngset2(out int bx, out byte al)
        {
            char ch;
            bx = 0;
            bool cy = false;

            do
            {
                ch = mml_seg.mml_buf[work.si++];
            } while (ch == ' ' || ch == '\t');

            mml_seg.calflg = 0;
            if (ch == '%')
            {
                mml_seg.calflg = 1;
                ch = mml_seg.mml_buf[work.si++];
            }

            //lgs00:;
            if (ch != '$')
            {
                //;	10進の場合
                work.si--;
                cy = numget(out al);
                //lgs02:;
                bx = al & 0xff;
                if (cy)
                {
                    //lgs03:;
                    bx = mml_seg.deflng & 0xff;
                    mml_seg.calflg = 1;
                    al = (byte)bx;
                    mml_seg.leng = al;
                    work.al = al;
                    work.bx = bx;
                    return true;
                }
                //lng1:;
                do
                {
                    cy = numget(out al);//; A=NUMBER
                    if (cy)
                    {
                        //lnexit:;
                        al = (byte)bx;
                        mml_seg.leng = al;
                        //lngset_ret:
                        work.al = al;
                        work.bx = bx;
                        return false;
                    }
                    //lng2:;
                    bx *= 10;
                    bx += al;
                } while (true);
            }

            //;	16進の場合
            //lgs01:;
            cy = hexget8(out al);
            bx = (bx & 0xff00) + (int)al;
            if (cy)
            {
                bx = mml_seg.deflng & 0xff;
                mml_seg.calflg = 1;
            }

            al = (byte)bx;
            mml_seg.leng = al;
            work.al = al;
            work.bx = bx;
            return cy;
        }

        private bool hexget8(out byte al_b)
        {
            al_b = (byte)mml_seg.mml_buf[work.si++];
            if (!hexcal8(ref al_b)) return true;//;ERROR RETURN

            byte bl = al_b;
            al_b = (byte)mml_seg.mml_buf[work.si++];

            if (hexcal8(ref al_b))
            {
                al_b += (byte)(bl * 16);
                return false;
            }

            work.si--;
            al_b = bl;
            return false;
        }

        private bool hexcal8(ref byte al_b)
        {
            if (byte.TryParse(((char)al_b).ToString(), System.Globalization.NumberStyles.HexNumber, null, out al_b))
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        //5168-5219
        //;==============================================================================
        //;	符点(.)があるかを見て、あれば[leng] を1.5倍する。
        //;	符点が２個以上あっても可
        //;		output al/bl/[leng]
        //;==============================================================================
        private bool futen()
        {
            char ch;

            work.al = (byte)mml_seg.leng;
            int ax = work.al;
            work.bx = ax;
            //ftloop:;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
                if (ch != '.') break;

                if (work.bx % 2 != 0)
                {
                    error('.', 21, work.si);
                }
                work.bx /= 2;
                ax += work.bx;
                work.si++;
            } while (true);
            //ft0:;
            if ((ax & 0xff00) != 0) goto ft1;// 音長 255 OVER
            work.bx = ax;
            work.al = (byte)ax;
            mml_seg.leng = (byte)ax;
            //    clc
            return false;

        ft1:;
            if (mml_seg.ge_delay != 0) error('.', 20, work.si);// Wコマンド使用中はError
            if (mml_seg.ss_length != 0) error('.', 20, work.si);// Sコマンド使用中もError

#if !efc
            if (mml_seg.part == mml_seg.rhythm) goto ft1_r;
#endif

            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)255));//音長255＋タイを設定
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)0xfb));
            work.di += 2;
            byte bl = (byte)m_seg.m_buf.Get(work.di - 3).dat;
            m_seg.m_buf.Set(work.di, new MmlDatum((byte)bl));//音符
            work.di++;

        ft2:;
            ax -= 255;
            if ((ax & 0xff00) != 0) goto ft1;// 音長 255 OVER

            work.bx = ax;
            mml_seg.leng = (byte)ax;
            work.al = (byte)ax;
            mml_seg.prsok |= 2;//直前＝加工された音長
            //  stc
            return true;

        ft1_r:;
            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)255));//音長255＋休符を設定
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)0x0f));
            work.di += 2;
            goto ft2;

        }



        //5220-5226
        private void futen_skip()
        {
            char ch;
            do
            {
                ch = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            } while (ch == '.');

            work.si--;
        }



        //5227-5243
        //;==============================================================================
        //;	0 ～ 9 の数値を得る
        //;		inputs	-- ds:si to mml pointer
        //; outputs	-- al
        //;			-- cy[1 = error]
        //;==============================================================================
        private bool numget(out byte al)
        {
            char ch = mml_seg.mml_buf[work.si++];
            al = (byte)(ch - '0');
            if (ch < '0')
            {
                work.si--;
                return true;
            }

            if (al >= 10)
            {
                work.si--;
                return true;
            }

            return false;
        }



        //5244-5269
        //;==============================================================================
        //;	COMMAND "o" オクターブの設定
        //;==============================================================================
        private enmPass2JumpTable octset()
        {
            char alc = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (alc == '+' || alc == '-')
            {
                octss_set((byte)alc);
                return enmPass2JumpTable.olc03;
            }

            if (lngset(out int bx, out byte al))
            {
                error(work.dx >> 8, 6, work.si);
            }

            al--;
            al += (byte)mml_seg.octss;
            octs0(al);

            return enmPass2JumpTable.olc03;
        }

        private void octs0(byte al)
        {
            if (al >= 8)
            {
                error(work.dx >> 8, 26, work.si);
            }
            mml_seg.octave = al;

            //   jmp olc03
        }

        private void octss_set(byte al)
        {
            getnum(out int bx, out byte dl);
            mml_seg.octss = dl;

            al = (byte)mml_seg.octave;
            al += dl;
            octs0(al);
        }



        //5270-5281
        //;==============================================================================
        //;	COMMAND ">","<" オクターブup/down
        //;==============================================================================
        private enmPass2JumpTable octup()
        {
            byte al = (byte)(mml_seg.octave + 1);
            octs0(al);

            return enmPass2JumpTable.olc03;
        }

        private enmPass2JumpTable octdown()
        {
            byte al = (byte)(mml_seg.octave - 1);
            octs0(al);
            return enmPass2JumpTable.olc03;
        }



        //5282-5311
        //;==============================================================================
        //;	COMMAND	"l"	デフォルト音長の設定
        //;	COMMAND	"l="	直前の音長の変更
        //;	COMMAND	"l-"	直前の音長の減算
        //;	COMMAND	"l^"	直前の音長の乗算
        //;==============================================================================
        private enmPass2JumpTable lengthset()
        {
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al == (byte)'=') return enmPass2JumpTable.lngrew;
            if (work.al == (byte)'-') return enmPass2JumpTable.lng_dec;
            if (work.al == (byte)'+') return enmPass2JumpTable.tieset_2;
            if (work.al == (byte)'^') return enmPass2JumpTable.lngmul;

            work.si--;

            bool cy = lngset2(out int bx, out byte al);
            work.bx = bx;
            work.al = al;
            if (cy)
            {
                error('l', 6, work.si);
            }

            if ((work.bx & 0xff00) != 0)
            {
                error(work.dx >> 8, 8, work.si);
            }

            lngcal();
            cy = futen();
            if (cy)
            {
                error('l', 8, work.si);
            }
            mml_seg.deflng = work.al;

            return enmPass2JumpTable.olc03;
        }



        //5312-5324
        //;==============================================================================
        //;	COMMAND "C" 全音符の長さを設定
        //;==============================================================================
        private enmPass2JumpTable zenlenset()
        {
            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);

            mml_seg.zenlen = work.al;
            mml_seg.deflng = (byte)((sbyte)work.al >> 2);
            return syousetu_lng_set_2();
        }



        //5325-5347
        //;==============================================================================
        //;	音長から具体的な長さを得る
        //;		INPUTS	-- [leng]
        //        to 音長
        //;			-- [zenlen]
        //        to 全音符の長さ
        //; OUTPUTS	-- al,[leng]
        //;==============================================================================
        private void lngcal()
        {
            work.al = (byte)mml_seg.leng;
            if (work.al == 0)
            {
                error(0, 21, work.si);// LENGTH=0 ... ERROR
            }

            if (mml_seg.calflg != 0) return;

            //lcl001:;
            work.al = (byte)mml_seg.zenlen;
            int ax = work.al;

            int d = ax / mml_seg.leng;
            if (ax % mml_seg.leng != 0)
            {
                error(0, 21, work.si);// 音長が全音符の公約数でない
            }
            mml_seg.leng = (byte)d;
        }



        //5348-5356
        //;==============================================================================
        //;	2byte[dh / dl] の dataをセットして戻る
        //;==============================================================================
        private enmPass2JumpTable parset()
        {
            byte b = (byte)work.dx;
            work.dx = (work.dx >> 8) + b * 0x100;

            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)work.dx));
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)(work.dx >> 8)));
            work.di += 2;

            return enmPass2JumpTable.olc0;
        }



        //5357-5388
        //;==============================================================================
        //;	COMMAND "t" / "T" テンポ／TimerBセット
        //;==============================================================================
        private enmPass2JumpTable tempoa()
        {
#if efc
            error('t', 12, work.si);//効果音emlにはテンポは指定出来ない
#else
            char alc = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            byte ah = 0xfd;//t±

            if (alc == '+') return tempo_ss(ah);
            if (alc == '-') return tempo_ss(ah);
            bool cy = lngset(out int bx, out byte al);
            if (cy || al < 18)
            {
                error('t', 2, work.si);
            }

#if !tempo_old_flag

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xfc));//t
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xff));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));
            return enmPass2JumpTable.olc0;
#else
            //	call timerb_get
            //    mov al, dl
            //    jmp tset
#endif
#endif
        }



        //5389-5428
        //;==============================================================================
        //;	"T" Command Entry
        //;==============================================================================

        private enmPass2JumpTable tempob()
        {
#if efc
            error('T', 12, work.si);//効果音emlにはテンポは指定出来ない
#else
            char alc = (work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a);
            byte ah = 0xfe;//T±
            if (alc == '+') return tempo_ss(ah);
            if (alc == '-') return tempo_ss(ah);
            bool cy = lngset(out int bx, out byte al);
            //tset:;
            if (cy || al >= 251)//251～255はエラー
            {
                error('T', 2, work.si);//KUMA: t -> T
            }

            work.dx = 0xfc00 + al;
            return enmPass2JumpTable.parset;
#endif
        }

        private enmPass2JumpTable tempo_ss(byte ah)
        {
#if !efc
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)0xfc));
            m_seg.m_buf.Set(work.di++, new MmlDatum(ah));
            bool cy = getnum(out int bx, out byte dl);
            work.al = dl;
            if (work.al != 0)
            {
                m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
                return enmPass2JumpTable.olc0;
            }
            //tss_non:;		; t±0 T±0の時は無視
            work.di -= 2;
            return enmPass2JumpTable.olc02;
#endif
        }


        //5429-5449
        //;==============================================================================
        //;	タイマＢの数値 を 計算
        //;		INPUTS --  AL = TEMPO
        //;		OUTPUTS -- DL = タイマＢの数値
        //;
        //;	DL = 256 - [ 112CH / TEMPO]
        //;==============================================================================
        private void timerb_get(byte al, out byte dl)
        {
            dl = 0;
            if (tempo_old_flag != 0)
            {
                //timerb_get:
                byte bl = al;

                int ax = 0x112c;
                al = (byte)(ax / bl);
                byte ah = (byte)(ax % bl);
                dl = (byte)(0x100 - al);

                if (ah > 127)
                {
                    dl--;//四捨五入
                }
            }
        }



        //5450-5473
        //;==============================================================================
        //;	clock値またはlength値を読み取る
        //;		input dh  command name
        //; output al  clock
        //;==============================================================================
        private void get_clock()
        {
            bool cy;
            int bx;
            byte al;

            char alc = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (alc != 'l') goto gcl_no_length;
            work.si++;

            int dx_p = work.dx;

            cy = lngset2(out bx, out al);
            if ((work.bx & 0xff00) != 0)
            {
                error((char)(byte)(work.dx >> 8), 8, work.si);
            }

            lngcal();
            cy = futen();
            if (cy)
            {
                work.dx = dx_p;
                error((char)(byte)(work.dx >> 8), 8, work.si);
            }

            return;
        gcl_no_length:;
            cy = lngset(out bx, out al);
        }



        //5474-5515
        //;==============================================================================
        //;	COMMAND	"q" step-gate time change
        //;==============================================================================
        private enmPass2JumpTable qset()
        {
            char al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (al == ',') goto qsetb;

            work.dx = ((byte)'q') * 0x100 + (byte)work.dx;
            get_clock();

            work.dx = work.al * 0x100 + (byte)0xfe;

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.dx));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(work.dx >> 8)));

            al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (al != '-') goto qseta;

            work.si++;

            int dx_p = work.dx;

            work.dx = ((byte)'q') * 0x100 + (byte)work.dx;
            get_clock();
            work.dx = work.al * 0x100 + (byte)0xb1;

            int ax = dx_p;

            byte dh = (byte)(work.dx >> 8);
            byte ah = (byte)(ax >> 8);

            work.dx = ((byte)(dh - ah)) * 0x100 + (byte)work.dx;
            if (dh - ah >= 0) goto qrnd_set;
            dh = (byte)-(work.dx >> 8);
            dh |= 0x80;
            work.dx = dh * 0x100 + (byte)work.dx;

        qrnd_set:;
            if ((work.dx & 0xff00) == 0) goto qseta;

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.dx));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(work.dx >> 8)));

        qseta:;
            al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (al != ',') return enmPass2JumpTable.olc0;

            qsetb:;
            work.si++;
            work.dx = ((byte)'q') * 0x100 + (byte)work.dx;
            get_clock();
            work.dx = 0xb300 + work.al;
            return enmPass2JumpTable.parset;
        }



        //5516-5548
        //;==============================================================================
        //;	COMMAND	"Q" step-gate time change 2
        //;==============================================================================
        private enmPass2JumpTable qset2()
        {
            bool cy;
            int bx;
            byte al;
            char alc = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (alc == '%') return qset3();

            cy = lngset(out bx, out al);
            if (work.al >= 9)
            {
                error('Q', 2, work.si);
            }

            work.al *= 2;
            if (work.al == 0) goto q2_not_inc;

            work.al *= 16;
            work.al--;

        q2_not_inc:;
            work.al = (byte)~work.al;
            work.dx = 0xc400 + work.al;
            return enmPass2JumpTable.parset;
        }

        //;==============================================================================
        //;	COMMAND	"Q%" step-gate time change 2
        //;==============================================================================
        private enmPass2JumpTable qset3()
        {
            work.si++;

            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);
            work.al = (byte)~work.al;
            work.dx = 0xc400 + work.al;
            return enmPass2JumpTable.parset;
        }



        //5549-5605
        //;==============================================================================
        //;	COMMAND	"v"/"V" volume_set
        //;==============================================================================
        private enmPass2JumpTable vseta()
        {
            char al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (al == '+') return enmPass2JumpTable.vss;
            if (al == '-') return enmPass2JumpTable.vss;
            if (al == ')') return enmPass2JumpTable.vss2;
            if (al == '(') return enmPass2JumpTable.vss2m;

            bool cy = lngset(out int bx, out byte alb);
            work.bx = bx;
            work.bx += mml_seg.volss2;
            if ((byte)work.bx >= 17)
            {
                error('v', 2, work.si);
            }
#if !efc
            if (mml_seg.part == mml_seg.pcmpart) return enmPass2JumpTable.vsetm;
            if (mml_seg.ongen == mml_seg.pcm_ex) return enmPass2JumpTable.vsetm;
            if (mml_seg.towns_flg != 1) goto vsa_no_towns;
            if (mml_seg.part == mml_seg.rhythm2) return enmPass2JumpTable.vsetm;//TownsのKパートはPCM相当
            vsa_no_towns:;
            if (mml_seg.ongen >= mml_seg.psg) return enmPass2JumpTable.vset;
#endif
            work.bx = (byte)work.bx;
            work.bx += 0;//offset fmvol
            work.bx = mml_seg.fmvol[work.bx];

            return enmPass2JumpTable.vset;
        }

        private enmPass2JumpTable vset()
        {
            work.dx = (work.dx & 0xff00) + (byte)work.bx;
            return vset2();
        }

        private enmPass2JumpTable vset2()
        {
            work.dx = 0xfd00 + (byte)work.dx;
            work.al = (byte)mml_seg.volss;
            work.al += (byte)work.dx;
            if (work.al < 0x80) goto vset4;
            if ((mml_seg.volss & 0x80) == 0) goto vset3;
            work.al = 0;
            goto vset4;
        vset3:;
#if !efc
            if (mml_seg.ongen < mml_seg.psg) goto vset3f;
            work.al = 15;
            goto vset4;
#endif
        vset3f:;
            work.al = 0x7f;
        vset4:;
            work.dx = (work.dx & 0xff00) + (byte)work.al;
            mml_seg.nowvol = work.al;
            return enmPass2JumpTable.parset;
        }



        //5606-5621
        //;==============================================================================
        //;	command "V" entry
        //;==============================================================================
        private enmPass2JumpTable vsetb()
        {
            bool cy = lngset(out int bx, out byte al);
#if !efc
            work.bx = bx;
            work.al = al;
            if (mml_seg.part == mml_seg.pcmpart) return enmPass2JumpTable.vsetm1;
            if (mml_seg.ongen == mml_seg.pcm_ex) return enmPass2JumpTable.vsetm1;
            if (mml_seg.towns_flg != 1) return enmPass2JumpTable.vset;
            if (mml_seg.part == mml_seg.rhythm2) return enmPass2JumpTable.vsetm1;//TownsのKパートはPCM相当
#endif

            return enmPass2JumpTable.vset;
        }



#if !efc
        //5622-5656
        //;==============================================================================
        //;	PCM volset patch
        //;==============================================================================
        private enmPass2JumpTable vsetm()
        {
            if (mml_seg.pcm_vol_ext == 1) goto vsetma;
            if ((byte)work.bx * 16 < 256) work.bx = (work.bx & 0xff00) + (byte)work.bx * 16;
            else work.bx = (work.bx & 0xff00) + 255;
            return enmPass2JumpTable.vsetm1;
        vsetma:;
            work.al = (byte)work.bx;
            int ax = work.al * (byte)work.bx;//    mul bl
            work.al = (byte)ax;
            if (ax < 256) goto vsetmb;
            work.al = 255;
        vsetmb:;
            work.bx = (work.bx & 0xff00) + work.al;
            return enmPass2JumpTable.vsetm1;
        }

        private enmPass2JumpTable vsetm1()
        {
            work.dx = 0xfd00 + (byte)work.bx;
            work.al = (byte)mml_seg.volss;
            if ((work.al & 0x80) == 0) goto vsetm0;
            bool cy = (byte)work.dx + work.al > 255;
            work.dx = (work.dx & 0xff00) + (byte)((byte)work.dx + work.al);
            if (cy) goto vset4m;
            work.dx = (work.dx & 0xff00);
            goto vset4m;
        vsetm0:;
            cy = (byte)work.dx + work.al > 255;
            work.dx = (work.dx & 0xff00) + (byte)((byte)work.dx + work.al);
            if (!cy) goto vset4m;
            work.dx = (work.dx & 0xff00) + 255;
        vset4m:;
            work.al = (byte)work.dx;

            //vset4相当
            work.dx = (work.dx & 0xff00) + (byte)work.al;
            mml_seg.nowvol = work.al;
            return enmPass2JumpTable.parset;
        }
#endif



        //5657-5674
        //;==============================================================================
        //;	command "v+"/"v-" entry
        //;==============================================================================
        private enmPass2JumpTable vss()
        {
            bool cy;
            int bx;
            byte dl;

            cy = getnum(out bx, out dl);

            mml_seg.volss = (sbyte)dl;
            work.dx = (work.dx & 0xff00) | (byte)mml_seg.nowvol;
#if !efc
            if (mml_seg.part == mml_seg.pcmpart) return enmPass2JumpTable.vsetm1;
            if (mml_seg.ongen == mml_seg.pcm_ex) return enmPass2JumpTable.vsetm1;
            if (mml_seg.towns_flg != 1) return vset2();
            if (mml_seg.part == mml_seg.rhythm2) return enmPass2JumpTable.vsetm1;//TownsのKパートはPCM相当
#endif
            return vset2();
        }



        //5675-5688
        //;==============================================================================
        //;	command "v)"/"v(" entry
        //;==============================================================================
        private enmPass2JumpTable vss2()
        {
            work.si++;

            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);

            mml_seg.volss2 = dl;
            return enmPass2JumpTable.olc03;
        }

        private enmPass2JumpTable vss2m()
        {
            work.si++;

            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);
            dl = (byte)-dl;

            mml_seg.volss2 = dl;
            return enmPass2JumpTable.olc03;
        }



        //5689-5798
        //;==============================================================================
        //;	command	"@"	音色の変更
        //;==============================================================================
        private enmPass2JumpTable neirochg()
        {
            int bx;
            byte al;
            bool cy;
            int cx = 0;
            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '@')
            {
                work.si++;
                cx = 128;
            }
            //nc01:;
            cy = lngset(out bx, out al);
            work.bx += cx;
            if (mml_seg.prg_flg != 0) set_prg();

            //not_sp:;
#if !efc
            if (mml_seg.part == mml_seg.rhythm) goto rhyprg;
            if (mml_seg.ongen == mml_seg.psg) goto psgprg;
#endif
            work.dx = 0xff00 + (byte)work.bx;
            work.bx = (work.bx & 0xff00) | (byte)(work.bx + 1);
            if (mml_seg.maxprg >= (byte)work.bx) goto nc00;
            mml_seg.maxprg = (byte)work.bx;
#if efc
        nc00:;
            return enmPass2JumpTable.parset;
#else
        nc00:;
            if (mml_seg.part == mml_seg.pcmpart) goto repeat_check;
            if (mml_seg.ongen == mml_seg.pcm_ex) goto repeat_check;
            if (mml_seg.part != mml_seg.rhythm2) return enmPass2JumpTable.parset;
            if (mml_seg.towns_flg == 1) goto repeat_check;// townsの K = PCM part
            if (mml_seg.skip_flag != 0) return enmPass2JumpTable.olc0;

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.dx));

            mml_seg.length_check1 = 1;// 音長データがあったよ
            mml_seg.length_check2 = 1;
            return enmPass2JumpTable.olc0;

        rhyprg:;
            if (work.bx >= 0x4000)
            {
                error('@', 2, work.si);
            }

            work.bx |= (0b1000_0000) * 0x100;
            mml_seg.lastprg = work.bx;
            return enmPass2JumpTable.olc03;

        psgprg:;
            //work.bx *= 4;
            //work.bx = (byte)work.bx;
            work.bx += 0;//offset psgenvdat
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf0));
            cx = 4;
            if (work.bx > 9)
            {
                Log.WriteLine(LogLevel.WARNING,
                    string.Format(msg.get("W0100"), work.bx)
                    );
                work.bx = 0;
            }
            //pplop0:;
            for (int i = 0; i < 4; i++)
            {
                work.al = mml_seg.psgenvdat[work.bx][i];
                m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.al));
            }
            //loop    pplop0
            return enmPass2JumpTable.olc0;

        repeat_check:;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') return enmPass2JumpTable.parset;

            int ax = work.dx;
            ax = (byte)(ax >> 8) | (((byte)ax) * 0x100);

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

            work.si++;

            cy = getnum(out bx, out byte dl);
            work.al = 0xce;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            ax = work.bx;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') goto noset_stop;

            work.si++;
            cy = getnum(out bx, out dl);
            ax = work.bx;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') goto noset_release;

            work.si++;
            cy = getnum(out bx, out dl);
            ax = work.bx;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

            return enmPass2JumpTable.olc0;

        noset_stop:;
            ax = 0;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

        noset_release:;
            ax = 0x8000;
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)ax));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(ax >> 8)));

            return enmPass2JumpTable.olc0;
#endif
        }



        //5799-5814
        //;==============================================================================
        //;	Ｖ２．６用 / ＦＭ音源の音色使用フラグセット
        //;==============================================================================
        private void set_prg()
        {
#if !efc
            if (mml_seg.ongen >= mml_seg.psg) return;
#endif
            int bx_p = work.bx;
            work.bx = (byte)work.bx;
            work.bx += 0;//offset prg_num
            mml_seg.prg_num[work.bx] = 1;
            work.bx = bx_p;
            //set_prg_ret:;
            return;
        }



        //5815-5924
        //;==============================================================================
        //;	COMMAND "&"	タイ
        //;	COMMAND "&&"	スラー
        //;	COMMAND "+"     直前の音長の加算
        //;==============================================================================
        private enmPass2JumpTable tieset()
        {
            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '&') return sular();// スラー?

            return tieset_2();
        }

        private enmPass2JumpTable tieset_2()
        {
            if (mml_seg.skip_flag != 0) return tie_skip();

            bool cy = lngset2(out int bx, out byte al);
            if (cy) goto tie_norm;

            if ((work.bx & 0xff00) != 0)
            {
                error('&', 8, work.si);
            }

            if ((mml_seg.prsok & 1) == 0)//直前byte = 音長?
            {
                error('&', 22, work.si);
            }

            if ((work.bx & 0xff00) != 0)//KUMA:２かい調べる?
            {
                error('&', 8, work.si);
            }

            work.di--;
            lngcal();
            cy = futen();
            if (cy)
            {
                error('&', 8, work.si);
            }

            byte ah = (byte)m_seg.m_buf.Get(work.di).dat;
            if (ah + work.al > 0xff)
            {
                ah += work.al;
                goto tie_lng_over;
            }
            ah += work.al;
            work.al = ah;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            mml_seg.prsok |= 2;// 直前 = 加工音長
            return enmPass2JumpTable.olc02;

        tie_lng_over:;
            work.di++;

            if ((mml_seg.prsok & 8) != 0)//ポルタ?
            {
                error('^', 8, work.si);
            }

#if !efc
            if (mml_seg.part == mml_seg.rhythm) goto tlo_r;//R
#endif

            m_seg.m_buf.Set(work.di, new MmlDatum(0xfb));
            ah = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            work.di++;

            m_seg.m_buf.Set(work.di, new MmlDatum(ah));
            work.di++;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            mml_seg.prsok = 1;//直前 = 音長
            return enmPass2JumpTable.olc02;

        tlo_r:;
            m_seg.m_buf.Set(work.di, new MmlDatum(0x0f)); //休符
            work.di++;

            m_seg.m_buf.Set(work.di, new MmlDatum(work.al));
            work.di++;
            mml_seg.prsok = 3;//直前 = 加工音長
            return enmPass2JumpTable.olc02;

        tie_norm:;
            mml_seg.tie_flag = 1;
#if !efc
            if (mml_seg.part == mml_seg.rhythm)//R
            {
                error('&', 32, work.si);
            }
#endif
            m_seg.m_buf.Set(work.di, new MmlDatum(0xfb));
            work.di++;
            if ((mml_seg.prsok & 1) == 0)//; 直前byte = 音長?
                return enmPass2JumpTable.olc0;//でなければ prsok = 0クリア
            mml_seg.prsok |= 4;//音長+タイのフラグをセット
            mml_seg.prsok &= 0xfe;//音長のフラグをリセット
            return enmPass2JumpTable.olc02;
        }

        private enmPass2JumpTable tie_skip()
        {
            bool cy = lngset2(out int bx, out byte al);
            if ((work.bx & 0xff00) != 0)
            {
                error('&', 8, work.si);
            }
            futen_skip();
            return enmPass2JumpTable.olc03;
        }

        private enmPass2JumpTable sular()
        {
#if !efc
            if (mml_seg.part == mml_seg.rhythm)//R
            {
                error('&', 32, work.si);
            }
#endif
            work.si++;
            if (mml_seg.skip_flag != 0) return tie_skip();
            work.al = 0xc1;
            m_seg.m_buf.Set(work.di, new MmlDatum(work.al));
            work.di++;

            int si_p = work.si;
            bool cy = lngset2(out int bx, out byte al);

            //    pushf
            if ((work.bx & 0xff00) != 0)
            {
                error('&', 8, work.si);
            }
            //    popf
            work.si = si_p;
            if (cy) return enmPass2JumpTable.olc0;

            byte bl = (byte)m_seg.m_buf.Get(work.di - 3).dat;//bl=前の音階
            return enmPass2JumpTable.ots002;//通常音程コマンド発行
        }



        //5925-6024
        //;==============================================================================
        //;	COMMAND "D"	デチューンの設定
        //;==============================================================================
        private enmPass2JumpTable detset()
        {
            bool cy;
            int bx;
            byte dl;

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al == (byte)'D') goto detset_2;
            if (work.al == (byte)'X') goto extdet_set;
            if (work.al == (byte)'M') goto mstdet_set;
            if (work.al == (byte)'F') goto vd_fm;
            if (work.al == (byte)'S') goto vd_ssg;
            if (work.al == (byte)'P') goto vd_pcm;
            if (work.al == (byte)'R') goto vd_rhythm;
            if (work.al == (byte)'Z') goto vd_ppz;
            work.si--;
            cy = getnum(out bx, out dl);
            mml_seg.detune = bx;
            bx += mml_seg.master_detune;
            work.bx = bx;

        detset_exit:;
            if (mml_seg.bend != 0) return enmPass2JumpTable.olc03;
            work.al = 0xfa;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(work.bx >> 8)));
            return enmPass2JumpTable.olc0;

        //;==============================================================================
        //;	COMMAND "DD"	相対デチューンの設定
        //;==============================================================================
        detset_2:;
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd5));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)(work.bx >> 8)));
            return enmPass2JumpTable.olc0;

        //;==============================================================================
        //;	COMMAND "DM"	マスターデチューン指定
        //;==============================================================================
        mstdet_set:;
            cy = getnum(out bx, out dl);
            mml_seg.master_detune = bx;
            work.bx += mml_seg.detune;
            goto detset_exit;

        //;==============================================================================
        //;	COMMAND "DX"	拡張デチューン指定
        //;==============================================================================
        extdet_set:;
            work.al = 0xcc;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            cy = getnum(out bx, out dl);
            work.al = dl;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        //;==============================================================================
        //;	COMMAND "DF"/"DS"/"DP"/"DR" 音量ダウン設定
        //;==============================================================================
        vd_fm:;
            work.al = 0xfe;
            goto vd_main;
        vd_ssg:;
            work.al = 0xfc;
            goto vd_main;
        vd_pcm:;
            work.al = 0xfa;
            goto vd_main;
        vd_rhythm:;
            work.al = 0xf8;
            goto vd_main;
        vd_ppz:;
            work.al = 0xf5;
        vd_main:;
            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
            if (ch == '+') goto vd_main2;
            if (ch == '-') goto vd_main2;
            work.al++;
        vd_main2:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xc0));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            cy = getnum(out bx, out dl);
            work.al = dl;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;
        }



        //6025-6059
        //;==============================================================================
        //;	符号付き数値を読む
        //;		OUTPUTS -- bx[word],dl[byte]
        //;==============================================================================
        private bool getnum(out int bx, out byte dl)
        {
            char al;
            int dh = 0;
            do
            {
                al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;

            } while (al == ' ' || al == 9);

            if (al == '+')
            {
                dh = 0;
            }
            else if (al == '-')
            {
                dh = 1;
            }
            else
            {
                work.si--;
            }

            bool cy = lngset(out bx, out byte al_b);

            dl = (byte)bx;
            if (dh == 0)
            {
                work.dx = dl;
                work.bx = bx;
                return cy;
            }

            //; dlとbxの符号を反転
            bx = -bx;
            dl = (byte)(-(int)dl);

            work.dx = (dh << 8) | dl;
            work.bx = bx;
            return cy;
        }



        //6060-6092
        //;==============================================================================
        //;	COMMAND "[" [LOOP START]
        //;==============================================================================
        private enmPass2JumpTable stloop()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf9));

            work.al = (byte)mml_seg.lopcnt;
            mml_seg.lopcnt++;

            int ax = work.al * 2;
            work.bx = 0;//offset loptbl
            work.bx += ax;// bxにloptblをセット

            // dxに、di - mbufを入れる
            work.dx = work.di;
            work.dx -= 0;//offset m_buf

            // 現在のdi - mbufをloptblに書く
            mml_seg.loptbl[work.bx + 0] = (byte)work.dx;
            mml_seg.loptbl[work.bx + 1] = (byte)(work.dx >> 8);

            // lextblに 0を書いておく
            //work.bx += mml_seg.loopnest * 2;
            //mml_seg.loptbl[work.bx + 0] = (byte)0;
            //mml_seg.loptbl[work.bx + 1] = (byte)0;
            mml_seg.lextbl[work.bx + 0] = (byte)0;
            mml_seg.lextbl[work.bx + 1] = (byte)0;

            //2byte 開けておく
            //work.di += 2;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0x00));//KUMA: オリジナルではメモリの内容が不定のまま？
            m_seg.m_buf.Set(work.di++, new MmlDatum(0x00));

            mml_seg.length_check2 = 0;//[]0発見対策

            return enmPass2JumpTable.olc0;
        }



        //6093-6203
        //;==============================================================================
        //;	COMMAND "]" [LOOP END]
        //;==============================================================================
        private enmPass2JumpTable edloop()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf8));

            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);

            if (cy)
            {
                work.bx = mml_seg.loop_def;//bl
                edl00b();
            }
            else
                edl00();

            return enmPass2JumpTable.olc0;
        }

        private void edl00()
        {
            byte bh = (byte)(work.bx >> 8);
            if (bh != 0)
            {
                error(']', 2, work.si);
            }
            edl00b();
        }

        // 繰り返し回数セット
        private void edl00b()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));

            if ((byte)work.bx != 0) goto edl_nonmuloop;

            if (mml_seg.length_check2 == 0)
            {
                error('[', 24, work.si);
            }

        edl_nonmuloop:;
            //	ひとつ開ける（ドライバーで使用する）
            m_seg.m_buf.Set(work.di++, new MmlDatum(0));
            mml_seg.lopcnt--;
            work.al = (byte)mml_seg.lopcnt;
            if (work.al == 0xff)
            {
                error(']', 23, work.si);
            }
            work.bx = work.al * 2;//offset loptbl
            //loptblに書いておいた値をセット
            work.dx = mml_seg.loptbl[work.bx + 0] + mml_seg.loptbl[work.bx + 1] * 0x100;
            m_seg.m_buf.Set(work.di + 0, new MmlDatum((byte)work.dx));
            m_seg.m_buf.Set(work.di + 1, new MmlDatum((byte)(work.dx >> 8)));

            //;==============================================================================
            //;	"[" のあった所に今のアドレスを書く
            //;==============================================================================
            int bx_p = work.bx;
            work.dx += 0;//offset m_buf	;dx=[commandで２つ開けておいたアドレス
            work.bx = work.di;
            work.bx -= 2;//bx=繰り返し回数がセットされているアドレス
            work.bx -= 0;//offset m_buf
            int a = work.bx;
            work.bx = work.dx;
            work.dx = a;

            m_seg.m_buf.Set(work.bx + 0, new MmlDatum((byte)work.dx));//;そこにもdxを書く
            m_seg.m_buf.Set(work.bx + 1, new MmlDatum((byte)(work.dx >> 8)));
            work.bx = bx_p;

            //;==============================================================================
            //;	":" があった時にはそこにも書く
            //;==============================================================================
            //work.bx += mml_seg.loopnest * 2;// bx＝lextblの位置
            //work.bx = m_seg.m_buf.Get(work.bx).dat + m_seg.m_buf.Get(work.bx).dat * 0x100;//bx＝lextblの値
            work.bx = mml_seg.lextbl[work.bx] + mml_seg.lextbl[work.bx + 1] * 0x100;//bx＝lextblの値
            if (work.bx == 0) goto nonexit;//":"はない

            m_seg.m_buf.Set(work.bx + 0, new MmlDatum((byte)work.dx));//;そこにもdxを書く
            m_seg.m_buf.Set(work.bx + 1, new MmlDatum((byte)(work.dx >> 8)));
            // DETUNE CANCEL(Bend On / ":"のあった時のみ)
            if (mml_seg.bend == 0) goto nonexit;
            mml_seg.alldet = 0x8000;
        nonexit:;
            work.di += 2;
            //if (work.si != 0) return enmPass2JumpTable.olc0;//pass2最後のcheck_loopか?

            return;
        }



        //;==============================================================================
        //;	COMMAND ":"	ループから脱出
        //;==============================================================================
        private enmPass2JumpTable extloop()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf7));

            work.al = (byte)mml_seg.lopcnt;
            work.al--;

            if (work.al == 0xff)
            {
                error(':', 23, work.si);
            }

            work.bx = 0;//offset lextbl
            work.bx += work.al * 2;
            work.dx = mml_seg.lextbl[work.bx + 0] + mml_seg.lextbl[work.bx + 1] * 0x100;

            if (work.dx != 0)
            {
                error(':', 25, work.si);//":"が２つ以上あった
            }

            mml_seg.lextbl[work.bx + 0] = (byte)work.di;// lextblに開けておくアドレスをセット
            mml_seg.lextbl[work.bx + 1] = (byte)(work.di >> 8);

            //work.di += 2;//２つ、開けておく
            m_seg.m_buf.Set(work.di++, new MmlDatum(0x00));//KUMA: オリジナルではメモリの内容が不定のまま？
            m_seg.m_buf.Set(work.di++, new MmlDatum(0x00));

            return enmPass2JumpTable.olc0;
        }



        //6204-6220
        //;==============================================================================
        //;	COMMAND "L" [LOOP SET]
        //;==============================================================================
        private enmPass2JumpTable lopset()
        {
#if efc
            error('L', 17, work.si);
#else
            if (mml_seg.part == mml_seg.rhythm)
            {
                error('L', 17, work.si);//R
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf6));
            mml_seg.allloop_flag = 1;

            mml_seg.length_check1 = 0;
            return enmPass2JumpTable.olc0;
#endif
        }


        //6221-6281
        //;==============================================================================
        //;	COMMAND	"_" [転調] , "__" [相対転調] , "_M" [Master転調]
        //;	"_{" Command[移調設定]
        //;==============================================================================
        private enmPass2JumpTable oshift()
        {
            bool cy;
            int bx;
            byte dl;
            byte ah;

            char al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;

            if (al == 'M') goto master_trans_set;
            if (al == '{') goto def_onkai_set;
            if (al != '_') goto osf00;
            work.si++;
            cy = getnum(out bx, out dl);
            work.dx = 0xe7 * 0x100 + dl;
            return enmPass2JumpTable.parset;            //__ command

        osf00:;
            cy = getnum(out bx, out dl);
            work.dx = 0xf5 * 0x100 + dl;
            return enmPass2JumpTable.parset;            //_ command

        master_trans_set:;
            work.si++;
            cy = getnum(out bx, out dl);
            work.dx = 0xb2 * 0x100 + dl;
            return enmPass2JumpTable.parset;            //_M command

        def_onkai_set:;
            work.si++;
            al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;

            ah = 0;
            if (al == '=')
            {
                ah = 0;
            }
            else if (al == '+')
            {
                ah = +1;
            }
            else if (al == '-')
            {
                ah = 0xff;//-1
            }
            else
            {
                error('{', 2, work.si);
            }

            //dos_main:;
            do
            {
                al = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
                if (al == '}') return enmPass2JumpTable.olc03;
                if (al == ' ') continue;
                if (al == 9) continue;
                if (al == ',') continue;

                if (al < 'a' || al > 'g')
                {
                    //error((byte)(work.dx >> 8), (byte)work.dx, work.si);
                    error('_', 7, work.si);//KUMA:
                }

                switch (al)
                {
                    case 'a':
                        mml_seg.def_a = ah;
                        break;
                    case 'b':
                        mml_seg.def_b = ah;
                        break;
                    case 'c':
                        mml_seg.def_c = ah;
                        break;
                    case 'd':
                        mml_seg.def_d = ah;
                        break;
                    case 'e':
                        mml_seg.def_e = ah;
                        break;
                    case 'f':
                        mml_seg.def_f = ah;
                        break;
                    case 'g':
                        mml_seg.def_g = ah;
                        break;
                }
            } while (true);
        }



        //6282-6336
        //;==============================================================================
        //;	COMMAND ")"	volume up
        //;==============================================================================
        private enmPass2JumpTable volup()
        {
            bool cy;
            int bx;
            byte al;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '%') goto volup3;
            if (ch == '^') goto volup4;

            cy = lngset(out bx, out al);
            if (al != 1) goto volup2;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf4));
            return enmPass2JumpTable.olc0;

        volup2:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe3));
            ongen_sel_vol();
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        volup3:;
            work.si++;
            cy = lngset(out bx, out al);
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe3));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        volup4:;
            work.si++;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '%') goto volup5;

            cy = lngset(out bx, out al);
            ongen_sel_vol();
            if (work.al == 0) return enmPass2JumpTable.olc03;//0なら無視

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xde));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            if (mml_seg.skip_flag == 0) return enmPass2JumpTable.olc0;
            mml_seg.acc_adr = work.di;
            return enmPass2JumpTable.olc0;

        volup5:;
            work.si++;
            cy = lngset(out bx, out al);
            if (work.al == 0) return enmPass2JumpTable.olc03;//0なら無視

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xde));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            if (mml_seg.skip_flag == 0) return enmPass2JumpTable.olc0;
            mml_seg.acc_adr = work.di;
            return enmPass2JumpTable.olc0;
        }



        //6337-6391
        //;==============================================================================
        //;	COMMAND "("	volume down
        //;==============================================================================
        private enmPass2JumpTable voldown()
        {
            bool cy;
            int bx;
            byte al;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '%') goto voldown3;
            if (ch == '^') goto voldown4;

            cy = lngset(out bx, out al);
            if (al != 1) goto voldown2;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf3));
            return enmPass2JumpTable.olc0;

        voldown2:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe2));
            ongen_sel_vol();
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        voldown3:;
            work.si++;
            cy = lngset(out bx, out al);
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe2));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        voldown4:;
            work.si++;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '%') goto voldown5;

            cy = lngset(out bx, out al);
            ongen_sel_vol();
            if (work.al == 0) return enmPass2JumpTable.olc03;//0なら無視

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xdd));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            if (mml_seg.skip_flag == 0) return enmPass2JumpTable.olc0;
            mml_seg.acc_adr = work.di;
            return enmPass2JumpTable.olc0;

        voldown5:;
            work.si++;
            cy = lngset(out bx, out al);
            if (work.al == 0) return enmPass2JumpTable.olc03;//0なら無視

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xdd));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            if (mml_seg.skip_flag == 0) return enmPass2JumpTable.olc0;
            mml_seg.acc_adr = work.di;
            return enmPass2JumpTable.olc0;
        }



        //6392-6633
        //;==============================================================================
        //;	COMMAND "M"	lfo set
        //;==============================================================================
        private enmPass2JumpTable lfoset()
        {
            bool cy;
            int bx;
            byte dl;
            char ch;
#if !efc
            if (mml_seg.part == mml_seg.rhythm)
            {
                error('M', 17, work.si);//R
            }
#endif

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al == (byte)'X') goto extlfo_set;
            if (work.al == (byte)'P') goto portaset;
            if (work.al == (byte)'D') goto depthset;
            if (work.al == (byte)'W') goto waveset;
            if (work.al == (byte)'M') goto lfomask_set;
            work.ah = 0xbf;
            if (work.al == (byte)'B') goto lfoset_main;
            work.ah = 0xf2;
            if (work.al == (byte)'A') goto lfoset_main;

            work.si--;
        lfoset_main:;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));

            int ax_p = work.ah * 0x100 + work.al;//ah(A/B)保存
            work.dx = ((byte)'M') * 0x100 + (byte)work.dx;
            get_clock(); //delay
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.ah = (byte)(ax_p >> 8);
            work.al = (byte)ax_p;

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

            if (work.al != (byte)',') goto delay_only;

            cy = getnum(out bx, out dl);// speed
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

            if (work.al != (byte)',')
            {
                error('M', 6, work.si);
            }

            cy = getnum(out bx, out dl);// depth1
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);

            if (work.al != (byte)',')
            {
                error('M', 6, work.si);
            }

            cy = getnum(out bx, out dl);// depth2
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

            return enmPass2JumpTable.olc0;

        delay_only:;

            work.si--;
            if (work.ah != 0xf2) goto delay_only2;

            m_seg.m_buf.Set(work.di - 2, new MmlDatum(0xc2));
            return enmPass2JumpTable.olc0;

        delay_only2:;

            m_seg.m_buf.Set(work.di - 2, new MmlDatum(0xb9));
            return enmPass2JumpTable.olc0;

        //;==============================================================================
        //; COMMAND "MX" LFO Speed Extended Mode Set Ver.4.0m～
        //;==============================================================================
        extlfo_set:;
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.ah = 0xbb;
            if (work.al == (byte)'B') goto extlfo_set_main;
            work.ah = 0xca;
            if (work.al == (byte)'A') goto extlfo_set_main;
            work.si--;
        extlfo_set_main:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));
            cy = getnum(out bx, out dl);
            work.al = dl;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            return enmPass2JumpTable.olc0;

        //;==============================================================================
        //; COMMAND "MD"[DEPTH SET] for PMD V3.3 -
        //; MDa, b
        //;==============================================================================
        depthset:;
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.ah = 0xbd;
            if (work.al == (byte)'B') goto depthset_main;
            work.ah = 0xd6;
            if (work.al == (byte)'A') goto depthset_main;
            work.si--;
        depthset_main:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));
            byte ah_p = work.ah;
            byte al_p = work.al;
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            if (dl != 0)// 0の場合は
            {
                goto dps_nextparam;
            }
            dl = 0;

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',')//値２を省略可
            {
                goto dps_param2set;
            }
            work.si++;
            cy = getnum(out bx, out dl);
            goto dps_param2set;
        dps_nextparam:;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',')
            {
                error('M', 6, work.si);
            }

            work.si++;
            cy = getnum(out bx, out dl);

            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

            work.ah = ah_p;
            work.al = al_p;

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',')
            {
                return enmPass2JumpTable.olc0;
            }

            work.si++;

            //; 値３(counter)   4.7a～
            work.al = 0xb7;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            ah_p = work.ah;
            al_p = work.al;

            cy = getnum(out bx, out dl);

            work.ah = ah_p;
            work.al = al_p;

            if ((((byte)work.bx) & 0x80) != 0)
            {
                error('M', 2, work.si);
            }

            if (work.ah == 0xd6) //A?
            {
                goto mdc_noa;
            }
            work.bx = (work.bx & 0xff00) | ((byte)work.bx | 0x80);
        mdc_noa:;
            work.al = (byte)work.bx;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;

        dps_param2set:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            work.ah = ah_p;
            work.al = al_p;
            return enmPass2JumpTable.olc0;

        //;==============================================================================
        //; COMMAND "MP"[PORTAMENT SET] for PMD V2.3 -
        //; MPa[, b][, c] = Mb, c, a, 255 * 1 def.b = 0, c = 1
        //;==============================================================================
        portaset:;
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.ah = 0xbf;
            if (work.al == 'B') goto portaset_main;
            work.ah = 0xf2;
            if (work.al == 'A') goto portaset_main;
            work.si--;
        portaset_main:;
            ah_p = work.ah;
            al_p = work.al;
            mml_seg.bend2 = 0;
            mml_seg.bend3 = 1;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));
            cy = getnum(out bx, out dl);
            mml_seg.bend1 = (sbyte)dl;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') goto bset;
            work.si++;
            work.dx = ((byte)'M') * 0x100 + (byte)work.dx;
            get_clock();
            mml_seg.bend2 = work.al;

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') goto bset;

            work.si++;
            cy = getnum(out bx, out dl);
            mml_seg.bend3 = dl;

        bset:;
            work.al = (byte)mml_seg.bend2;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.al = (byte)mml_seg.bend3;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.al = (byte)mml_seg.bend1;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.al = (byte)255;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            work.dx = (ah_p - 1) * 0x100 + 1;            //0f1h or 0beh
            return enmPass2JumpTable.parset;

        //;==============================================================================
        //;	COMMAND "MW" [WAVE SET] for PMD V4.0j～
        //;==============================================================================
        waveset:;
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.ah = 0xbc;
            if (work.al == 'B') goto waveset_main;
            work.ah = 0xcb;
            if (work.al == 'A') goto waveset_main;
            work.si--;
        waveset_main:;
            ah_p = work.ah;
            al_p = work.al;
            cy = getnum(out bx, out dl);
            work.ah = ah_p;
            work.al = al_p;

            work.dx = work.ah * 0x100 + (byte)work.dx;

            return enmPass2JumpTable.parset;

        //;==============================================================================
        //;	COMMAND "MM"	LFO Mask for PMD v4.2～
        //;==============================================================================
        lfomask_set:;
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.ah = 0xba;
            if (work.al == 'B') goto lfomask_set_main;
            work.ah = 0xc5;
            if (work.al == 'A') goto lfomask_set_main;
            work.si--;
        lfomask_set_main:;
            ah_p = work.ah;
            al_p = work.al;
            cy = getnum(out bx, out dl);
            work.ah = ah_p;
            work.al = al_p;

            work.dx = work.ah * 0x100 + (byte)work.dx;

            return enmPass2JumpTable.parset;
        }



        //6634-6692
        //;==============================================================================
        //;	COMMAND "*"	lfo switch
        //;==============================================================================
        private enmPass2JumpTable lfoswitch()
        {
            byte ah_p;
            byte al_p;
            bool cy;
            int bx;
            byte al;
            char ch;

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.ah = 0xf1;
#if !efc
            if (mml_seg.part == mml_seg.rhythm) goto lfoswitch_noB;// R part
#endif
            work.ah = 0xbe;
        lfoswitch_noB:;
            if (work.al == 'B') goto lfoswitch_main;
            work.ah = 0xf1;
            if (work.al == 'A') goto lfoswitch_main;
            work.si--;
        lfoswitch_main:;
            ah_p = work.ah;
            al_p = work.al;
            cy = lngset(out bx, out al);
            work.bx = ah_p * 0x100 + al_p;
            if (cy)
            {
                error('*', 6, work.si);
            }
            work.ah = work.al;
            work.al = (byte)(work.bx >> 8);
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') return enmPass2JumpTable.olc0;

            work.si++;

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
#if !efc
            if (mml_seg.part == mml_seg.rhythm)
            {
                error('*', 17, work.si);// R part
            }
#endif
            work.ah = 0xf1;
            if (work.al == 'A') goto lfoswitch_main2;
            work.ah = 0xbe;
            if (work.al == 'A') goto lfoswitch_main2;
            work.si--;
        lfoswitch_main2:;
            ah_p = work.ah;
            al_p = work.al;
            cy = lngset(out bx, out al);
            work.bx = ah_p * 0x100 + al_p;
            if (cy)
            {
                error('*', 6, work.si);
            }

            byte d = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            if (ah_p != d)//cmp bh,-2[di]	;対象が同じ?
            {
                goto lsm2_0;
            }
            work.si -= 2;//後半が有効

        lsm2_0:;
            work.ah = work.al;
            work.al = (byte)(work.bx >> 8);
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.ah));
            return enmPass2JumpTable.olc0;
        }



        //6693-6755
        //;==============================================================================
        //;	COMMAND "E"	PSG Software_envelope
        //;==============================================================================
        private enmPass2JumpTable psgenvset()
        {
            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == 'X') return extenv_set();

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xf0));
            int cx = 3;
            bool cy;
            int bx;
            byte dl;

            //pe0:;
            do
            {
                cy = getnum(out bx, out dl);
                m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

                ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
                if (ch != ',')
                {
                    error('E', 6, work.si);
                }
                cx--;
            } while (cx > 0);

            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == ',') goto extend_psgenv;
            return enmPass2JumpTable.olc0;

        //;	4.0h Extended
        extend_psgenv:;
            m_seg.m_buf.Set(work.di - 5, new MmlDatum(0xcd));
            work.si++;
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di - 1, new MmlDatum(m_seg.m_buf.Get(work.di - 1).dat & 0xf));
            dl <<= 4;
            dl &= 0xf0;
            m_seg.m_buf.Set(work.di - 1, new MmlDatum(m_seg.m_buf.Get(work.di - 1).dat | dl));
            dl = 0;

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') goto not_set_al;
            work.si++;
            cy = getnum(out bx, out dl);
        not_set_al:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            work.dx = (work.dx & 0xff00) | dl;
            return enmPass2JumpTable.olc0;
        }

        //;==============================================================================
        //;	COMMAND "EX"	Envelope Speed Extended Mode Set
        //;==============================================================================
        private enmPass2JumpTable extenv_set()
        {
            work.si++;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xc9));

            bool cy;
            int bx;
            byte dl;
            cy = getnum(out bx, out dl);

            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));

            return enmPass2JumpTable.olc0;
        }



        //6756-6775
        //;==============================================================================
        //;	COMMAND "y"	OPN Register set
        //;==============================================================================
        private enmPass2JumpTable ycommand()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xef));

            bool cy;
            int bx;
            byte al;
            cy = lngset(out bx, out al);
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));

            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            if (work.al != (byte)',')
            {
                error('y', 6, work.si);
            }

            cy = lngset(out bx, out al);
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));

            return enmPass2JumpTable.olc0;
        }



        //6776-6800
        //;==============================================================================
        //;	COMMAND "w"	PSG noise 平均周波数設定
        //;==============================================================================
        private enmPass2JumpTable psgnoise()
        {
            bool cy;
            int bx;
            byte al;
            byte dl;
            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '+') goto psgnoise_move;
            if (ch == '-') goto psgnoise_move;

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xee));

            cy = lngset(out bx, out al);

            m_seg.m_buf.Set(work.di++, new MmlDatum(al));

            return enmPass2JumpTable.olc0;

        psgnoise_move:;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xd0));
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            return enmPass2JumpTable.olc0;
        }



        //6801-6833
        //;==============================================================================
        //;	COMMAND "P"	PSG tone/noise/mix Select
        //;==============================================================================
        private enmPass2JumpTable psgpat()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xed));
            bool cy = lngset(out int bx, out byte al);
            if (cy)
            {
                error('P', 6, work.si);
            }

            if (work.al >= 4)
            {
                error('P', 2, work.si);
            }

            byte ah = work.al;
            ah &= 2;
            work.al &= 1;
            ah <<= 2;
            work.al |= ah;

            ah = work.al;
            work.al <<= 1;
            work.al |= ah;
            ah = work.al;
            work.al <<= 1;
            work.al |= ah;

            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            return enmPass2JumpTable.olc0;
        }



        //6834-6850
        //;==============================================================================
        //;	COMMAND "B"	ベンド幅の設定
        //;==============================================================================
        private enmPass2JumpTable bendset()
        {
            bool cy;
            int bx;
            byte dl;

            cy = getnum(out bx, out dl);
            if (dl >= 13)
            {
                error('B', 2, work.si);
            }

            mml_seg.bend = dl;
            if (dl != 0) return enmPass2JumpTable.olc03;

            mml_seg.alldet = 0x8000;//０が指定されたらalldet値を初期化
            return enmPass2JumpTable.olc03;
        }



        //6851-6858
        //;==============================================================================
        //;	COMMAND "I"	ピッチの設定
        //;==============================================================================
        private enmPass2JumpTable pitchset()
        {
            bool cy;
            int bx;
            byte dl;

            cy = getnum(out bx, out dl);
            mml_seg.pitch = work.bx;

            return enmPass2JumpTable.olc03;
        }



        //6859-6891
        //;==============================================================================
        //;	COMMAND	"p"	パンの設定
        //;==============================================================================
        private enmPass2JumpTable panset()
        {
            bool cy;
            int bx;
            byte dl;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == 'x') goto panset_extend;
            cy = getnum(out bx, out dl);
            if (work.bx >= 4)
            {
                error('p', 2, work.si);
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xec));
            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));

            return enmPass2JumpTable.olc0;

        panset_extend:;
            work.si++;
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xc3));
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            m_seg.m_buf.Set(work.di++, new MmlDatum(0));

            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != ',') return enmPass2JumpTable.olc0;
            work.si++;
            cy = getnum(out bx, out dl);
            if (dl == 0) return enmPass2JumpTable.olc0;

            m_seg.m_buf.Set(work.di - 1, new MmlDatum(1));
            return enmPass2JumpTable.olc0;
        }



        //6892-7091
        //;==============================================================================
        //;	COMMAND "\"	リズム音源コントロール
        //;==============================================================================

        private Tuple<char, Func<enmPass2JumpTable>>[] rcomtbl;

        private void setupRcomtbl()
        {

            rcomtbl = new Tuple<char, Func<enmPass2JumpTable>>[]
            {
                new Tuple<char, Func<enmPass2JumpTable>>('V',mstvol)
                ,new Tuple<char, Func<enmPass2JumpTable>>('v',rthvol)
                ,new Tuple<char, Func<enmPass2JumpTable>>('l',panlef)
                ,new Tuple<char, Func<enmPass2JumpTable>>('m',panmid)
                ,new Tuple<char, Func<enmPass2JumpTable>>('r',panrig)
                ,new Tuple<char, Func<enmPass2JumpTable>>('b',bdset)
                ,new Tuple<char, Func<enmPass2JumpTable>>('s',snrset)
                ,new Tuple<char, Func<enmPass2JumpTable>>('c',cymset)
                ,new Tuple<char, Func<enmPass2JumpTable>>('h',hihset)
                ,new Tuple<char, Func<enmPass2JumpTable>>('t',tamset)
                ,new Tuple<char, Func<enmPass2JumpTable>>('i',rimset)
                ,new Tuple<char, Func<enmPass2JumpTable>>( (char)0 ,null)
            };

        }

        private enmPass2JumpTable rhycom()
        {
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.bx = 0;//offset rcomtbl
                        //rc00:;
            do
            {
                if (rcomtbl[work.bx].Item1 == 0) error('\\', 1, work.si);
                if (work.al == rcomtbl[work.bx].Item1) goto rc01;
                work.bx++;
            } while (true);
        rc01:;
            return rcomtbl[work.bx].Item2();
            //return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable mstvol()
        {
            bool cy;
            int bx;
            byte al, dl;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '+') goto mstvol_sft;
            if (ch == '-') goto mstvol_sft;
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe8));

            cy = lngset(out bx, out al);
            if (cy)
            {
                error('\\', 6, work.si);
            }

            if ((byte)work.bx >= 64)
            {
                error('\\', 2, work.si);
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum((byte)work.bx));
            return enmPass2JumpTable.olc0;

        mstvol_sft:;
            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe6));
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable rthvol()
        {
            bool cy;
            int bx;
            byte al, dl;

            rhysel();

            int ax_p = work.ah * 0x100 + work.al;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch == '+') goto rhyvol_sft;
            if (ch == '-') goto rhyvol_sft;
            cy = lngset(out bx, out al);

            work.al = (byte)ax_p;
            work.ah = (byte)(ax_p >> 8);

            if (cy)
            {
                error('\\', 6, work.si);
            }

            if ((byte)work.bx >= 32)
            {
                error('\\', 2, work.si);
            }

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xea));

            return rc02();

        rhyvol_sft:;

            work.al = (byte)ax_p;
            work.ah = (byte)(ax_p >> 8);

            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe5));
            work.al &= 0b1110_0000;
            work.al >>= 5;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));

            cy = getnum(out bx, out dl);
            m_seg.m_buf.Set(work.di++, new MmlDatum(dl));
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable rc02()
        {
            work.al &= 0b1110_0000;
            work.bx &= 0xff1f;
            work.al |= (byte)work.bx;
            m_seg.m_buf.Set(work.di++, new MmlDatum(work.al));
            return enmPass2JumpTable.olc0;
        }

        private enmPass2JumpTable panlef()
        {
            work.bx = (work.bx & 0xff00) + 2;
            return rpanset();
        }

        private enmPass2JumpTable panmid()
        {
            work.bx = (work.bx & 0xff00) + 3;
            return rpanset();
        }

        private enmPass2JumpTable panrig()
        {
            work.bx = (work.bx & 0xff00) + 1;
            return rpanset();
        }

        private enmPass2JumpTable rpanset()
        {
            m_seg.m_buf.Set(work.di++, new MmlDatum(0xe9));
            rhysel();
            return rc02();
        }

        private void rhysel()
        {
            work.al = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
            work.bx = 0x0100 + (byte)work.bx;
            if (work.al == 'b') goto rsel;
            work.bx += 0x100;
            if (work.al == 's') goto rsel;
            work.bx += 0x100;
            if (work.al == 'c') goto rsel;
            work.bx += 0x100;
            if (work.al == 'h') goto rsel;
            work.bx += 0x100;
            if (work.al == 't') goto rsel;
            work.bx += 0x100;
            if (work.al == 'i') goto rsel;

            error('\\', 1, work.si);

        rsel:;
            work.al = (byte)(work.bx >> 8);
            work.al <<= 5; //0000_0111 -> 1110_0000
        }

        private enmPass2JumpTable bdset()
        {
            work.al = 1;
            return rs00();
        }

        private enmPass2JumpTable snrset()
        {
            work.al = 2;
            return rs00();
        }

        private enmPass2JumpTable cymset()
        {
            work.al = 4;
            return rs00();
        }

        private enmPass2JumpTable hihset()
        {
            work.al = 8;
            return rs00();
        }

        private enmPass2JumpTable tamset()
        {
            work.al = 16;
            return rs00();
        }

        private enmPass2JumpTable rimset()
        {
            work.al = 32;
            return rs00();
        }

        private enmPass2JumpTable rs00()
        {
            if (mml_seg.skip_flag != 0) goto rs_skip;

            char ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != 'p') goto rs01;
            work.si++;
            work.al |= 0x80;
            goto rs02;
        rs01:;
            byte cch = (byte)m_seg.m_buf.Get(work.di - 2).dat;
            if (cch != 0xeb) goto rs02;
            cch = (byte)m_seg.m_buf.Get(work.di - 1).dat;
            if ((cch & 0x80) != 0) goto rs02;
            if (mml_seg.prsok != 0x80) goto rs02;//直前byte = リズム?
            work.al |= cch;
            m_seg.m_buf.Set(work.di - 1, new MmlDatum(work.al));
            goto rsexit;
        rs02:;
            m_seg.m_buf.Set(work.di, new MmlDatum(0xeb));
            m_seg.m_buf.Set(work.di + 1, new MmlDatum(work.al));
            work.di += 2;
            if ((work.al & 0x80) == 0) goto rsexit;
            return enmPass2JumpTable.olc0;
        rsexit:;
            mml_seg.prsok = 0x80;//直前byte = リズム
            return enmPass2JumpTable.olc02;
        rs_skip:;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch != 'p') return enmPass2JumpTable.olc03;
            work.si++;
            return enmPass2JumpTable.olc03;
        }



        //7092-7163
        //;==============================================================================
        //;	MML 変数の使用
        //;==============================================================================
        private enmPass2JumpTable hscom()
        {
            char ch;
            int ax;
            //	push es
            //    mov ax, hs_seg
            //    mov es, ax
            //    assume es:hs_seg

            bool cy = lngset(out int bx, out byte al);
            if (cy) goto hscom3;
            if ((work.bx & 0xff00) != 0)
            {
                error('!', 2, work.si);
            }

            //;;	jnc hscom2
            //;;	lodsb
            //;;	cmp al,"!"
            //;;	jz hscom3
            //;;	sub al,64
            //;;	mov dx,"!"*256+7
            //;;	jc error
            //;;	cmp al,64
            //;;	jnc error
            //;;	xor ah, ah
            //;;	add ax, ax
            //;;	mov bx, offset hsbuf
            //;;	add bx, ax
            //;;	jmp hscom_main

            //hscom2:;
            ax = work.al * 2;
            work.bx = 0;//offset hsbuf2
            hs_seg.currentBuf = hs_seg.hsbuf2;
            work.bx += ax;
            goto hscom_main;

        hscom3:;
            ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
            if (ch < '!')
            {
                error('!', 6, work.si);
            }

            cy = search_hs3();
            if (!cy) goto hscom_main;
            if (work.dx != -1) goto hscom3_small;

            error('!', 27, work.si);

        hscom3_small:;
            work.bx = work.dx;

        hscom_main:;
            ax = hs_seg.currentBuf[work.bx] + hs_seg.currentBuf[work.bx + 1] * 0x100;

            //    assume es:m_seg

            if (ax == 0)
            {
                error('!', 27, work.si); // 定義されてないってばさ
            }

            mml_seg.hscomSI.Push(work.si);
            mml_seg.hsflag++;
            work.si = ax;
            return enmPass2JumpTable.olc02;// olc0
        }

        private enmPass2JumpTable hscom_exit()
        {
            mml_seg.hsflag--;
            work.si = mml_seg.hscomSI.Pop();

            return enmPass2JumpTable.olc03;
        }



        //7164-7233
        //;==============================================================================
        //;	可変長変数検索
        //;		in.	ds:si mml_buffer
        //;         es hs_seg
        //;		out.bx hs3_offset
        //;         ds:si next mml_buffer
        //;			cy=1	no_match dx = near hs3_offset
        //;==============================================================================
        private bool search_hs3()
        {
            int di_p = work.di;

            work.al = 0;
            byte ah = 0;
            work.dx = -1;
            work.bx = 0;//offset hsbuf3
            hs_seg.currentBuf = hs_seg.hsbuf3;
            work.di = work.bx + 2;
            int cx = 256;
            //hscom3_loop:;
            do
            {
                int cx_p = cx;
                int si_p = work.si;

                if ((hs_seg.currentBuf[work.bx] | hs_seg.currentBuf[work.bx + 1]) == 0) goto hscom3_next;
                cx = hs_seg.hs_length - 2;
                work.al = 0;

                //hscom3_loop2:;
                char ch;
                do
                {
                    ch = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si] : (char)0x1a;
                    if (ch < '!') goto hscom3_chk;
                    if (hs_seg.currentBuf[work.di] == 0) goto hscom3_next2;// 変数定義側が先に終わった場合は最小確認

                    byte s = (byte)(work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a);
                    byte d = hs_seg.currentBuf[work.di++];
                    if (s != d) goto hscom3_next;

                    work.al++;
                    cx--;
                } while (cx > 0);

                //hscom3_loop3:;
                char alc;
                do
                {
                    alc = work.si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[work.si++] : (char)0x1a;
                } while (alc >= '!');
                work.si--;
                goto hscom3_found;

            hscom3_chk:;
                if (hs_seg.currentBuf[work.di] == 0) goto hscom3_found;
                goto hscom3_next;

            hscom3_next2:;
                if (ah >= work.al) goto hscom3_next;
                ah = work.al;
                work.dx = work.bx;
            hscom3_next:;
                work.si = si_p;
                cx = cx_p;

                work.bx += hs_seg.hs_length;
                work.di = work.bx + 2;
                cx--;
            } while (cx > 0);
            work.di = di_p;

            work.al = ah;
            work.si += work.al;

            //    stc
            return true;

        hscom3_found:;
            //ax = si_p;
            //cx = cx_p;
            work.di = di_p;
            //    clc
            return false;
        }



        //7234-7393
        //;==============================================================================
        //;	ERRORの表示
        //;		input dl      ERROR_NUMBER
        //;			dh ERROR Command	0なら不定
        //;			si ERROR address	0なら不定
        //;			[part] part番号	0なら不定
        //;==============================================================================
        private void error(int dh, int dl, int si)
        {
            calc_line(ref si);

            string mes = "";
            //; ------------------------------------------------------------------------------
            //; filename,lineの表示
            //; ------------------------------------------------------------------------------
            if (si != 0)
            {
                mes += System.IO.Path.GetFileName(mml_seg.mml_filename);
                mes += string.Format("({0}) :", mml_seg.line);
            }

            //; ------------------------------------------------------------------------------
            //; Error番号の表示
            //; ------------------------------------------------------------------------------
            mes += err_seg.errmes_1;
            mes += dl.ToString();

            //; ------------------------------------------------------------------------------
            //; Partの表示
            //; ------------------------------------------------------------------------------
            put_part();

            //; ------------------------------------------------------------------------------
            //; Commandの表示
            //; ------------------------------------------------------------------------------
            if (dh != 0)
            {
                mes += err_seg.errmes_3;
                mes += ((char)dh).ToString();
            }

            if (!string.IsNullOrEmpty(mes)) print_mes(mes);

            //; ------------------------------------------------------------------------------
            //; Error Messageの表示
            //; ------------------------------------------------------------------------------
            print_mes(err_seg.errmes_4 + msg.get(string.Format("E01{0:00}", dl)));// err_seg.err_table[dl]);

            //; ------------------------------------------------------------------------------
            //; エラー箇所の表示
            //; ------------------------------------------------------------------------------
            if (si != 0 && mml_seg.line != 0)
            {
                mes = "";
                int di = mml_seg.linehead;
                while (di < mml_seg.mml_buf.Length && mml_seg.mml_buf[di] != (char)mc.cr)
                {
                    mes += mml_seg.mml_buf[di++];
                }
                print_mes(mes);
                int s = si - 1 - mml_seg.linehead;
                if (s >= 0) print_mes(new string(' ', s) + "^^");
            }

            error_exit(1);

        }



        //7394-
        //;==============================================================================
        //;	Error,Warning時のパート表示
        //;==============================================================================
        private void put_part()
        {
            if (mml_seg.part == 0) return;

            print_mes(err_seg.errmes_2 + ((char)('A' - 1 + mml_seg.part)).ToString());//1文字表示

            if (mml_seg.hsflag == 0) return;

#if !efc
            if (mml_seg.part != mml_seg.rhythm)
            {
                print_mes(err_seg.errmes_5);
                return;
            }
#endif
            if (mml_seg.hsflag == 1) return;

            print_mes(err_seg.errmes_5);
        }



        //7429-7505
        //;==============================================================================
        //;	Error位置のline,lineheadを計算
        //;		input DS:SI Error位置
        //; output[line] Line
        //;[linehead] Lineの頭位置
        //;			[mml_filename] MMLのファイル名
        //;==============================================================================
        private void calc_line(ref int si)
        {
            if (si == 0)
            {
                mml_seg.line = 0;
                mml_seg.linehead = 0;
                si = 0;
                return;
            }

            Stack<int> lineStack = new Stack<int>();
            int bx = 0;
            int ah = 0;// Main/Include Flag
            int dx = si;// DX=Error位置
            si = 0;//offset mml_buf

            mml_seg.line = 1;

            do
            {
                mml_seg.linehead = si;
                if (si == dx) break; //１文字目でerrorの場合

                do
                {
                    char al = si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[si++] : (char)0x1a;
                    if (si == dx) goto cl_exit;//Error位置まで来たか？

                    if (al == 0x1a)//EOF
                    {
                        mml_seg.line = 0;
                        mml_seg.linehead = 0;
                        si = 0;
                        return;
                    }

                    if (al >= 0x1a) continue;

                    if (al == 13)//CR
                    {
                        si++;// LFを飛ばす
                        mml_seg.line++;
                        break;
                    }

                    if (al == 1)//Main->Include check code
                    {
                        lineStack.Push(mml_seg.line);//Lineを保存
                        lineStack.Push(bx);//MMLのファイル名位置を保存
                        mml_seg.line = 1;//1行目から
                        ah++;//Include階層を一つ増やす
                        bx = si + 1;//MMLのファイル名位置をBXに保存
                        do
                        {
                            al = si < mml_seg.mml_buf.Length ? mml_seg.mml_buf[si++] : (char)0x1a;
                        } while (al != 0x0a);//ファイル名部分を飛ばす
                        break;
                    }
                    else if (al == 2)//Include->Main check code
                    {
                        bx = lineStack.Pop();//MMLのファイル名位置を元に戻す
                        mml_seg.line = lineStack.Pop();//Line位置を元に戻す
                        ah--;//Include階層を一つ減らす

                        si++;
                        break;
                    }

                } while (true);
            } while (true);

        cl_exit:;

            if (ah == 0)
            {
                si = dx;// Error位置をSIに戻す
                return;
            }

            si = bx;
            while (mml_seg.mml_buf[si] != 'e' && mml_seg.mml_buf[si] != 'E')//includeをスキップする
            {
                si++;
            }
            si++;

            mml_seg.mml_filename = ""; //offset mml_filename
            do
            {
                mml_seg.mml_filename += mml_seg.mml_buf[si++];//Include中にError --> MML Filenameを変更
            } while (si != mml_seg.mml_buf.Length && mml_seg.mml_buf[si] >= 0x20);
            si = dx;// Error位置をSIに戻す
            mml_seg.mml_filename = mml_seg.mml_filename.Trim();

        }



        //7506-7536
        //;==============================================================================
        //;	環境の検索
        //;	input si 環境変数名+"="
        //;       es 環境segment
        //; output es:di 環境のaddress
        //; cy	1なら無し
        //;==============================================================================
        /// <summary>
        /// 環境変数の検索
        /// </summary>
        /// <param name="siSearchEnv">検索文字(=は不要)</param>
        /// <param name="esKankyoseg">検索対象</param>
        /// <param name="index">見つけた添え字番号</param>
        /// <param name="col">値の始まる位置</param>
        /// <returns>true:見つけた</returns>
        private bool search_env(string siSearchEnv, string[] esKankyoseg, out int index, out int col)
        {
            index = -1;
            col = -1;
            if (siSearchEnv == null) return false;
            if (siSearchEnv.Length < 1) return false;
            if (esKankyoseg == null) return false;
            if (esKankyoseg.Length < 1) return false;

            for (index = 0; index < esKankyoseg.Length; index++)
            {
                if (string.IsNullOrEmpty(esKankyoseg[index])) continue;
                if (esKankyoseg[index].IndexOf(siSearchEnv) != 0) continue;
                if (esKankyoseg[index][siSearchEnv.Length - 1] != '=') continue;
                //見つけた
                col = siSearchEnv.Length;
                return true;
            }

            //見つからなかった
            index = -1;
            col = -1;
            return false;
        }



        //7619-7628
        //;==============================================================================
        //; 	usage put & exit
        //;==============================================================================
        private void usage()
        {
            print_mes(mml_seg.usames);
            error_exit(1);
        }



        //7649
        private string[] kankyo_seg;



    }
}
