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

        //get_psp macro
        //		mov ah,51h
        //		int	21h
        //		endm

        //msdos_exit macro
        //		mov ax,4c00h
        //		int	21h
        //		endm

        //error_exit macro   qq
        //	   mov ax,4c00h+qq
        //		int	21h
        //		endm

        //print_mes macro   qq
        //		if	va
        //			push    si
        //			lea si,qq
        //			mov dh,80h
        //			mov ah,02h
        //			int	83h
        //			pop si
        //		else
        //			mov dx, offset qq
        //			 mov ah,09h
        //			int	21h
        //		endif
        //		endm

        //print_dx    macro
        //		if	va
        //			push    si
        //			mov si,dx
        //			mov dh,80h
        //			mov ah,02h
        //			int	83h
        //			pop si
        //		else
        //			mov ah,09h
        //			int	21h
        //		endif
        //		endm

        //debug       macro qq
        //		push es
        //		push ax
        //		mov ax,0a000h
        //		mov es,ax
        //		inc byte ptr es:[qq*2]
        //		pop ax
        //		pop es
        //		endm

        //debug2      macro q1, q2
        //		push es
        //		push ax
        //		mov ax,0a000h
        //		mov es,ax
        //		mov byte ptr es:[q1*2],q2
        //pop ax
        //pop es
        //endm

        //debug_pcm macro   qq
        //local       zzzz
        //	  push    ax
        //	  push    dx
        //	  mov dx,0a468h
        //		in	al,dx
        //		test    al,10h
        //		jz  zzzz
        //		debug   qq
        //zzzz:		pop dx
        //		pop ax
        //		endm

        //_wait       macro
        //		mov cx,[wait_clock]
        //		loop	$
        //		endm

        //_waitP      macro
        //		push    cx
        //		mov cx,[wait_clock]
        //		loop	$
        //		pop cx
        //		endm

        //_rwait      macro			;リズム連続出力用wait
        //		push    cx
        //		mov cx,[wait_clock]
        //		add cx, cx
        //		add cx, cx
        //		add cx, cx
        //		add cx, cx
        //		add cx, cx; x32
        //loop	$
        //		pop cx
        //		endm

        //rdychk      macro			;Address out時用	break:ax
        //local       loop
        //		in	al,dx		;無駄読み
        //loop:		in	al,dx
        //		test    al,al
        //		js  loop
        //		endm

        //_ppz macro
        //local exit
        //if		ppz
        //		cmp[ppz_call_seg],2
        //		jc exit
        //		call dword ptr[ppz_call_ofs]
        //exit:
        //endif
        //		endm



    }
}
