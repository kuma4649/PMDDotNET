using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PW
    {
        public const string ver = "4.8q";
        public int vers = 0x48;
        public string verc = "q";
        public string date = "Aug.5th 1998";

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

        public int pmdvector = 0x60;//PMD用の割り込みベクトル
        public int ppsdrv = 0x64;//ppsdrvの割り込みベクトル
        public int ppz_vec = 0x7f;//ppz8の割り込みベクトル



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
        public int slot_detune1 = 0;//FM3 Slot Detune値 slot1
        public int slot_detune2 = 0;// FM3 Slot Detune値 slot2
        public int slot_detune3 = 0;// FM3 Slot Detune値 slot3
        public int slot_detune4 = 0;// FM3 Slot Detune値 slot4
        public int wait_clock = 0;// FM ADDRESS-DATA間 Loop $の回数
        public int wait1_clock = 0;//loop $ １個の速度
        public byte ff_tempo = 0;//早送り時のTimerB値
        public byte pcm_access = 0;//PCMセット中は 1
        public byte TimerB_speed = 0;// TimerBの現在値(=ff_tempoならff中)
        public byte fadeout_flag = 0;// 内部からfoutを呼び出した時1
        public byte adpcm_wait = 0;//ADPCM定義の速度
        public byte revpan = 0;//PCM86逆走flag
        public byte pcm86_vol = 0;//PCM86の音量をSPBに合わせるか?
        public int syousetu = 0;//小節カウンタ
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
        public byte rshot_bd = 0;// リズム音源 shot inc flag(BD)
        public byte rshot_sd = 0;// リズム音源 shot inc flag(SD)
        public byte rshot_sym = 0;// リズム音源 shot inc flag(CYM)
        public byte rshot_hh = 0;// リズム音源 shot inc flag(HH)
        public byte rshot_tom = 0;// リズム音源 shot inc flag(TOM)
        public byte rshot_rim = 0;// リズム音源 shot inc flag(RIM)
        public byte rdump_bd = 0;// リズム音源 dump inc flag(BD)
        public byte rdump_sd = 0;// リズム音源 dump inc flag(SD)
        public byte rdump_sym = 0;// リズム音源 dump inc flag(CYM)
        public byte rdump_hh = 0;// リズム音源 dump inc flag(HH)
        public byte rdump_tom = 0;// リズム音源 dump inc flag(TOM)
        public byte rdump_rim = 0;// リズム音源 dump inc flag(RIM)
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



    }
}
