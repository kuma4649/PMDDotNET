using System;
using System.Collections.Generic;
using System.Text;
using musicDriverInterface;

namespace PMDDotNET.Driver
{
    public class PMD
    {
        private PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;


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


        public PMD()
        {
            pw = new PW();
        }
    }
}
