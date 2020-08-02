using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PW
    {
        public object lockObj = new object();
        public object SystemInterrupt = new object();

        private int _status = 0;
        public int Status
        {
            get { lock (lockObj) { return _status; } }
            set { lock (lockObj) { _status = value; } }
        }

        public int maxLoopCount { get; internal set; } = -1;
        public int nowLoopCounter { get; internal set; } = -1;

        //DotNET独自
        public MmlDatum[] md { get; internal set; }
        public MmlDatum[] crtEfcDat { get; internal set; }
        public Func<object>[] currentCommandTable { get; internal set; }
        public MmlDatum[] inst = null;
        public bool usePPSDRV = false;
        public OPNATimer timer = null;
        public ulong timeCounter = 0L;





        //PMD.ASM 7-53
        public const string ver = "4.8s";

        public int vers = 0x48;
        public string verc = "s";
        public const string date = "Jan.22nd 2020";

        public int mdata_def = 16;
        public int voice_def = 8;
        public int effect_def = 4;
        public int key_def = 1;

        public string _myname = "PMD     COM";

        public int va = 0;//１の時ＶＡMSDOS用
        public int board2 = 0;//１の時ボード２/音美ちゃん有り
        public int adpcm = 0;//１の時ADPCM使用
        public int ademu = 0;//１の時ADPCM Emulate
        public int pcm = 0;//１の時PCM使用
        public int ppz = 0;//１の時PPZ8使用
        public int sync = 0;//１の時MIDISYNC使用
        public int vsync = 0;//１の時VSyncを止める
        public string resmes = "PMD ver." + ver;
        public int fmvd_init = 16;//９８は８８よりもＦＭ音源を小さく



        //PMD.ASM 114-116
        public int pmdvector = 0x60;//PMD用の割り込みベクトル
        public int ppsdrv = 0x64;//ppsdrvの割り込みベクトル
        public int ppz_vec = 0x7f;//ppz8の割り込みベクトル


        //PMD.ASM 260-277
        //;==============================================================================
        //;	定数
        //;==============================================================================
        public int ms_cmd = 0x000;// ８２５９マスタポート
        public int ms_msk = 0x002;// ８２５９マスタ／マスク
        public int sl_cmd = 0x008;// ８２５９スレーブポート
        public int sl_msk = 0x00a;// ８２５９スレーブ／マスク



        //PMD.ASM 279-306
        //;==============================================================================
        //;	Program Start
        //;==============================================================================
        //int60_head:	jmp short int60_main
        //db	'PMD'	;+2  常駐チェック用
        //db  vers	;+5
        //db verc;+6
        public int int60ofs;//	?	;+7
        public int int60seg;//	?	;+9
        public int int5ofs;//	?	;+11
        public int int5seg;//	?	;+13
        public int maskpush;//	?	;+15
        public int vector;//	?	;+16
        public int int_level;//	?	;+18

        public int _p = 2;
        public int _m = 3;
        public int _d = 4;
        public int _vers = 5;
        public int _verc = 6;
        public int _int60ofs = 7;
        public int _int60seg = 9;
        public int _int5ofs = 11;
        public int _int5seg = 13;
        public int _maskpush = 15;
        public int _vector = 16;
        public int _int_level = 18;



        //PMD.ASM 2077
        public byte com_end_0c0h = 0xf7;



        //PMD.ASM 4859
        public byte[] vol_tbl = new byte[] { 0, 0, 0, 0 };



        //PMD.ASM 5560
        public ushort seed;

        //7972-8029
        //;==============================================================================
        //;	WORK AREA
        //;==============================================================================
        public ushort fm_port1;// w	FM音源 I/O port work(1)
        public ushort fm_port2;// w FM音源 I/O port work(2)
        public ushort ds_push;// w INT60用 ds push
        public ushort dx_push;// w INT60用 dx push
        public byte ah_push;// b INT60用 ah push
        public byte al_push;// b INT60用 al push
        public byte partb;//b 処理中パート番号
        public byte tieflag;//b &のフラグ
        public byte volpush_flag;//b 次の１音音量down用のflag
        public byte rhydmy;//b R part ダミー演奏データ
        public byte fmsel;//b FM 表か裏か flag
        public byte[] fmKeyOnDataTbl = new byte[6];//KUMA: 以下６つのパラメータの実体
        //public byte[] omote_key = new byte[] { 0, 0, 0 };
        public byte omote_key1Ptr = 0;//b FM keyondata表1
        public byte omote_key2Ptr = 1;//b  FM keyondata表2
        public byte omote_key3Ptr = 2;//b FM keyondata表3
        //public byte[] ura_key = new byte[] { 0, 0, 0 };
        public byte ura_key1Ptr = 3;//b FM keyondata裏1
        public byte ura_key2Ptr = 4;//b FM keyondata裏2
        public byte ura_key3Ptr = 5;//b FM keyondata裏3
        public byte loop_work;//b Loop Work
        public byte ppsdrv_flag;//b ppsdrv flag
        public ushort prgdat_adr2;// w 曲データ中音色データ先頭番地(効果音用)
        public ushort pcmrepeat1;// w PCMのリピートアドレス1
        public ushort pcmrepeat2;// w PCMのリピートアドレス2
        public ushort pcmrelease;// w PCMのRelease開始アドレス
        public byte lastTimerAtime;// b 一個前の割り込み時のTimerATime値
        public byte music_flag;// b B0:次でMSTART 1:次でMSTOP のFlag
        public byte slotdetune_flag;// b FM3 Slot Detuneを使っているか
        public byte slot3_flag;// b FM3 Slot毎 要効果音モードフラグ
        public ushort eoi_adr;// w EOIをsendするI/Oアドレス
        public byte eoi_data;// b EOI用のデータ
        public ushort mask_adr;// w MaskをするI/Oアドレス
        public byte mask_data;// b Mask用のデータ(OrでMask)
        public byte mask_data2;// b Mask用のデータ(AndでMask解除)
        public ushort ss_push;// w FMint中 SSのpush
        public ushort sp_push;// w FMint中 SPのpush
        public byte fm3_alg_fb;// b FM3chの最後に定義した音色のalg/fb
        public byte af_check;// b FM3chのalg/fbを設定するかしないかflag
        public byte ongen;// b 音源 0=無し/2203 1=2608
        public byte lfo_switch;// b	局所LFOスイッチ

        public byte[] rhydat = new byte[]{//; ドラムス用リズムデータ
            //PT PAN/VOLUME  KEYON
            0x18,0b1101_1111,0b0000_0001,//バス
            0x19,0b1101_1111,0b0000_0010,//スネア
            0x1c,0b0101_1111,0b0001_0000,//タム[LOW]
            0x1c,0b1101_1111,0b0001_0000,//タム[MID]
            0x1c,0b1001_1111,0b0001_0000,//タム[HIGH]
            0x1d,0b1101_0011,0b0010_0000,//リム
            0x19,0b1101_1111,0b0000_0010,//クラップ
            0x1b,0b1001_1100,0b1000_1000,//Cハイハット
            0x1a,0b1001_1101,0b0000_0100,//Oハイハット
            0x1a,0b1101_1111,0b0000_0100,//シンバル
            0x1a,0b0101_1110,0b0000_0100,//RIDEシンバル
        };

        //PMD.ASM 8030-
        public byte open_work = 0;//label byte
        public int mmlbuf = 0;//Musicdataのaddress+1
        public int tondat = 0;//Voicedataのaddress
        public int efcdat = 0;//FM Effecdataのaddress
        public int fm1_port1 = 0;// FM音源 I/O port(表1)
        public int fm1_port2 = 0;//FM音源 I/O port(表2)
        public int fm2_port1 = 0;//FM音源 I/O port(裏1)
        public int fm2_port2 = 0;//FM音源 I/O port(裏2)
        public int fmint_ofs = 0;//FM割り込みフックアドレス offset
        public int fmint_seg = 0;//FM割り込みフックアドレス address
        public int efcint_ofs = 0;//効果音割り込みフックアドレス offset
        public int efcint_seg = 0;//効果音割り込みフックアドレス address
        public int prgdat_adr = 0;//曲データ中音色データ先頭番地
        public int radtbl = 0;//R part offset table 先頭番地
        public int rhyadr = 0;//R part 演奏中番地
        public byte rhythmmask = 0;//Rhythm音源のマスク x8c/10hのbitに対応
        public byte board = 0;//FM音源ボードあり／なしflag
        public byte key_check = 0;//ESC/GRPH key Check flag
        public byte fm_voldown = 0;//FM voldown 数値
        public byte ssg_voldown = 0;//PSG voldown 数値
        public byte pcm_voldown = 0;//PCM voldown 数値
        public byte rhythm_voldown = 0;//RHYTHM voldown 数値
        public byte prg_flg = 0;//曲データに音色が含まれているかflag
        public byte x68_flg = 0;//OPM flag
        public byte status = 0;// status1
        public byte status2 = 0;//status2
        public byte tempo_d = 0;//tempo(TIMER-B)
        public byte fadeout_speed = 0;//Fadeout速度
        public byte fadeout_volume = 0;//Fadeout音量
        public byte tempo_d_push = 0;//tempo(TIMER-B) / 保存用
        public byte syousetu_lng = 0;//小節の長さ
        public byte opncount = 0;//最短音符カウンタ
        public byte TimerAtime = 0;//TimerAカウンタ
        public byte effflag = 0;//PSG効果音発声on/off flag
        public byte psnoi = 0;//PSG noise周波数
        public byte psnoi_last = 0;//PSG noise周波数(最後に定義した数値)
        public byte fm_effec_num = 0;// 発声中のFM効果音番号
        public byte fm_effec_flag = 0;//FM効果音発声中flag(1)
        public byte disint = 0;// FM割り込み中に割り込みを禁止するかflag
        public byte pcmflag = 0;//PCM効果音発声中flag
        public int pcmstart = 0;//PCM音色のstart値
        public int pcmstop = 0;//PCM音色のstop値
        public byte pcm_effec_num = 0;//発声中のPCM効果音番号
        public int _pcmstart = 0;//PCM効果音のstart値
        public int _pcmstop = 0;//PCM効果音のstop値
        public int _voice_delta_n = 0;//PCM効果音のdelta_n値
        public byte _pcmpan = 0;//PCM効果音のpan
        public byte _pcm_volume = 0;//PCM効果音のvolume
        public byte rshot_dat = 0;//リズム音源 shot flag
        public byte[] rdat = new byte[6];//リズム音源 音量/パンデータ
        public byte rhyvol = 0b0011_1100;//リズムトータルレベル
        public int kshot_dat = 0;//ＳＳＧリズム shot flag
        public int ssgefcdat = 0;// efftbl  PSG Effecdataのaddress
        public int ssgefclen = 0;// efftblend-efftbl PSG Effecdataの長さ
        public byte play_flag = 0;// play flag
        public byte pause_flag = 0;// pause flag
        public byte fade_stop_flag = 0;//Fadeout後 MSTOPするかどうかのフラグ
        public byte kp_rhythm_flag = 0;//K/RpartでRhythm音源を鳴らすかflag
        public byte TimerBflag = 0;//TimerB割り込み中？フラグ
        public byte TimerAflag = 0;//TimerA割り込み中？フラグ
        public byte int60flag = 0;//INT60H割り込み中？フラグ
        public byte int60_result = 0;//INT60Hの実行ErrorFlag
        public byte pcm_gs_flag = 0;//ADPCM使用 許可フラグ(0で許可)
        public byte esc_sp_key = 0;// ESC +?? Key Code
        public byte grph_sp_key = 0;// GRPH+?? Key Code
        public byte rescut_cant = 0;// 常駐解除禁止フラグ
        public ushort slot_detune1 = 0;//FM3 Slot Detune値 slot1
        public ushort slot_detune2 = 0;// FM3 Slot Detune値 slot2
        public ushort slot_detune3 = 0;// FM3 Slot Detune値 slot3
        public ushort slot_detune4 = 0;// FM3 Slot Detune値 slot4
        public int wait_clock = 0;// FM ADDRESS-DATA間 Loop $の回数
        public int wait1_clock = 0;//loop $ １個の速度
        public byte ff_tempo = 0;//早送り時のTimerB値
        public byte pcm_access = 0;//PCMセット中は 1
        public byte TimerB_speed = 0;// TimerBの現在値(=ff_tempoならff中)
        public byte fadeout_flag = 0;// 内部からfoutを呼び出した時1
        public byte adpcm_wait = 0;//ADPCM定義の速度
        public byte revpan = 0;//PCM86逆走flag
        public byte pcm86_vol = 0;//PCM86の音量をSPBに合わせるか?
        public ushort syousetu = 0;//小節カウンタ
        public byte int5_flag = 0;//FM音源割り込み中？フラグ
        public byte port22h = 0;//OPN-PORT 22H に最後に出力した値(hlfo)
        public byte tempo_48 = 0;// 現在のテンポ(clock= 48 tの値)
        public byte tempo_48_push = 0;//現在のテンポ(同上/保存用)
        public byte rew_sp_key = 0;//GRPH+?? (rew) Key Code
        public byte intfook_flag = 0;//int_fookのflag B0:TB B1:TA
        public byte skip_flag = 0;//normal:0 前方SKIP中:1 後方SKIP中:2
        public byte _fm_voldown = 0;// FM voldown 数値(保存用)
        public byte _ssg_voldown = 0;// PSG voldown 数値(保存用)
        public byte _pcm_voldown = 0;// PCM voldown 数値(保存用)
        public byte _rhythm_voldown = 0;//RHYTHM voldown 数値(保存用)
        public byte _pcm86_vol = 0;// PCM86の音量をSPBに合わせるか? (保存用)
        public byte mstart_flag = 0;//mstartする時に１にするだけのflag
        public byte[] mus_filename = new byte[13];// 曲のFILE名バッファ
        public byte mmldat_lng = 0;//曲データバッファサイズ(KB)
        public byte voicedat_lng = 0;// 音色データバッファサイズ(KB)
        public byte effecdat_lng = 0;// 効果音データバッファサイズ(KB)
        public byte[] rshot = new byte[] { 0, 0, 0, 0, 0, 0 };// リズム音源 shot inc flags
        //public byte rshot_bd = 0;// リズム音源 shot inc flag(BD)
        //public byte rshot_sd = 0;// リズム音源 shot inc flag(SD)
        //public byte rshot_sym = 0;// リズム音源 shot inc flag(CYM)
        //public byte rshot_hh = 0;// リズム音源 shot inc flag(HH)
        //public byte rshot_tom = 0;// リズム音源 shot inc flag(TOM)
        //public byte rshot_rim = 0;// リズム音源 shot inc flag(RIM)
        public byte[] rdump = new byte[] { 0, 0, 0, 0, 0, 0 };// リズム音源 dump inc flags
        //public byte rdump_bd = 0;// リズム音源 dump inc flag(BD)
        //public byte rdump_sd = 0;// リズム音源 dump inc flag(SD)
        //public byte rdump_sym = 0;// リズム音源 dump inc flag(CYM)
        //public byte rdump_hh = 0;// リズム音源 dump inc flag(HH)
        //public byte rdump_tom = 0;// リズム音源 dump inc flag(TOM)
        //public byte rdump_rim = 0;// リズム音源 dump inc flag(RIM)
        public byte ch3mode = 0;// ch3 Mode
        public byte ch3mode_push = 0;// ch3 Mode(効果音発音時用push領域)
        public byte ppz_voldown = 0;// PPZ8 voldown 数値
        public byte _ppz_voldown = 0;//PPZ8 voldown 数値(保存用)
        public int ppz_call_ofs = 0;// PPZ8call用 far call address
        public int ppz_call_seg = 0;// seg値はPPZ8常駐checkを兼ねる,0で非常駐
        public byte p86_freq = 8;//PMD86のPCM再生周波数
                                 //if	pcm* board2
        public int p86_freqtable = 0;//offset pcm_tune_data
                                     //else
                                     //public int p86_freqtable = 0;// PMD86のPCM再生周波数table位置
                                     //endif
        public byte adpcm_emulate = 0;// PMDPPZEでADPCMエミュレート中か


        public MmlDatum[] rd = null;
        public MmlDatum[] rdDmy = new MmlDatum[] { new MmlDatum(0xff) };

        //8153-8247
        //;	演奏中のデータエリア

        public class partWork
        { //qq  struc
            public ushort address;// w?	; 2 ｴﾝｿｳﾁｭｳ ﾉ ｱﾄﾞﾚｽ
            public ushort partloop;// w? ; 2 ｴﾝｿｳ ｶﾞ ｵﾜｯﾀﾄｷ ﾉ ﾓﾄﾞﾘｻｷ
            public byte leng;// b? ; 1 ﾉｺﾘ LENGTH
            public byte qdat;// b? ; 1 gatetime(q/Q値を計算した値)
            public ushort fnum;// w? ; 2 ｴﾝｿｳﾁｭｳ ﾉ BLOCK/FNUM
            public ushort detune;// w? ; 2 ﾃﾞﾁｭｰﾝ
            //+10
            public ushort lfodat;// w? ; 2 LFO DATA
            public ushort porta_num;// w? ; 2 ポルタメントの加減値（全体）
            public ushort porta_num2;// w? ; 2 ポルタメントの加減値（一回）
            public ushort porta_num3;// w? ; 2 ポルタメントの加減値（余り）
            public byte volume;// b? ; 1 VOLUME
            public byte shift;// b? ; 1 ｵﾝｶｲ ｼﾌﾄ ﾉ ｱﾀｲ
            //+20
            public byte delay;// b? ; 1 LFO[DELAY]
            public byte speed;// b? ; 1 [SPEED]
            public byte step;// b? ; 1 [STEP]
            public byte time;// b? ; 1 [TIME]
            public byte delay2;// b? ; 1 [DELAY_2]
            public byte speed2;// b? ; 1 [SPEED_2]
            public byte step2;// b? ; 1 [STEP_2]
            public byte time2;// b? ; 1 [TIME_2]
            public byte lfoswi;// b? ; 1 LFOSW.B0/tone B1/vol B2/同期 B3/porta
                               //    ;          B4/tone B5/vol B6/同期
            public byte volpush;// b? ; 1 Volume PUSHarea
            //+30
            public byte mdepth;// b? ; 1 M depth
            public byte mdspd;// b? ; 1 M speed
            public byte mdspd2;// b? ; 1 M speed_2
            public byte envf;// b? ; 1 PSG ENV. [START_FLAG] / -1でextend
            public byte eenv_count;// b? ; 1 ExtendPSGenv/No=0 AR=1 DR=2 SR=3 RR=4
            public byte eenv_ar;// b? ; 1 /AR /旧pat
            public byte eenv_dr;// b? ; 1 /DR /旧pv2
            public byte eenv_sr;// b? ; 1 /SR /旧pr1
            public byte eenv_rr;// b? ; 1 /RR /旧pr2
            public byte eenv_sl;// b? ; 1 /SL
            //+40
            public byte eenv_al;// b? ; 1 /AL
            public byte eenv_arc;// b? ; 1 /ARのカウンタ /旧patb
            public byte eenv_drc;// b? ; 1 /DRのカウンタ
            public byte eenv_src;// b? ; 1 /SRのカウンタ /旧pr1b
            public byte eenv_rrc;// b? ; 1 /RRのカウンタ /旧pr2b
            public byte eenv_volume;// b? ; 1 /Volume値(0～15)/旧penv
            public byte extendmode;// b? ; 1 B1/Detune B2/LFO B3/Env Normal/Extend
            public byte fmpan;// b? ; 1 FM Panning + AMD + PMD
            public byte psgpat;// b? ; 1 PSG PATTERN[TONE / NOISE / MIX]
            public byte voicenum;// b? ; 1 音色番号
            //+50
            public byte loopcheck;// b? ; 1 ループしたら１ 終了したら３
            public byte carrier;// b? ; 1 FM Carrier
            public byte slot1;// b? ; 1 SLOT 1 ﾉ TL
            public byte slot3;// b? ; 1 SLOT 3 ﾉ TL
            public byte slot2;// b? ; 1 SLOT 2 ﾉ TL
            public byte slot4;// b? ; 1 SLOT 4 ﾉ TL
            public byte slotmask;// b? ; 1 FM slotmask
            public byte neiromask;// b? ; 1 FM 音色定義用maskdata
            public byte lfo_wave;// b? ; 1 LFOの波形
            public byte partmask;// b 1 PartMask b0:通常 b1:効果音 b2:NECPCM用
                                 //          ;   b3:none b4:PPZ/ADE用 b5:s0時 b6:m b7:一時
                                 //+60
            public byte keyoff_flag;// b? ; 1 KeyoffしたかどうかのFlag
            public byte volmask;// b? ; 1 音量LFOのマスク
            public byte qdata;// b? ; 1 qの値
            public byte qdatb;// b?	; 1 Qの値
            public byte hldelay;// b? ; 1 HardLFO delay
            public byte hldelay_c;// b? ; 1 HardLFO delay Counter
            public ushort _lfodat;// w? ; 2 LFO DATA
            public byte _delay;// b? ; 1 LFO[DELAY]
            public byte _speed;// b? ; 1	[SPEED]
            public byte _step;// b? ; 1	[STEP]
            public byte _time;// b? ; 1	[TIME]
            public byte _delay2;// b? ; 1	[DELAY_2]
            public byte _speed2;// b? ; 1	[SPEED_2]
            public byte _step2;// b? ; 1	[STEP_2]
            public byte _time2;// b? ; 1	[TIME_2]
            public byte _mdepth;// b? ; 1 M depth
            public byte _mdspd;// b? ; 1 M speed
            public byte _mdspd2;// b? ; 1 M speed_2
            public byte _lfo_wave;// b?	; 1 LFOの波形
            public byte _volmask;// b? ; 1 音量LFOのマスク
            public byte mdc;// b? ; 1 M depth Counter(変動値)
            public byte mdc2;// b? ; 1 M depth Counter
            public byte _mdc;// b? ; 1 M depth Counter(変動値)
            public byte _mdc2;// b? ; 1 M depth Counter
            public byte onkai;//b 1 演奏中の音階データ(0ffh:rest)
            public byte sdelay;//b?; 1 Slot delay
            public byte sdelay_c;//b? ; 1 Slot delay counter
            public byte sdelay_m;//b? ; 1 Slot delay Mask
            public byte alg_fb;//b? ; 1 音色のalg/fb
            public byte keyon_flag;// b 1 新音階/休符データを処理したらinc
            public byte qdat2;// b? ; 1 q 最低保証値
            public ushort fnum2;// w? ; 2 ppz8/pmd86用fnum値上位
            public byte onkai_def;// b 1 演奏中の音階データ(転調処理前 / ?fh:rest)
            public byte shift_def;// b? ; 1 マスター転調値
            public byte qdat3;// b? ; 1 q Random

            public void Clear()
            {
                address = 0;// w?	; 2 ｴﾝｿｳﾁｭｳ ﾉ ｱﾄﾞﾚｽ
                partloop = 0;// w? ; 2 ｴﾝｿｳ ｶﾞ ｵﾜｯﾀﾄｷ ﾉ ﾓﾄﾞﾘｻｷ
                leng = 0;// b? ; 1 ﾉｺﾘ LENGTH
                qdat = 0;// b? ; 1 gatetime(q/Q値を計算した値)
                fnum = 0;// w? ; 2 ｴﾝｿｳﾁｭｳ ﾉ BLOCK/FNUM
                detune = 0;// w? ; 2 ﾃﾞﾁｭｰﾝ
                lfodat = 0;// w? ; 2 LFO DATA
                porta_num = 0;// w? ; 2 ポルタメントの加減値（全体）
                porta_num2 = 0;// w? ; 2 ポルタメントの加減値（一回）
                porta_num3 = 0;// w? ; 2 ポルタメントの加減値（余り）
                volume = 0;// b? ; 1 VOLUME
                shift = 0;// b? ; 1 ｵﾝｶｲ ｼﾌﾄ ﾉ ｱﾀｲ
                delay = 0;// b? ; 1 LFO[DELAY]
                speed = 0;// b? ; 1 [SPEED]
                step = 0;// b? ; 1 [STEP]
                time = 0;// b? ; 1 [TIME]
                delay2 = 0;// b? ; 1 [DELAY_2]
                speed2 = 0;// b? ; 1 [SPEED_2]
                step2 = 0;// b? ; 1 [STEP_2]
                time2 = 0;// b? ; 1 [TIME_2]
                lfoswi = 0;// b? ; 1 LFOSW.B0/tone B1/vol B2/同期 B3/porta
                volpush = 0;// b? ; 1 Volume PUSHarea
                mdepth = 0;// b? ; 1 M depth
                mdspd = 0;// b? ; 1 M speed
                mdspd2 = 0;// b? ; 1 M speed_2
                envf = 0;// b? ; 1 PSG ENV. [START_FLAG] / -1でextend
                eenv_count = 0;// b? ; 1 ExtendPSGenv/No=0 AR=1 DR=2 SR=3 RR=4
                eenv_ar = 0;// b? ; 1 /AR /旧pat
                eenv_dr = 0;// b? ; 1 /DR /旧pv2
                eenv_sr = 0;// b? ; 1 /SR /旧pr1
                eenv_rr = 0;// b? ; 1 /RR /旧pr2
                eenv_sl = 0;// b? ; 1 /SL
                eenv_al = 0;// b? ; 1 /AL
                eenv_arc = 0;// b? ; 1 /ARのカウンタ /旧patb
                eenv_drc = 0;// b? ; 1 /DRのカウンタ
                eenv_src = 0;// b? ; 1 /SRのカウンタ /旧pr1b
                eenv_rrc = 0;// b? ; 1 /RRのカウンタ /旧pr2b
                eenv_volume = 0;// b? ; 1 /Volume値(0～15)/旧penv
                extendmode = 0;// b? ; 1 B1/Detune B2/LFO B3/Env Normal/Extend
                fmpan = 0;// b? ; 1 FM Panning + AMD + PMD
                psgpat = 0;// b? ; 1 PSG PATTERN[TONE / NOISE / MIX]
                voicenum = 0;// b? ; 1 音色番号
                loopcheck = 0;// b? ; 1 ループしたら１ 終了したら３
                carrier = 0;// b? ; 1 FM Carrier
                slot1 = 0;// b? ; 1 SLOT 1 ﾉ TL
                slot3 = 0;// b? ; 1 SLOT 3 ﾉ TL
                slot2 = 0;// b? ; 1 SLOT 2 ﾉ TL
                slot4 = 0;// b? ; 1 SLOT 4 ﾉ TL
                slotmask = 0;// b? ; 1 FM slotmask
                neiromask = 0;// b? ; 1 FM 音色定義用maskdata
                lfo_wave = 0;// b? ; 1 LFOの波形
                partmask = 0;// b 1 PartMask b0:通常 b1:効果音 b2:NECPCM用
                keyoff_flag = 0;// b? ; 1 KeyoffしたかどうかのFlag
                volmask = 0;// b? ; 1 音量LFOのマスク
                qdata = 0;// b? ; 1 qの値
                qdatb = 0;// b?	; 1 Qの値
                hldelay = 0;// b? ; 1 HardLFO delay
                hldelay_c = 0;// b? ; 1 HardLFO delay Counter
                _lfodat = 0;// w? ; 2 LFO DATA
                _delay = 0;// b? ; 1 LFO[DELAY]
                _speed = 0;// b? ; 1	[SPEED]
                _step = 0;// b? ; 1	[STEP]
                _time = 0;// b? ; 1	[TIME]
                _delay2 = 0;// b? ; 1	[DELAY_2]
                _speed2 = 0;// b? ; 1	[SPEED_2]
                _step2 = 0;// b? ; 1	[STEP_2]
                _time2 = 0;// b? ; 1	[TIME_2]
                _mdepth = 0;// b? ; 1 M depth
                _mdspd = 0;// b? ; 1 M speed
                _mdspd2 = 0;// b? ; 1 M speed_2
                _lfo_wave = 0;// b?	; 1 LFOの波形
                _volmask = 0;// b? ; 1 音量LFOのマスク
                mdc = 0;// b? ; 1 M depth Counter(変動値)
                mdc2 = 0;// b? ; 1 M depth Counter
                _mdc = 0;// b? ; 1 M depth Counter(変動値)
                _mdc2 = 0;// b? ; 1 M depth Counter
                onkai = 0;//b 1 演奏中の音階データ(0ffh:rest)
                sdelay = 0;//b?; 1 Slot delay
                sdelay_c = 0;//b? ; 1 Slot delay counter
                sdelay_m = 0;//b? ; 1 Slot delay Mask
                alg_fb = 0;//b? ; 1 音色のalg/fb
                keyon_flag = 0;// b 1 新音階/休符データを処理したらinc
                qdat2 = 0;// b? ; 1 q 最低保証値
                fnum2 = 0;// w? ; 2 ppz8/pmd86用fnum値上位
                onkai_def = 0;// b 1 演奏中の音階データ(転調処理前 / ?fh:rest)
                shift_def = 0;// b? ; 1 マスター転調値
                qdat3 = 0;// b? ; 1 q Random
            }
        }

        //qqq struc
        //     db  offset eenv_ar dup(?)
        //pat  db	?	; 1 旧SSGENV	/Normal pat
        //pv2  db	?	; 1		/Normal pv2
        //pr1  db	?	; 1		/Normal pr1
        //pr2  db	?	; 1		/Normal pr2
        //     db?
        //     db	?
        //patb db	?	; 1		/Normal patb
        //     db?
        //pr1b db?	    ; 1		/Normal pr1b
        //pr2b db	?	; 1		/Normal pr2b
        //penv db	?	; 1		/Normal penv
        //qqq ends

        public int max_part1;//０クリアすべきパート数
        public int max_part2;//初期化すべきパート数

        //fm     equ 0
        //fm2    equ 1
        //psg    equ 2
        //rhythm equ 3

        //    dw  open_work

        public int[] part_data_table;

        //FM1-3
        public int part1;
        public int part2;
        public int part3;

        //FM4-6
        public int part4;
        public int part5;
        public int part6;

        //効果音モード
        public int part3b;
        public int part3c;
        public int part3d;

        //pps?
        public int part7;
        public int part8;
        public int part9;
        public int part10;
        public int part11;

        //ppz 
        public int part10a;
        public int part10b;
        public int part10c;
        public int part10d;
        public int part10e;
        public int part10f;
        public int part10g;
        public int part10h;

        //効果音
        public int part_e;

        public partWork[] partWk;
        //ノーマル
        // 1,2,3, 3b,3c,3d, 7,8,9,10,11, e
        //board2
        // 1,2,3,4,5,6, 7,8,9,10,11, 3b,3c,3d, e
        //ppz(ppzはboard2を兼ねる)
        // 1,2,3,4,5,6, 7,8,9,10,11, 3b,3c,3d, 10a,10b,10c,10d,10e,10f,10g,10h ,e


        //    even
        //pcm_table   label word
        //if	board2
        // if	adpcm
        //  ife   ademu
        //pcmends     dw	26H	;最初のstartは26Hから
        //pcmadrs     dw	2*256 dup(0)
        //pcmfilename db	128 dup(0)
        //  endif
        // endif
        // if	pcm
        //pcmst_ofs   dw	0
        //pcmst_seg dw	0
        //pcmadrs db	6*256 dup(0)
        // endif
        //endif

        //      db	"ここはＳＴＡＣＫエリアです。いつ"
        //		db	"もＰＭＤをご愛用して下さっている"
        //		db	"方々、どうもありがとうございます"
        //		db	"(^^)。何かバグらしき物が見つかり"
        //		db	"ましたら、些細な事でも構いません"
        //		db	"ので、是非私までご一報、お願いし"
        //		db	"ますね(^^)。→PMDBBS [xx(xxxx)xx"
        //		db	"xx] @PMD ボードまで     by KAJA."

        //_stack:
        //dataarea label   word
        //   db	0		;
        //   dw	12 dup(18h); 初期データ
        //   db	80h		;

        public byte[] fmoff_nef = new byte[] { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 0xff };
        public byte[] fmoff_ef = new byte[] { 0, 1, 4, 5, 8, 9, 12, 13, 0xff };



        //10538-
        public string mes_title = "Ｍｕｓｉｃ　Ｄｒｉｖｅｒ　Ｐ.Ｍ.Ｄ. for PC9801/88VA Version " + ver
        + "\r\n"
        + "Copyright (C)1989," + date + " by M.Kajihara(KAJA).\r\n\r\n";

        public string mes_ppsdrv = "PPSDRV(INT64H)に対応します．\r\n";
        public string mes_ppz8 = "PPZ8(INT7FH)に対応します．\r\n";

        public byte port_sel;// b? ; 選択ポート
        public byte opn_0eh;// b?
        public byte message_flag;    // b?
        public ushort opt_sp_push; // w?
        public ushort resident_size;  // w?


        //EFCDRV.ASM
        public ushort effadr;// w effect address
        public ushort eswthz;// w トーンスゥイープ周波数
        public ushort eswtst;// w トーンスゥイープ増分
        public byte effcnt;// b effect count
        public byte eswnhz;// b ノイズスゥイープ周波数
        public byte eswnst;// b ノイズスゥイープ増分
        public byte eswnct;// b ノイズスゥイープカウント
        public byte effon;// b 効果音 発音中
        public byte psgefcnum;// b 効果音番号
        public byte hosei_flag;// b ppsdrv 音量/音程補正をするかどうか
        public byte last_shot_data;// b 最後に発音させたPPSDRV音色


        //PCMDRV86.ASM
        //;==============================================================================
        //;	Datas
        //;==============================================================================
        //trans_size equ	256	;1回の転送byte数
        //play86_flag db	0	;発音中? flag
        //trans_flag db	0	; 転送するdataが残っているか? flag
        //start_ofs dw	0	; 発音中PCMデータ番地(offset下位)
        //start_ofs2 dw	0	; 発音中PCMデータ番地(offset上位)
        //size1 dw	0	; 残りサイズ(下位word)
        //size2 dw	0	; 残りサイズ(上位word)
        //_start_ofs dw	0	; 発音開始PCMデータ番地(offset下位)
        //_start_ofs2 dw	0	; 発音開始PCMデータ番地(offset上位)
        //_size1 dw	0	; PCMデータサイズ(下位word)
        //_size2 dw	0	; PCMデータサイズ(上位word)
        //addsize1 db	0	; PCMアドレス加算値(整数部)
        //addsize2 dw	0	; PCMアドレス加算値(小数点部)
        //addsizew dw	0	; PCMアドレス加算値(小数点部, 転送中work)
        //repeat_ofs dw	0	; リピート開始位置(offset下位)
        //repeat_ofs2 dw	0	; リピート開始位置(offset上位)
        //repeat_size1 dw	0	; リピート後のサイズ(下位word)
        //repeat_size2 dw	0	; リピート後のサイズ(上位word)
        //release_ofs dw	0	; リリース開始位置(offset下位)
        //release_ofs2 dw	0	; リリース開始位置(offset上位)
        //release_size1 dw	0	; リリース後のサイズ(下位word)
        //release_size2 dw	0	; リリース後のサイズ(上位word)
        //repeat_flag db	0	; リピートするかどうかのflag
        // release_flag1   db	0	;リリースするかどうかのflag
        // release_flag2   db	0	;リリースしたかどうかのflag
        public byte pcm86_pan_flag = 0;// b 0 ;パンデータ１(bit0= 左 / bit1 = 右 / bit2 = 逆)
        public byte com_end = 0xb1;

        //pcm86_pan_dat db	0	; パンデータ２(音量を下げるサイドの音量値)

        //; pan_flagによる転送table
        //trans_table dw double_trans, left_trans

        //        dw right_trans, double_trans

        //        dw double_trans_g, left_trans_g

        //        dw right_trans_g, double_trans_g

        //; 周波数table Include

        //    include tunedata.inc



        //;==============================================================================
        //;	ｵﾝｶｲ DATA
        //;==============================================================================
        public ushort[] fnum_data = new ushort[] {
              0x026a//; C
            , 0x028f//; D-
            , 0x02b6//; D
            , 0x02df//; E-
            , 0x030b//; E
            , 0x0339//; F
            , 0x036a//; G-
            , 0x039e//; G
            , 0x03d5//; A-
            , 0x0410//; A
            , 0x044e//; B-
            , 0x048f//; B
        };

        public ushort[] psg_tune_data = new ushort[]{
              0x0ee8//; C
            , 0x0e12//; D-
            , 0x0d48//; D
            , 0x0c89//; E-
            , 0x0bd5//; E
            , 0x0b2b//; F
            , 0x0a8a//; G-
            , 0x09f3//; G
            , 0x0964//; A-
            , 0x08dd//; A
            , 0x085e//; B-
            , 0x07e6//; B
        };



        //7177-7242
        public byte[] part_table = null;
        //if	board2
        // if	ppz
        //;			Part番号,Partb,音源番号
        private byte[] part_table_ppz = new byte[]
        {
         00,1,0 //; A
        ,01,2,0	//; B
        ,02,3,0	//; C
        ,03,1,1	//; D
        ,04,2,1	//; E
        ,05,3,1	//; F
        ,06,1,2	//; G
        ,07,2,2	//; H
        ,08,3,2	//; I
        ,09,1,3	//; J
        ,10,3,4	//; K
        ,11,3,0	//; c2
        ,12,3,0	//; c3
        ,13,3,0	//; c4
        ,0xff,0,0xff//;Rhythm
        ,22,3,1	//; Effect
        ,14,0,5	//; PPZ1
        ,15,1,5	//; PPZ2
        ,16,2,5	//; PPZ3
        ,17,3,5	//; PPZ4
        ,18,4,5	//; PPZ5
        ,19,5,5	//; PPZ6
        ,20,6,5	//; PPZ7
        ,21,7,5 //; PPZ8
        };
        // else
        //;			Part番号,Partb,音源番号
        private byte[] part_table_brd2 = new byte[]
        {
         00,1,0	//;A
		,01,2,0	//;B
		,02,3,0	//;C
		,03,1,1	//;D
		,04,2,1	//;E
		,05,3,1	//;F
		,06,1,2	//;G
		,07,2,2	//;H
		,08,3,2	//;I
		,09,1,3	//;J
		,10,3,4	//;K
		,11,3,0	//;c2
		,12,3,0	//;c3
		,13,3,0	//;c4
		,0xff,0,0xff	//;Rhythm
		,14,3,1 //;Effect
        };
        //else
        //; Part番号,Partb,音源番号
        private byte[] part_table_nbrd2 = new byte[]
        {
         00,1,0	//;A
		,01,2,0	//;B
		,02,3,0	//;C
		,03,3,0	//;c2
		,04,3,0	//;c3
		,05,3,0	//;c4
		,06,1,2	//;G
		,07,2,2	//;H
		,08,3,2	//;I
		,09,1,3	//;J
		,10,3,4	//;K
		,03,3,0	//;c2
		,04,3,0	//;c3
		,05,3,0	//;c4
		,0xff,0,0xff //;Rhythm
		,11,3,0 //;Effect
        };



        //PMD.ASM 7955-7964
        //;==============================================================================
        //;	ＦＭ音色のキャリアのテーブル
        //;==============================================================================
        public byte[] carrier_table = new byte[] {
             0b1000_0000,0b1000_0000,0b1000_0000,0b1000_0000
            ,0b1010_0000,0b1110_0000,0b1110_0000,0b1111_0000
            ,0b1110_1110,0b1110_1110,0b1110_1110,0b1110_1110
            ,0b1100_1100,0b1000_1000,0b1000_1000,0b0000_0000
        };



        //
        //;==============================================================================
        //;	効果音データ ＩＮＣＬＵＤＥ
        //;==============================================================================
        //public byte[] efftbl;//label   word
        //include effect.inc
        public byte efftblend;//   label word



        public PW(bool isSB2, bool usePPZ)
        {
            board = 1;
            board2 = isSB2 ? 1 : 0;
            ppz = usePPZ ? 1 : 0;

            fmvd_init = (va + board2 != 0) ? 0 : 16;

            if (va != 0)
            {
                ms_cmd = 0x188;// ８２５９マスタポート
                ms_msk = 0x18a;// ８２５９マスタ／マスク
                sl_cmd = 0x184;// ８２５９スレーブポート
                sl_msk = 0x186;// ８２５９スレーブ／マスク
            }

            if (board2 == 0)
            {
                //ノーマル
                partWk = new partWork[3 + 3 + 5 + 1];
                part3b = 3; partWk[part3b] = new partWork();
                part3c = 4; partWk[part3c] = new partWork();
                part3d = 5; partWk[part3d] = new partWork();
                part7 = 6; partWk[part7] = new partWork();
                part8 = 7; partWk[part8] = new partWork();
                part9 = 8; partWk[part9] = new partWork();
                part10 = 9; partWk[part10] = new partWork();
                part11 = 10; partWk[part11] = new partWork();
                part_e = 11; partWk[part_e] = new partWork();

                max_part1 = 11;//０クリアすべきパート数
                max_part2 = 11;//初期化すべきパート数
            }
            else
            {
                //board2
                if (ppz == 0)
                {
                    partWk = new partWork[3 + 3 + 5 + 3 + 1];
                    part_e = 14; partWk[part_e] = new partWork();

                    max_part1 = 14;//０クリアすべきパート数
                    max_part2 = 11;//初期化すべきパート数
                }
                else
                {
                    partWk = new partWork[3 + 3 + 5 + 3 + 8 + 1];
                    part10a = 14; partWk[part10a] = new partWork();
                    part10b = 15; partWk[part10b] = new partWork();
                    part10c = 16; partWk[part10c] = new partWork();
                    part10d = 17; partWk[part10d] = new partWork();
                    part10e = 18; partWk[part10e] = new partWork();
                    part10f = 19; partWk[part10f] = new partWork();
                    part10g = 20; partWk[part10g] = new partWork();
                    part10h = 21; partWk[part10h] = new partWork();
                    part_e = 22; partWk[part_e] = new partWork();

                    max_part1 = 14 + 8;//０クリアすべきパート数
                    max_part2 = 11;//初期化すべきパート数
                }
                part4 = 3; partWk[part4] = new partWork();
                part5 = 4; partWk[part5] = new partWork();
                part6 = 5; partWk[part6] = new partWork();
                part7 = 6; partWk[part7] = new partWork();
                part8 = 7; partWk[part8] = new partWork();
                part9 = 8; partWk[part9] = new partWork();
                part10 = 9; partWk[part10] = new partWork();
                part11 = 10; partWk[part11] = new partWork();
                part3b = 11; partWk[part3b] = new partWork();
                part3c = 12; partWk[part3c] = new partWork();
                part3d = 13; partWk[part3d] = new partWork();
            }
            part1 = 0; partWk[part1] = new partWork();
            part2 = 1; partWk[part2] = new partWork();
            part3 = 2; partWk[part3] = new partWork();

            part_table = part_table_nbrd2;
            if (board2 != 0)
            {
                part_table = part_table_brd2;
                if (ppz != 0)
                {
                    part_table = part_table_ppz;
                }
            }


            efftbl = new List<Tuple<byte, MmlDatum[]>>();
            MmlDatum[] ef;
            ef = MakeMmlDatum(D_000); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;BDRM		    ;0
            ef = MakeMmlDatum(D_001); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;SIMONDS    	;1
            ef = MakeMmlDatum(D_002); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;SIMONDSTAML	;2
            ef = MakeMmlDatum(D_003); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;SIMONDSTAMM	;3
            ef = MakeMmlDatum(D_004); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;SIMONDSTAMH	;4
            ef = MakeMmlDatum(D_005); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;RIMSHOTT	    ;5
            ef = MakeMmlDatum(D_006); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;CPSIMONDSSD2	;6
            ef = MakeMmlDatum(D_007); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;CLOSEHT	    ;7
            ef = MakeMmlDatum(D_008); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;OPENHT		    ;8
            ef = MakeMmlDatum(D_009); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;CRUSHCYMBA	    ;9
            ef = MakeMmlDatum(D_010); efftbl.Add(new Tuple<byte, MmlDatum[]>(1, ef)); //;RDCYN		    ;10
        }

        private MmlDatum[] MakeMmlDatum(byte[] dd)
        {
            List<MmlDatum> ret = new List<MmlDatum>();
            foreach (byte b in dd) ret.Add(new MmlDatum(b));
            return ret.ToArray();
        }



        //EFFECT.INC
        public List<Tuple<byte, MmlDatum[]>> efftbl;

        private static byte[] D_000 = new byte[]{//; Bass Drum               	1990-06-22	05:47:11
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
                1,  220,    5,   31,  54,   15,   0,   0,     0,   127,     0
            ,   8,  164,    6,    0,  62,   16, 176,   4,     0,   127,     0
            ,0xff//-1
        };
        private static byte[] D_001 = new byte[]{//; Snare Drum              	1990-06-22	05:48:06
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
	           14,  144,    1,    7,  54,   16, 184,  11,     0,    93,   242
            ,0xff//-1
        };
        private static byte[] D_002 = new byte[]{//; Low Tom                 	1990-06-22	05:49:19
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
	            2,  188,    2,    0,  54,   15,   0,   0,     0,   100,     0
            ,  14,  132,    3,    0,  54,   16, 196,   9,     0,   100,     0
            ,0xff//-1
        };
        private static byte[] D_003 = new byte[]{//; Middle Tom              	1990-06-22	05:50:23
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
   	            2,  244,    1,    5,  54,   15,   0,   0,     0,    60,     0
            ,  14,  108,    2,    0,  54,   16, 196,   9,     0,    60,     0
            ,0xff//-1
        };
        private static byte[] D_004 = new byte[]{//; High Tom                	1990-06-22	05:51:13
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
    	        2,   44,    1,    0,  54,   15,   0,   0,     0,    50,     0
            ,  14,  144,    1,    0,  54,   16, 196,   9,     0,    50,     0
            ,0xff//-1
        };
        private static byte[] D_005 = new byte[]{//; Rim Shot                	1990-06-22	05:51:57
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
 	            2,   55,    0,    0,  62,   16,  44,   1,     0,   100,     0
            ,0xff//-1
        };
        private static byte[] D_006 = new byte[]{//; Snare Drum 2            	1990-06-22	05:52:36
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
 	           16,    0,    0,   15,  55,   16, 184,  11,     0,     0,   241
            ,0xff//-1
        };
        private static byte[] D_007 = new byte[]{//; Hi-Hat Close            	1990-06-22	05:53:10
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
	            6,   39,    0,    0,  54,   16, 244,   1,     0,     0,     0
            ,0xff//-1
        };
        private static byte[] D_008 = new byte[]{//; Hi-Hat Open             	1990-06-22	05:53:40
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
	           32,   39,    0,    0,  54,   16, 136,  19,     0,     0,     0
            ,0xff//-1
        };
        private static byte[] D_009 = new byte[]{//; Crush Cymbal            	1990-06-22	05:54:11
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
	           31,   40,    0,   31,  54,   16, 136,  19,     0,     0,   241
            ,0xff//-1
        };
        private static byte[] D_010 = new byte[]{//; Ride Cymbal             	1990-06-22	05:54:38
            //len freqL freqH noise  mix  Evol envL envH envPtn sweepT sweepN
	           31,   30,    0,    0,  54,   16, 136,  19,     0,     0,     0
            , 0xff//-1
        };
    }
}
