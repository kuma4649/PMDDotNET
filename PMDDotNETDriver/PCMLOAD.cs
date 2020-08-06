using musicDriverInterface;
using PMDDotNET.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PCMLOAD
	{
        private x86Register r = null;
        private PW pw = null;
        private PMD pmd = null;
        private Pc98 pc98 = null;
        private PPZ8em ppz8em = null;
        private Func<string, Stream> appendFileReaderCallback = null;

        public PCMLOAD(PMD pmd, PW pw, x86Register r,Pc98 pc98, PPZ8em ppz8em, Func<string, Stream> appendFileReaderCallback)
        {
            this.pmd = pmd;
            this.pw = pw;
            this.r = r;
            this.pc98 = pc98;
            this.ppz8em = ppz8em;
            this.appendFileReaderCallback = appendFileReaderCallback;
        }

        private byte[] GetPCMDataFromFile(string fnPcm)
        {
            try
            {
                using (Stream pd = appendFileReaderCallback?.Invoke(fnPcm))
                {
                    return ReadAllBytes(pd);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
		/// ストリームから一括でバイナリを読み込む
		/// </summary>
		private byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null) return null;

            var buf = new byte[8192];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var r = stream.Read(buf, 0, buf.Length);
                    if (r < 1)
                    {
                        break;
                    }
                    ms.Write(buf, 0, r);
                }
                return ms.ToArray();
            }
        }



        //20-202
        //;==============================================================================
        //;
        //;	PPZ(PVI/PZI)ファイルの読み込み
        //;
        //;		input DS:AX filename(128byte)
        //; CL 読ませるバンク(1=１つ目 2=２つ目 3=両方)
        //; output cy = 1    Not Loaded
        //; AX=1	ファイルの読み込み失敗
        //;				AX=2	データ形式が違う
        //;				AX=3	メモリが確保できない
        //;				AX=4	EMSハンドルのマッピングができない
        //;				AX=5	PPZ8が常駐していない
        //;				CL エラーの出たPCM番号(0 or 1)
        //;		break	ax,cx
        //;==============================================================================
        public void ppz_load(string ppz1File, string ppz2File)
        {
            r.stack.Push(r.bx);
            r.stack.Push(r.dx);
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            r.stack.Push(r.bp);
            //r.stack.Push(r.ds);
            //r.stack.Push(r.es);

            pw.ppz_bank = r.cl;
            pw.filename_ofs = ppz1File;
            pw.filename_seg = 0;
            ppz_load_main(ppz1File, ppz2File);

            //r.es = r.stack.Pop();
            //r.ds = r.stack.Pop();
            r.bp = r.stack.Pop();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            r.dx = r.stack.Pop();
            r.bx = r.stack.Pop();
        }

        //;==============================================================================
        //;	PPZ8 読み込み main
        //;==============================================================================
        private void ppz_load_main(string ppz1File, string ppz2File)
        {
            ppz8_check();
            pw.ppz_bank = (byte)((string.IsNullOrEmpty(ppz1File) ? 0 : 1) | (string.IsNullOrEmpty(ppz2File) ? 0 : 2));
            r.cl = 0;
            r.ax = 4;
            if (r.carry)
            {
                ppz_load_error();
                return;
            }
            //; PCM２つ読み用追加判別処理
            read_ppz8();
            if (r.carry) goto plm_exit;
            if (string.IsNullOrEmpty(ppz2File))
                goto plm_exit2;// ; PCMは１つだけ
            pw.filename_ofs = ppz2File;
            r.cl = 1;
            read_ppz8();
        plm_exit:;
            return;
        plm_exit2:;
            r.ax = 0;
            return;
        }

        private void read_ppz8()
        {
            //; 拡張子判別(PVI / PZI)
            string ext = Path.GetExtension(pw.filename_ofs).ToUpper().Trim();
            if (ext == ".PZI") r.ch = 1;
            else if (ext == ".PVI") r.ch = 0;

            r.carry = (pw.ppz_bank & 1) != 0;
            pw.ppz_bank >>= 1;
            if (!r.carry) goto p8_load_skip;//; load skip

            //; PVI / PZI 読み込み
            p8_load_main:;
            byte[] pcmData = GetPCMDataFromFile(pw.filename_ofs);
            int ret = ppz8em.LoadPcm(r.cl,r.ch, pcmData);

            if (ret==0) goto p8_load_exit;//KUMA:読み込めた
            if (ret!=2) goto p8_load_exit;//; file not found or 形式が違うなら

            r.ch ^= 1;//; もう片方の形式も
            ppz8em.LoadPcm(r.cl,r.ch, pcmData);//pcm loadを試してみる

        p8_load_exit:;
            r.carry = false;
            if (ret!=0)
            {
                r.ax = (ushort)ret;
                ppz_load_error();
                r.carry = true;
            }
        p8_load_skip:;
            r.ax = 0;
        }

        //;	Error処理
        private void ppz_load_error()
        {
            r.ax++;
            if (pw.message != 0)
            {
                r.bx = r.ax;
                r.dx = 0;//offset exit1z_mes
                r.bx--;
                if (r.bx == 0)
                {
                    ppz_error_main(pw.exit1z_mes);
                    return;
                }
                r.dx = 0;//offset exit2z_mes
                r.bx--;
                if (r.bx == 0)
                {
                    ppz_error_main(pw.exit2z_mes);
                    return;
                }
                r.dx = 0;//offset exit3z_mes
                r.bx--;
                if (r.bx == 0)
                {
                    ppz_error_main(pw.exit3z_mes);
                    return;
                }
                r.dx = 0;//offset exit4z_mes
                r.bx--;
                if (r.bx == 0)
                {
                    ppz_error_main(pw.exit4z_mes);
                    return;
                }
                r.dx = 0;//offset exit5z_mes
                ppz_error_main2(pw.exit5z_mes);
            }

            r.carry = true;
        }
        private void ppz_error_main(string msg)
        {
            r.al = r.cl;
            r.al += (byte)'1';
            //pw.banknum = r.al.ToString();//;Bank
            r.stack.Push(r.dx);
            r.dx = 0;//offset ppzbank_mes
            //ppz_error_main2(string.Format(pw.ppzbank_mes, r.al));
            r.dx = r.stack.Pop();
            ppz_error_main2(string.Format(pw.ppzbank_mes, r.al) + msg);
        }

        private void ppz_error_main2(string msg)
        {
            Log.writeLine(LogLevel.ERROR, msg);
        }

        //; PPZ8常駐check
        private void ppz8_check()
        {
            r.carry = pw.ppz == 0;
        }



        //203-288
        //;==============================================================================
        //;
        //;	PCM(PPC/P86)ファイルの読み込み
        //;		P86DRV.COMが常駐していれば.P86を、
        //;		そうでない場合は.PPCを読む。
        //;		PMDPPZEが常駐している場合は無条件にPVIをPPZ8に読み込む。
        //;
        //;		input DS:AX filename(128byte)
        //; ES:DI pcm_work(32KB, P86の場合は必要無し)
        //; output cy = 1    Not Loaded
        //; PMDB2/PMD86の場合
        //;				AX=1	SPB/ADPCM-RAMかPMDB2がない
        //;					86B/P86DRV かPMD86がない
        //; AX=2	ファイルがない
        //;				AX=3	ファイルがPMDのPCMデータではない
        //;				AX=4	SPB/既に読み込んであるのと同じだった
        //;					86B/容量OVER
        //;				AX=5	ファイルが読めない
        //;				AX=6	PCMメモリがアクセス中だった
        //;			PMDPPZEの場合
        //;				AX=1	ファイルの読み込み失敗
        //;				AX=2	データ形式が違う
        //;				AX=3	メモリが確保できない
        //;				AX=4	EMSハンドルのマッピングができない
        //;				AX=5	PPZ8が常駐していない
        //;
        //;	.PPC format:
        //; WORK=PMD内PCMWORK , DATA=PCMRAM先頭のWORK , FILE=PCMFILE
        //;					 123456789012345678901234567890
        //;		DATA/FILEのみ		"ADPCM DATA for  PMD ver.4.4-  "30bytes
        //;		WORK/DATA/FILE		1Word Next START Address
        //;					2Word*256	START/STOP
        //;		WORK/DATAのみ		128bytes FILENAME
        //; DATAのみ		32bytes 予備
        //;
        //;		PCMRAM_Work		=00000H～00025H
        //;		PCMRAM_Main_data	=00026H～01FFFH
        //;
        //;	.P86 format:
        //;		"PCM86 DATA",0ah,0	12 byte
        //;		P86DRVのversion		1  byte
        //;		全体のサイズ		3  byte
        //;		音色table start(3),size(3) * 256 (1536) bytes
        //;		音色データ 可変
        //;
        //;==============================================================================
        public void pcm_all_load(string ppcFile)
        {
            //cld
            //r.stack.Push(r.ds);
            //r.stack.Push(r.es);
            r.stack.Push(r.bx);
            r.stack.Push(r.cx);
            r.stack.Push(r.dx);
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            r.stack.Push(r.bp);

            pw.filename_ofs = ppcFile;// r.ax;
            pw.filename_seg = 0;// r.ds;
            pw.pcmdata_ofs = r.di;
            pw.pcmdata_seg = 0;// r.es;
            r.ah = 0xe;//;GET_PCM_ADR
            pmd.int60_main(r.ax);//	int	60h		;DS:DX=PCMワーク
            pw.pcmwork_ofs = r.dx;
            pw.pcmwork_seg = 0;// r.ds;

            all_load();

            r.bp = r.stack.Pop();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            r.dx = r.stack.Pop();
            r.cx = r.stack.Pop();
            r.bx = r.stack.Pop();
            //r.es = r.stack.Pop();
            //r.ds = r.stack.Pop();
        }



        //389-583
        //;==============================================================================
        //;	.PPC/.P86 一括load
        //;		in	cs:[filename_ofs/seg] Filename
        //;			cs:[pcmdata_ofs/seg]
        //		PCMData loadarea
        //; cs:[pcmwork_ofs/seg] PMD内PCMwork
        //;==============================================================================
        private void all_load()
        {
            //;-----------------------------------------------------------------------------
            //;	読み込むのは.P86か.PPCかどうかを判別
            //;-----------------------------------------------------------------------------
            check_p86drv();
            if (!r.carry)
            {
                //goto p86_load;
                throw new NotImplementedException();
            }
            r.ah = 9;
            pmd.int60_main(r.ax);//;board check
            if (r.al == 1)//; pmdb2
                goto allload_main1;
            if (r.al == 4)//;pmdppz
                goto allload_main1;
            if (r.al == 5)//;pmdppze
            {
                //goto allload_ppze;
                throw new NotImplementedException();
            }
            allload_exit1();
            return;

        //;-----------------------------------------------------------------------------
        //;	.PPC read Main
        //;-----------------------------------------------------------------------------
        allload_main1:;
            check_pmdb2();
            if (r.carry)
            {
                allload_exit1();
                return;
            }

            filename_set();

            //;-----------------------------------------------------------------------------
            //;	FileをPMDのワークにヘッダだけ読みこむ //KUMA:全部読み込む！
            //;-----------------------------------------------------------------------------
            string fn = Path.ChangeExtension(pw.filename_ofs, ".PPC");//;拡張子 "PPC"に変更
            byte[] pcmData = GetPCMDataFromFile(fn);
            if (pcmData == null || pcmData.Length < 1)
            {
                fn = Path.ChangeExtension(pw.filename_ofs, ".P86");//;拡張子 "P86"に変更
                pcmData = GetPCMDataFromFile(fn);
                if (pcmData == null || pcmData.Length < 1)
                {
                    allload_exit2();
                    return;
                }
            }

        exec_ppcload:;
            if (pcmData.Length < 30)
            {
                allload_exit3_close();
                return;
            }

            if (pcmData[0] == 'P' && pcmData[1] == 'V' && pcmData[2] == 'I' && pcmData[3] == '2')
            {
                if (pcmData[10] == 2)//;RAM Type 8bit
                {
                    //goto pvi_load;
                    throw new NotImplementedException();
                }
            }
        not_pvi:;
            if (!(pcmData[0] == 'A' && pcmData[1] == 'D'
                && pcmData[2] == 'P' && pcmData[3] == 'C'
                && pcmData[4] == 'M' && pcmData[5] == ' '
                ))
            {
                allload_exit3_close();//;PMDのPCMデータではない
                return;
            }

            if (pcmData.Length < 4 * 256 + 2 + 30)//KUMA:0x420未満
            {
                allload_exit3_close();
                return;
            }

        //;-----------------------------------------------------------------------------
        //;	PMDのワークにFilenameを書く
        //;-----------------------------------------------------------------------------
        ppc_load_main:;
            write_filename_to_pmdwork();

            //;-----------------------------------------------------------------------------
            //;	PCMRAMのヘッダを読む
            //;-----------------------------------------------------------------------------
            if (pw.retry_flag != 0)
                goto write_pcm_main;//; 無条件

            //TBD

            //;-----------------------------------------------------------------------------
            //;	PMDのワークとPCMRAMのヘッダを比較
            //;-----------------------------------------------------------------------------
            //TBD

            //;-----------------------------------------------------------------------------
            //;	PMDのワークをPCMRAM頭に書き込む
            //;-----------------------------------------------------------------------------
            write_pcm_main:;
            //r.ds = r.cs;
            r.si = 0;//offset adpcm_header
            r.di = 0;//pcmdata_ofs
            r.cx = 30 / 2;//;"ADPCM～"ヘッダを書き込み

            pw.pcmDt = new byte[4 * 256 + 128 + 2 + 30];
            for (int i = 0; i < (4 * 256 + 128 + 2 + 30); i++)
            {
                pw.pcmDt[i] = pcmData[i];
            }
            for (int i = 0; i < (4 * 256 + 128); i++)
            {
                pw.pcmWk[i] = pcmData[i + 32];
            }

            pw.pcmload_pcmstart = 0;
            pw.pcmload_pcmstop = 0x25;
            pcmstore();

            //;-----------------------------------------------------------------------------
            //;	PCMDATAをPCMRAMに書き込む
            //;	8000hずつ読み込みながら定義
            //;-----------------------------------------------------------------------------
            if (pw.message != 0)
            {
                //r.ds = r.cs;
                Log.writeLine(LogLevel.INFO, pw.allload_mes);//;"ＰＣＭ定義中"の表示
            }

            r.bx = 30;// pw.pcmwork_ofs;//cs:[pcmwork_ofs]
            r.ax = (ushort)(pcmData[r.bx] + pcmData[r.bx + 1] * 0x100);//ds:[bx]	;AX=PCM Next Start Address
            r.ax -= 0x26;//;実際にこれから転送するデータ量に変換

            pw.pcmload_pcmstart = 0x26;
            pw.pcmload_pcmstop = 0x426;//;400h*32=8000h 一括

            int pcmdata_ofs = 4 * 256 + 2 + 30;
        allload_loop:;
            if (r.ax < 0x401)
                goto allload_last;
            r.ax -= 0x400;
            r.bp = r.ax;//;Push
            r.cx = 0x8000;

            pw.pcmDt = new byte[r.cx];
            for (int i = 0; i < r.cx; i++)
            {
                pw.pcmDt[i] = pcmData[pcmdata_ofs++];
            }
            //	jc allload_exit5_close
            //	jnz allload_exit3_close

            pcmstore();//;PCM Store

            pw.pcmload_pcmstart += 0x400;
            pw.pcmload_pcmstop += 0x400;
            r.ax = r.bp;//;Pop
            goto allload_loop;
        allload_last:;
            if (r.ax == 0)
                goto allload_justend;
            r.bp = r.ax;//; Push
            r.ax += pw.pcmload_pcmstart;
            pw.pcmload_pcmstop = r.ax;
            r.dx = pw.pcmdata_ofs;//cs:[pcmdata_ofs]
            r.cx = 0x8000;
            pw.pcmDt = new byte[r.cx];
            for (int i = 0; i < r.cx; i++)
            {
                if (pcmdata_ofs < pcmData.Length)
                    pw.pcmDt[i] = pcmData[pcmdata_ofs++];
                else
                    pw.pcmDt[i] = 0;
            }
            //	jc allload_exit5_close
            r.bx = r.bp;//; Pop
            r.bx += r.bx;
            r.bx += r.bx;
            r.bx += r.bx;
            r.bx += r.bx;
            r.bx += r.bx;
            r.carry = (r.ax < r.bx);
            //pushf
            pcmstore();//; PCM Store
        //	popf
        //	jc  allload_exit3_close
        allload_justend:;
            //; FILE Close

            //;-----------------------------------------------------------------------------
            //;	終了
            //;-----------------------------------------------------------------------------
            r.ax = 0;
        }



        //739-748
        //;-----------------------------------------------------------------------------
        //;	エラーリターン
        //;-----------------------------------------------------------------------------
        private void allload_exit1()
        {
            if (pw.message != 0)
            {
                r.dx = 0;//    mov dx,offset exit1_mes
            }
            r.ax = 1;//;PCMが定義出来ません。
            error_exec(pw.exit1_mes);
        }

        private void allload_exit2()
        {
            if (pw.message != 0)
            {
                r.dx = 0;//    mov dx,offset exit2_mes
            }
            r.ax = 2;//;PCMファイルがない
            error_exec(pw.exit2_mes);
        }

        private void allload_exit3_close()
        {
            allload_exit3();
        }

        private void allload_exit3()
        {
            if (pw.message != 0)
            {
                r.dx = 0;//    mov dx,offset exit3_mes
            }
            r.ax = 3;//;ファイルがPMDのPCMではない
            error_exec(pw.exit3_mes);
        }

        private void allload_exit6_close()
        {
            allload_exit6();
        }

        private void allload_exit6()
        {
            if (pw.message != 0)
            {
                r.dx = 0;//    mov dx,offset exit6_mes
            }
            r.ax = 6;//;PCMメモリアクセス中
            error_exec(pw.exit6_mes);
        }



        //828-839
        private void error_exec(string msg)
        {
            if (pw.message != 0)
            {
                r.stack.Push(r.ax);
                r.ax = 0;//r.cs;
                         //r.ds = r.ax;
                r.ah = 0x09;
                //int 21h
                Log.writeLine(LogLevel.ERROR, msg);
                r.ax = r.stack.Pop();
            }

            r.carry = true;
        }



        //840-864
        //;==============================================================================
        //;	PMDB2＆ADPCMのCheck
        //;		output cy  PMDB2又はADPCMがない
        //;==============================================================================
        private void check_pmdb2()
        {
            //;-----------------------------------------------------------------------------
            //;	PMDB2＆ADPCMの搭載CHECK
            //;-----------------------------------------------------------------------------
            r.ah = 0x10;
            pmd.int60_main(r.ax);//;get_workadr in DS:DX
            r.bx = r.dx;
            r.bx = 0;// mov bx,-2[bx]	;ds:bx = open_work
            if (pw.pcm_gs_flag != 0)
                goto cpb_stc_ret;// ; ERROR Return
            r.ax = (ushort)pw.fm2_port1;
            pw.port46 = r.ax;
            r.ax = (ushort)pw.fm2_port2;
            pw.port47 = r.ax;
            r.carry = false;
            return;

        cpb_stc_ret:;
            r.carry = true;
            return;
        }



        //865-890
        //;==============================================================================
        //;	P86DRVの常駐Check
        //;		output cy  P86DRVがない
        //;==============================================================================
        private void check_p86drv()
        {
            r.carry = !pw.useP86DRV;
        }



        //907-959
        //;==============================================================================
        //;	Filenameの大文字化＆パス名回避処理
        //;==============================================================================
        private void filename_set()
        {
            //;-----------------------------------------------------------------------------
            //;	Filenameを小文字から大文字に変換(SHIFTJIS回避付き)
            //;-----------------------------------------------------------------------------
            pw.filename_ofs = pw.filename_ofs.ToUpper().Trim();

            //;-----------------------------------------------------------------------------
            //;	Filename中のパス名を抜いたfilename_ofs2を設定(File名比較用)
            //;-----------------------------------------------------------------------------
            pw.filename_ofs2 = Path.GetFileName(pw.filename_ofs);

        }



        //960-977
        //;==============================================================================
        //;	PMDのワークにFilenameを書く
        //;==============================================================================
        private void write_filename_to_pmdwork()
        {
            r.si = 0;
            byte[] fnba = System.Text.Encoding.GetEncoding("shift_jis").GetBytes(pw.filename_ofs2);
            r.di = 4 * 256 + 2;//; ES:DI = PMD内PCM_WORKのFilename格納位置
            r.cx = 128;//;byte数

            while (r.cx != 0)
            {
                if (r.si < fnba.Length)
                {
                    pw.pcmWk[r.di] = fnba[r.si];
                    r.si++;
                }
                else
                {
                    pw.pcmWk[r.di] = 0; //; 残りを０で埋める
                }
                r.di++;
                r.cx--;
            }
        }



        //978-1189
        //;==============================================================================
        //;	ＰＣＭメモリへメインメモリからデータを送る(x8, 高速/低速選択版)
        //;
        //;	INPUTS..cs:[pcmstart] to Start Address
        //;		.. cs:[pcmstop] to Stop  Address
        //;		.. cs:[pcmdata_ofs/seg]
        //        to PCMData_Buffer
        //;==============================================================================
        private void pcmstore()
        {
            key_check_reset();

            r.dx = 0x0001;
            out46();
            r.dx = 0x1017;//;brdy以外はマスク(=timer割り込みは掛からない)
            out46();
            r.dx = 0x1080;
            out46();
            r.dx = 0x0060;
            out46();
            r.dx = 0x0102;//;x8
            out46();
            r.dx = 0x0cff;
            out46();
            r.dh++;
            out46();

            r.bx = pw.pcmload_pcmstart;
            r.dh = 0x02;
            r.dl = r.bl;
            out46();
            r.dh++;
            r.dl = r.bh;
            out46();
            r.dx = 0x04ff;
            out46();
            r.dh++;
            out46();

            r.si = 0;//[pcmdata_ofs]
            r.cx = pw.pcmload_pcmstop;
            r.cx -= pw.pcmload_pcmstart;
            r.cx += r.cx;
            r.cx += r.cx;
            r.cx += r.cx;
            r.cx += r.cx;
            r.cx += r.cx;

            r.dx = pw.port46;
            r.bx = pw.port47;

            if (pw.adpcm_wait == 0)
                goto fast_store;
            if (pw.adpcm_wait == 1)
                goto middle_store;

            //;------------------------------------------------------------------------------
            //;	低速定義
            //;------------------------------------------------------------------------------
            slow_store:;
        //	cli
        //o4600z:	in	al,dx
        //    or  al,al
        //    js  o4600z
        //    mov al,8	;PCMDAT reg.
        //    out dx, al
        //    push    cx
        //    mov cx, cs:[wait_clock]
        //    loop    $
        //    pop cx
        //    xchg bx, dx
        //    lodsb
        //    out dx, al   ; OUT data
        //    sti
        //    xchg    dx,bx
        //o4601z:
        //	in	al,dx
        //    test    al,8	;BRDY check
        //    jz o4601z
        //o4601zb:
        //	test al, al; BUSY check
        //    jns o4601zc
        //	in	al,dx
        //    jmp o4601zb
        //o4601zc:
        //    mov al,10h
        //    cli
        //	out	dx,al
        //    push    cx
        //    mov cx,cs:[wait_clock]
        //        loop	$
        //	pop cx
        //    xchg dx, bx
        //    mov al,80h
        //	out	dx,al	;BRDY reset
        //    sti
        //    xchg    dx,bx
        //    loop    slow_store
        //    jmp pcmst_exit

        //;------------------------------------------------------------------------------
        //;	中速定義
        //;------------------------------------------------------------------------------
        middle_store:;
        //	call cli_sub
        //o4600y:	in	al,dx
        //    or  al,al
        //    js  o4600y
        //    mov al,8	;PCMDAT reg.
        //    out dx, al
        //middle_store_loop:
        //    push cx
        //    mov cx, cs:[wait_clock]
        //    loop    $
        //    pop cx
        //    xchg bx, dx
        //    lodsb
        //    out dx, al   ; OUT data
        //    xchg bx, dx
        //o4601y:
        //	in	al,dx
        //    test    al,8	;BRDY check
        //    jz o4601y
        //    loop middle_store_loop
        //    call sti_sub
        //    jmp pcmst_exit

        //;------------------------------------------------------------------------------
        //;	高速定義
        //;------------------------------------------------------------------------------
        fast_store:;
            cli_sub();

        o4600x:;
            r.al=pc98.InPort(r.dx);
            if ((r.al & 0x80) != 0) goto o4600x;
            r.al = 8;//;PCMDAT reg.
            pc98.OutPort(r.dx, r.al);
            r.stack.Push(r.cx);
            r.cx = pw.pcmload_wait_clock;
            //do
            //{
                //r.cx--;
            //} while (r.cx != 0);
            r.cx = r.stack.Pop();
            ushort b = r.bx;
            r.bx = r.dx;
            r.dx = b;

        fast_store_loop:;
            r.al = pw.pcmDt[r.si++];
            pc98.OutPort(r.dx, r.al);//; OUT data
            b = r.bx;
            r.bx = r.dx;
            r.dx = b;

        o4601x:;
            r.al = pc98.InPort(r.dx);
            //if ((r.al & 8) == 0)//;BRDY check
                //goto o4601x;

            b = r.bx;
            r.bx = r.dx;
            r.dx = b;

            r.cx--;
            if (r.cx != 0) goto fast_store_loop;

            sti_sub();

        pcmst_exit:;
            r.dx = 0x1000;
            out46();
            r.dx = 0x1080;
            out46();
            r.dx = 0x0001;
            out46();
            key_check_set();
        }

        //;------------------------------------------------------------------------------
        //;	RS-232C以外は割り込みを禁止する
        //;	(FM音源LSI の ADDRESSの変更をさせない為)
        //;------------------------------------------------------------------------------
        private void cli_sub()
        {
            r.stack.Push(r.ax);
            r.stack.Push(r.dx);
            //cli
            r.dx = pw.mmask_port;
            r.al = pc98.InPort(r.dx);
            pw.mmask_push = r.al;
            r.al |= 0b1110_1111;//;RSのみ変化させない
            pc98.OutPort(r.dx, r.al);
            //sti
            r.dx = r.stack.Pop();
            r.ax = r.stack.Pop();
            return;
        }

        //;------------------------------------------------------------------------------
        //;	上のsubroutineで禁止した割り込みを元に戻す
        //;------------------------------------------------------------------------------
        private void sti_sub()
        {
            r.stack.Push(r.ax);
            r.stack.Push(r.dx);
            //cli
            r.dx = pw.mmask_port;
            r.al = pw.mmask_push;
            pc98.OutPort(r.dx, r.al);
            //sti
            r.dx = r.stack.Pop();
            r.ax = r.stack.Pop();
            return;
        }



        //1300-1330
        //;==============================================================================
        //;	ＯＰＮＡ裏ポートへのデータの書き込み
        //;
        //;	Inputs..dh to Register
        //;		.. dl to Data
        //;==============================================================================
        private void out46()
        {
            r.stack.Push(r.dx);
            r.stack.Push(r.bx);
            r.bx = r.dx;
            r.dx = pw.port46;
        o4600:;
            r.al = pc98.InPort(r.dx);
            r.al |= r.al;
            if ((r.al & 0x80) != 0)
                goto o4600;
            r.al = r.bh;
            //cli
            pc98.OutPort(r.dx, r.al);
            r.stack.Push(r.cx);
            //r.cx = (ushort)pw.pcmload_wait_clock;
            //do
            //{
            //r.cx--;
            //} while (r.cx != 0);
            r.cx = r.stack.Pop();
            r.dx = pw.port47;
            r.al = r.bl;
            pc98.OutPort(r.dx, r.al);
            //sti
            r.bx = r.stack.Pop();
            r.dx = r.stack.Pop();
        }



        //1331-1374
        //;==============================================================================
        //;	PMDの ESC/GRPH入力を効かなくする
        //;	その他必要なデータをpmdのsegmentから読み取る
        //;		out	cy acccess flag on
        //;==============================================================================
        private void key_check_reset()
        {
            //r.stack.Push(r.ds);
            r.stack.Push(r.ax);
            r.stack.Push(r.bx);
            r.stack.Push(r.dx);
            r.stack.Push(r.di);

            r.ah = 0x10;
            pmd.int60_main(r.ax);
            r.bx = r.dx;
            r.di = (ushort)pw.part10;//18[bx]
            //mov bx,-2[bx]//KUMA:open_work
            r.ax = (ushort)pw.wait_clock;//_wait_clock[bx]
            pw.pcmload_wait_clock = r.ax;//;get wait_clock
            r.al = pw.adpcm_wait;//[bx]
            pw.pcmload_adpcm_wait = r.al;//;get adpcm_wait
            pw.mmask_port = 0x02;//;master_mask(98)
            if(pw.va!=0)//cmp word ptr ds:[84h],"AV"
            {
                pw.mmask_port = 0x18a;//;master_mask(VA)
            }

            r.carry = false;
            if (pw.pcm_access != 0) //; cf=0
                goto kcr_exit;
            r.al = pw.key_check;
            pw.key_check_push = r.al;
            pw.key_check = 0;
            pw.pcm_access = 1;
            pw.pcmflag = 0;//; 追加(効果音対策)
            pw.pcm_effec_num = 255;
            pw.partWk[r.di].partmask &= 0xfd;// bit1をclear
            r.carry = true;//;cf=1
        kcr_exit:;
            r.carry = !r.carry;

            r.di = r.stack.Pop();
            r.dx = r.stack.Pop();
            r.bx = r.stack.Pop();
            r.ax = r.stack.Pop();
            //r.ds = r.stack.Pop();
        }



        //1375-1396
        //;==============================================================================
        //;	PMDの ESC/GRPH入力を元に戻す
        //;	PCMメモリアクセスフラグをoff
        //;==============================================================================
        private void key_check_set()
        {
            //r.stack.Push(r.ds);
            r.stack.Push(r.ax);
            r.stack.Push(r.bx);
            r.stack.Push(r.dx);

            r.ah = 0x10;
            pmd.int60_main(r.ax);
            //    mov bx,dx
            //mov bx,-2[bx]//KUMA:open_work
            r.al = pw.key_check_push;
            pw.key_check = r.al;
            pw.pcm_access = 0;

            r.dx = r.stack.Pop();
            r.bx = r.stack.Pop();
            r.ax = r.stack.Pop();
            //r.ds = r.stack.Pop();
        }



    }
}
