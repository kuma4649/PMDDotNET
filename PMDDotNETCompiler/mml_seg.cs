using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Compiler
{
	public class mml_seg
	{
		//;==============================================================================
		//;	Work Area
		//;==============================================================================
		public string warning_mes = "Warning " + mc.eof;
		public string not_ff_mes = ": 音色ファイル名が指定されていません．" + mc.cr + mc.lf + mc.eof;
		public string ff_readerr_mes = ": 音色ファイルが読み込めません．" + mc.cr + mc.lf + mc.eof;
		public string not_pmd_mes = ": ＰＭＤが常駐していません．" + mc.cr + mc.lf + mc.eof;
		public string loop_err_mes = " : ループ終了記号 ] が足りません。" + mc.cr + mc.lf + mc.eof;
		public string mcopt_err_mes = ": 環境変数 MCOPT の記述に誤りがあります。" + (char)13 + (char)10 + mc.eof;

#if efc
		public string usames = "Usage:  EFC [/option] filename[.EML] [filename[.FF]]" + mc.cr + mc.lf + mc.cr + mc.lf
		+ "Option: /V  Compile with Tonedatas" + mc.cr + mc.lf
		+ "        /VW Write Voicefile after Compile" + mc.cr + mc.lf
		+ "        /N  Compile on OPN Mode(Default)" + mc.cr + mc.lf
		+ "        /M  Compile on OPM Mode" + mc.cr + mc.lf
		+ "        /L  Compile on OPL Mode" + mc.cr + mc.lf + mc.eof;

		public string titmes = " .EML file --> .EFC file Compiler ver " + mc.ver
				+ mc.cr + mc.lf
				+ "		Programmed by M.Kajihara(KAJA) " + mc.date
				+ mc.cr + mc.lf + mc.cr + mc.lf + mc.eof;
#else
#if !hyouka
		public string usames = "Usage:  MC"
		+ " [/option] filename[.MML] [filename[.FF]]" + mc.cr + mc.lf + mc.cr + mc.lf
		+ "Option: "
		+ "/V  Compile with Tonedatas & Messages & Filenames" + mc.cr + mc.lf
		+ "        /VW Write Tonedata after Compile" + mc.cr + mc.lf
		+ "        "
		+ "/N  Compile on OPN   Mode(Default)" + mc.cr + mc.lf
		+ "        /L  Compile on OPL   Mode" + mc.cr + mc.lf
		+ "        /M  Compile on OPM   Mode" + mc.cr + mc.lf
		+ "        /T  Compile on TOWNS Mode" + mc.cr + mc.lf
		+ "        /P  Play after Compile Complete" + mc.cr + mc.lf
		+ "        /S  Not Write Compiled File & Play" + mc.cr + mc.lf
		+ "        /A  Not Set ADPCM_File before Play" + mc.cr + mc.lf
		+ "        /O  Not Put Title Messages after Play" + mc.cr + mc.lf
		+ "        /C  Calculate & Put Total Length of Parts" + mc.cr + mc.lf
		//+ mc.eof
		;
		public string titmes = " .MML file --> .M file Compiler"
		+ " ver " + mc.ver + mc.cr + mc.lf
		+ "		Programmed by M.Kajihara(KAJA) " + mc.date
		+ mc.cr + mc.lf
		//+ mc.cr + mc.lf
		//+ mc.eof
		;
#else
		public string usames = "Usage:  MCH"
		+ " [/option] filename[.MML] [filename[.FF]]" + mc.cr + mc.lf + mc.cr + mc.lf
		+ "Option: "
		+ "/N  Compile on OPN   Mode(Default)" + mc.cr + mc.lf
		+ "        /L  Compile on OPL   Mode" + mc.cr + mc.lf
		+ "        /A  Not Set ADPCM_File before Play" + mc.cr + mc.lf
		+ "        /O  Not Put Title Messages after Play" + mc.cr + mc.lf
		+ "        /C  Calculate & Put Total Length of Parts" + mc.cr + mc.lf
		+ mc.eof;
		public string titmes = " .MML file Compiler & Player (MC.EXE評価版)"
		+ " ver " + mc.ver + mc.cr + mc.lf
		+ "		Programmed by M.Kajihara(KAJA) " + mc.date
		+ mc.cr + mc.lf
		//+ mc.cr + mc.lf + mc.eof
		;
#endif
#endif

		public  string finmes = "Compile Completed.";
		public string mes_crlf = "" + mc.cr + mc.lf + mc.eof;

		public string mes_title = mc.cr + mc.lf + "演奏を開始します。" + mc.cr + mc.lf + mc.cr + mc.lf
		+ "Title    : " + mc.eof;
		public string mes_composer = "Composer : " + mc.eof;
		public string mes_arranger = "Arranger : " + mc.eof;
		public string mes_memo = "         : " + mc.eof;

		public string mes_ppzfile = "PPZFile  : " + mc.eof;
		public string mes_ppsfile = "PPSFile  : " + mc.eof;
		public string mes_pcmfile = "PCMFile  : " + mc.eof;

		public string user_txt = "USER=";
		public string composer_txt = "COMPOSER=";
		public string arranger_txt = "ARRANGER=";
		public string mcopt_txt = "MCOPT=";

#if !tempo_old_flag
		public  int tempo = 0;
#endif
		public int timerb = 0;//b
		public int octave = 4;//b
		public int length = 0;//b
		public int zenlen = 96;//b
		public int deflng = 24;//b
		public int deflng_k = 24;//b
		public int calflg = 0;//b
		public int hsflag = 0;//b
		public int lopcnt = 0;//b
		public int volss = 0;//b
		public int volss2 = 0;//b
		public int octss = 0;//b
		public int nowvol = 0;//b
		public int line = 0;//w
		public int linehead = 0;//w
		public int length_check1 = 0;//b
		public int length_check2 = 0;//b
		public int allloop_flag = 0;//b
		public int qcommand = 0;//w
		public int acc_adr = 0;//w
		public int jump_flag = 0;//w

		public byte def_a = 0;//b
		public byte def_b = 0;//b
		public byte def_c = 0;//b
		public byte def_d = 0;//b
		public byte def_e = 0;//b
		public byte def_f = 0;//b
		public byte def_g = 0;//b

		public int master_detune = 0;//w
		public int detune = 0;//w
		public int alldet = 0;//w
		public int bend = 0;//b
		public int pitch = 0;//w

		public int bend1 = 0;//b
		public int bend2 = 0;//b
		public int bend3 = 0;//b

		public int transpose = 0;//b

		public int fm_voldown = 0;//b
		public int ssg_voldown = 0;//b
		public int pcm_voldown = 0;//b
		public int rhythm_voldown = 0;//b
		public int ppz_voldown = 0;//b

		public int fm_voldown_flag = 0;//b
		public int ssg_voldown_flag = 0;//b
		public int pcm_voldown_flag = 0;//b
		public int rhythm_voldown_flag = 0;//b
		public int ppz_voldown_flag = 0;//b

		public byte[] fmvol = new byte[]
		{
			127-0x2a //VOLUME	00
			,127-0x28 //VOLUME	01
			,127-0x25 //VOLUME	02
			,127-0x22 //VOLUME	03
			,127-0x20 //VOLUME	04
			,127-0x1d //VOLUME	05
			,127-0x1a //VOLUME	06
			,127-0x18 //VOLUME	07
			,127-0x15 //VOLUME	08
			,127-0x12 //VOLUME	09
			,127-0x10 //VOLUME	10
			,127-0x0d //VOLUME	11
			,127-0x0a //VOLUME	12
			,127-0x08 //VOLUME	13
			,127-0x05 //VOLUME	14
			,127-0x02 //VOLUME	15
			,127-0x00 //VOLUME	16
		};

#if !efc

		public int pcm_vol_ext = 0;//b

		//ＰＳＧ音色のパターン
		public byte[][] psgenvdat = new byte[][]{
			new byte[]{  0, 0,0,0 }// @0 ﾋｮｳｼﾞｭﾝ
			,new byte[]{ 2,255,0,1 }// @1 Synth 1 
			,new byte[]{ 2,254,0,1 }// @2 Synth 2
			,new byte[]{ 2,254,0,8 }// @3 Synth 3
			,new byte[]{ 2,255,24,1 }// @4 E.Piano 1
			,new byte[]{ 2,254,24,1 }// @5 E.Piano 2
			,new byte[]{ 2,254,4,1 }// @6 Glocken/Malimba
			,new byte[]{ 2,1,0,1 }// @7 Strings
			,new byte[]{ 1,2,0,1 }// @8 Brass 1
			,new byte[]{ 1,2,24,1 }// @9 Brass 2
		};

		public int max_part = 11;
		public int fm = 0;
		public int fm2 = 1;
		public int psg = 2;
		public int pcm = 3;
		public int pcm_ex = 4;

#else

		public int max_part =	126;

#endif

		public int pcmpart = 10;
		public int rhythm2 = 11;
		public int rhythm = 18;

		public int part = 0;//b
		public int ongen = 0;//b
		public int pass = 0;//b

		public int maxprg = 0;//b
		public int kpart_maxprg = 0;//b
		public int lastprg = 0;//w

		public int prsok = 0;//b 直前のbyte
                             // bit 1 ･･･ 音長
                             // bit 2 ･･･ 加工
							 // bit 3 ･･･ +タイ
							 // bit 4 ･･･ ポルタ
							 // bit 7 ･･･ リズム

		public int prg_flg = 0;//b
		public int ff_flg = 0;//b
		public int x68_flg = 0;//b
		public int towns_flg = 0;//b
		public int dt2_flg = 0;//b
		public int opl_flg = 0;//b
		public int play_flg = 0;//b
		public int save_flg = 0;//b
		public int pmd_flg = 0;//b
		public int ext_detune = 0;//b
		public int ext_lfo = 0;//b
		public int ext_env = 0;//b
		public int memo_flg = 0;//b
		public int pcm_flg = 0;//b
		public int lc_flag = 0;//b
		public int loop_def = 0; //b

		public int adpcm_flag = 255;//b	-1

		public int sp_push = 0;//w	0

		public int ss_speed = 0;//b	0
		public int ss_depth = 0;//b	0
		public int ss_length = 0;//b	0
		public int ss_tie = 0;//b	0

		public int ge_delay = 0;//b	0
		public int ge_depth = 0;//b	0
		public int ge_depth2 = 0;//b	0
		public int ge_tie = 0;//b	0
		public int ge_flag1 = 0;//b	0
		public int ge_flag2 = 0;//b	0
		public int ge_dep_flag = 0;//b	0

		public int skip_flag = 0;//b	0
		public int tie_flag = 0;//b	0
		public int porta_flag = 0;//b	0

		public int fm3_partchr1 = 0;//b	0
		public int fm3_partchr2 = 0;//b	0
		public int fm3_partchr3 = 0;//b	0
		public int fm3_ofsadr = 0;//w	0
		public char[] pcm_partchr = new char[8] {
			(char)0,
			(char)0,
			(char)0,
			(char)0,
			(char)0,
			(char)0,
			(char)0,
			(char)0 };//b	0
		public int pcm_ofsadr = 0;//w	0

		// offset,max,rot
		public byte[] oplprg_table = new byte[]{
			08,001,0  // alg
			,08,007,1 // fbl
			
			,04,015,4 // ar
			,04,015,0 // dr
			,06,015,0 // rr
			,06,015,4 // sl
			,02,063,0 // tl
			,02,003,6 // ksl
			,00,015,0 // ml
			,00,001,4 // ksr
			,00,001,5 // egt
			,00,001,6 // vib
			,00,001,7 // am
			
			,05,015,4 // ar
			,05,015,0 // dr
			,07,015,0 // rr
			,07,015,4 // sl
			,03,063,0 // tl
			,03,003,6 // ksl
			,01,015,0 // ml
			,01,001,4 // ksr
			,01,001,5 // egt
			,01,001,6 // vib
			,01,001,7 // am
		};

		public int mml_endadr;//w

		public int loopnest = 32;// MAX 32 NEST
		public byte[] loptbl = new byte[32 * 2];// loopnest * 2];
		public byte[] lextbl = new byte[32 * 2];// loopnest * 2];

		//prgbuf_start label   byte //構造体かな?

		public int prgbuf_length = 26;
		public byte newprg_num;//b
		public byte alg_fb;//b
		public byte[][] slot = new byte[4][] { new byte[6], new byte[6], new byte[6], new byte[6] };//b
		public string prg_name = null;//b

		public byte[] oplbuf = new byte[16];//b
		public byte[] prg_num = new byte[256];//b

		public string mml_filename;//b
		public string mml_filename2 = "";// include用
		public int ppzfile_adr;//w
		public int ppsfile_adr;//w
		public int pcmfile_adr;//w
		public int title_adr;//w
		public int composer_adr;//w
		public int arranger_adr;//w
		public int[] memo_adr = new int[129];//w
		public string composer_seg = null;//w
		public string arranger_seg = null;//w
		//public string composer_adr;//w
		//public string arranger_adr;//w

		public string mml_buf = null;//61*1024-1 dup(?); max 61k(.mml file)
		public byte mmlbuf_end;//b

		public Stack<int> hscomSI = new Stack<int>();

        //mml_seg ends


    }
}
