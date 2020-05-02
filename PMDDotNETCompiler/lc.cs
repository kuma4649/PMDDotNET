using System;
using System.Collections.Generic;
using System.Text;
using musicDriverInterface;

namespace PMDDotNET.Compiler
{
    public class lc
    {
        private work work = null;
        private m_seg m_seg = null;
        private mc mc = null;

        public lc(mc mc, work work, m_seg m_seg)
        {
            this.mc = mc;
            this.work = work;
            this.m_seg = m_seg;
            setJumpTable();
        }

        //1-30
        //;==============================================================================
        //;	音長計算用 include file
        //;		in.al print_flag(0で非表示)
        //;==============================================================================
        //_print_mes macro   ofs
        //local   exit
        //   cmp[print_flag],0
        //    jz exit
        //    print_mes ofs
        //exit:
        //	endm

        public void lc_proc(byte al)
        {
            enmPart_ends ret;
            ret = enmPart_ends.calc_start;

            List<byte> dst = new List<byte>();
            dst.Add(0);
            for (int i = 0; i < m_seg.m_buf.Count; i++)
            {
                MmlDatum o = m_seg.m_buf.Get(i);
                dst.Add((byte)(o == null ? 0xff : o.dat));
            }
            System.IO.File.WriteAllBytes("c:\\temp\\debug", dst.ToArray());

            do
            {
                switch (ret)
                {
                    case enmPart_ends.calc_start:
                        ret = calc_start(al);
                        break;
                    case enmPart_ends.part_loop:
                        ret = part_loop();
                        break;
                    case enmPart_ends.part_loop2:
                        ret = part_loop2();
                        break;
                    case enmPart_ends.check_j:
                        ret = check_j();
                        break;
                    case enmPart_ends.com_loop:
                        ret = com_loop();
                        break;
                    case enmPart_ends.part_ends:
                        ret = part_ends();
                        break;
                    case enmPart_ends.partk_start:
                        ret = partk_start();
                        break;
                    case enmPart_ends.kcom_loop:
                        ret = kcom_loop();
                        break;
                    case enmPart_ends.kpart_end:
                        ret = kpart_end();
                        break;
                    case enmPart_ends.kl_00:
                        ret = kl_00();
                        break;
                }
            } while (ret != enmPart_ends.exit);
        }



        //31-39
        //;==============================================================================
        //;	計算開始
        //;==============================================================================
        private enmPart_ends calc_start(byte al)
        {
            print_flag = al;
            part_chr = 'A';
            work.bp = 0;//offset m_buf

            return enmPart_ends.part_loop;
        }



        //40-73
        //;==============================================================================
        //;	パート毎のループ
        //;==============================================================================
        private enmPart_ends part_loop()
        {
            work.si = (byte)m_seg.m_buf.Get(work.bp).dat + (byte)m_seg.m_buf.Get(work.bp + 1).dat * 0x100;
            work.si += 0;//offset m_buf
            work.bp += 2;

            return enmPart_ends.part_loop2;
        }



        private enmPart_ends part_loop2()
        {
            all_length[0] = 0;
            all_length[1] = 0;
            loop_length[0] = -1;
            loop_length[1] = -1;
            loop_flag = 0;

            //;==============================================================================
            //;	(Part Aの場合) 拡張のFM3ch目があるか調べる
            //;==============================================================================
            if (part_chr != 'A') return enmPart_ends.check_j;
            if (m_seg.m_buf.Get(work.si).dat != 0xc6) return enmPart_ends.check_j;

            work.si++;

            for (int i = 0; i < 3; i++)
            {
                byte l = (byte)m_seg.m_buf.Get(work.si++).dat;
                byte h = (byte)m_seg.m_buf.Get(work.si++).dat;
                fm3_adr[i] = h * 0x100 + l;
            }

            return enmPart_ends.check_j;
        }



        //74-93
        //;==============================================================================
        //;	(Part Jの場合) 拡張のPCMパートがあるか調べる
        //;==============================================================================
        private enmPart_ends check_j()
        {
            if (part_chr != 'J') return enmPart_ends.com_loop; //jnz com_loop
            if (m_seg.m_buf.Get(work.si).dat != 0xb4) return enmPart_ends.com_loop;

            work.si++;

            for (int i = 0; i < 8; i++)
            {
                byte l = (byte)m_seg.m_buf.Get(work.si++).dat;
                byte h = (byte)m_seg.m_buf.Get(work.si++).dat;
                pcm_adr[i] = h * 0x100 + l;
            }

            return enmPart_ends.com_loop;
        }



        //94-112
        //;==============================================================================
        //;	コマンド毎のループ
        //;==============================================================================
        private enmPart_ends com_loop()
        {
            do
            {
                byte al;
                do
                {
                    Log.WriteLine(LogLevel.TRACE, string.Format("si:{0}", work.si));
                    al = (byte)(work.si<m_seg.m_buf.Count ? m_seg.m_buf.Get(work.si++).dat : 0x80);

                    if (al == 0x80) return enmPart_ends.part_ends;
                    if (al >= 0x80) break;

                    al = (byte)m_seg.m_buf.Get(work.si++).dat;
                    //byte ah = 0;

                    all_length[0] += al;
                    all_length[1] += 0;//dummy本来はcarryが加算される
                } while (true);

                //cl_00:;
                command_exec(al);
                if (loop_flag != 0) return enmPart_ends.part_ends;

            } while (true);

            //return enmPart_ends.part_ends;
        }



        //113-151
        //;==============================================================================
        //;	パート終了
        //;==============================================================================
        private enmPart_ends part_ends()
        {
            print_length();

            part_chr++;
            if (part_chr < 'K') return enmPart_ends.part_loop;

            int di = work.di;
            work.di = 0;//offset fm3_adr1
            int bx = 0;//offset _fm3_partchr1;in cs
            int cx = 3 + 8;
            //extend_check_loop:;
            do
            {
                work.si = work.di < 3 ? fm3_adr[work.di] : pcm_adr[work.di - 3];
                if (work.si == 0) goto extend_check_next;
                work.si += 0;//offset m_buf
                char al_c = bx < 3 ? _fm3_partchr[bx] : _pcm_partchr[bx - 3];
                part_chr = al_c;
                if (work.di < 3) fm3_adr[work.di] = 0;
                else pcm_adr[work.di - 3] = 0;

                work.di = di;
                return enmPart_ends.part_loop2;

            extend_check_next:;
                work.di++;
                bx++;
                cx--;
            } while (cx > 0);

            work.di = di;
            return enmPart_ends.partk_start;
        }



        //152-169
        //;==============================================================================
        //;	Part K
        //;==============================================================================
        private enmPart_ends partk_start()
        {
            part_chr = 'K';
            all_length[0] = 0;
            all_length[1] = 0;
            loop_length[0] = -1;
            loop_length[1] = -1;
            loop_flag = 0;

            work.si = (int)(m_seg.m_buf.Get(work.bp).dat + (m_seg.m_buf.Get(work.bp + 1).dat * 0x100));
            work.si += 0;//offset m_buf
            work.bp += 2;
            work.bx = (int)(m_seg.m_buf.Get(work.bp).dat + (m_seg.m_buf.Get(work.bp + 1).dat * 0x100));
            work.bx += 0;//offset m_buf	; bx= R table 先頭番地

            return enmPart_ends.kcom_loop;
        }



        //170-191
        //;==============================================================================
        //;	Kpart/コマンド毎のループ
        //;==============================================================================
        private enmPart_ends kcom_loop()
        {
            do
            {
                int al = (byte)m_seg.m_buf.Get(work.si++).dat;
                if (al == 0x80) return enmPart_ends.kpart_end;
                if (al >= 0x80)
                {
                    work.al = (byte)al;
                    return enmPart_ends.kl_00;
                }
                al *= 2;

                //    push    si
                //    push    bx
                int si = work.si;
                int bx = work.bx;
                work.bx += al;
                work.si = (int)(m_seg.m_buf.Get(work.bx).dat + (m_seg.m_buf.Get(work.bx + 1).dat * 0x100));
                work.si += 0;//offset m_buf
                rcom_loop();
                work.bx = bx;
                work.si = si;

                if (loop_flag != 0) return enmPart_ends.kpart_end;
            } while (true);
        }



        //192-202
        //;==============================================================================
        //;	Kpart/各種特殊コマンド
        //;==============================================================================
        private enmPart_ends kl_00()
        {
            int bx = work.bx;
            command_exec((byte)work.al);
            work.bx = bx;

            if (loop_flag != 0) return enmPart_ends.kpart_end;
            return enmPart_ends.kcom_loop;
        }


        //203-209
        //;==============================================================================
        //;	Kpart/計算終了
        //;==============================================================================
        private enmPart_ends kpart_end()
        {
            print_length();
            return enmPart_ends.exit;
        }



        private enum enmPart_ends
        {
            calc_start,
            part_loop,
            part_loop2,
            check_j,
            com_loop,
            part_ends,
            partk_start,
            kcom_loop,
            kpart_end,
            kl_00,
            exit
        }



        //210-243
        //;==============================================================================
        //;	Rpart/コマンド毎のループ
        //;==============================================================================
        private void rcom_loop()
        {
            do
            {
                int al;
                do
                {
                    al = (byte)m_seg.m_buf.Get(work.si++).dat;
                    if (al == 0xff) goto rpart_end;
                    if (al >= 0xc0) break;

                    if ((al & 0x80) != 0)
                    {
                        work.si++;
                    }
                    //rl_01:;
                    al = (byte)m_seg.m_buf.Get(work.si++).dat;
                    all_length[0] += al;

                } while (true);
                //;==============================================================================
                //; Rpart / 各種特殊コマンド処理
                //;==============================================================================
                // rl_00:
                command_exec((byte)al);
                if (loop_flag != 0) goto rpart_end;
            } while (true);
        //;==============================================================================
        //; Rpart / 計算終了
        //;==============================================================================
        rpart_end:;

        }



        //244-264
        //;==============================================================================
        //;	各種コマンド
        //;==============================================================================
        private void command_exec(byte al)
        {
            al = (byte)~al;
            int ax = al;
            ax += 0;//offset jumptable
            jumptable[ax]();
            return;
        }

        private void jump16() { work.si += 16; }
        private void jump6() { work.si += 6; }
        private void jump5() { work.si += 5; }
        private void jump4() { work.si += 4; }
        private void jump3() { work.si += 3; }
        private void jump2() { work.si += 2; }
        private void jump1() { work.si += 1; }
        private void jump0() { work.si += 0; }



        //265-275
        //;==============================================================================
        //;	tempo
        //;==============================================================================
        private void _tempo()
        {
            byte al = (byte)m_seg.m_buf.Get(work.si++).dat;
            if (al >= 251)
            {
                work.si++;// 相対
            }
            //tempo_ret:;
        }



        //276-287
        //;==============================================================================
        //;	ポルタメント
        //;==============================================================================
        private void porta()
        {
            work.si += 2;

            byte al = (byte)m_seg.m_buf.Get(work.si++).dat;
            all_length[0] += al;
        }



        //288-297
        //;==============================================================================
        //;	L command
        //;==============================================================================
        private void loop_set()
        {
            int ax = all_length[0];
            loop_length[0] = ax;
        }



        //298-307
        //;==============================================================================
        //;	[command
        //;==============================================================================
        private void loop_start()
        {
            int ax = (byte)m_seg.m_buf.Get(work.si++).dat;
            ax += m_seg.m_buf.Get(work.si++).dat*0x100;

            work.bx = ax;
            work.bx += 1;//offset m_buf+1
            m_seg.m_buf.Set(work.bx, new musicDriverInterface.MmlDatum(0));
        }



        //308-332
        //;==============================================================================
        //;	] command
        //;==============================================================================
        private void loop_end()
        {
            byte al = (byte)m_seg.m_buf.Get(work.si++).dat;
            if (al == 0) goto loop_fset;//無条件loopがあった
            byte ah = al;
            m_seg.m_buf.Set(work.si, new musicDriverInterface.MmlDatum(m_seg.m_buf.Get(work.si).dat + 1));
            al = (byte)m_seg.m_buf.Get(work.si++).dat;
            if (ah != al) goto reloop;
            work.si++;
            work.si++;
            return;
        reloop:;
            int ax = (byte)m_seg.m_buf.Get(work.si++).dat;
            ax += m_seg.m_buf.Get(work.si++).dat * 0x100;
            ax += 2;//offset m_buf+2
            work.si = ax;
            return;
        loop_fset:;
            loop_flag = 1;
        }



        //333-350
        //;==============================================================================
        //;	: command
        //;==============================================================================
        private void loop_exit()
        {
            int ax = (byte)m_seg.m_buf.Get(work.si++).dat;
            ax += m_seg.m_buf.Get(work.si++).dat * 0x100;
            work.bx = ax;
            work.bx += 0;//offset m_buf
            byte dl = (byte)m_seg.m_buf.Get(work.bx).dat;
            dl--;
            work.bx++;
            if (dl == (byte)m_seg.m_buf.Get(work.bx).dat) goto loopexit;
            return;
        loopexit:;
            work.bx += 3;
            work.si = work.bx;
        }



        //351-367
        //;==============================================================================
        //;	0c0h + ?? special control
        //;==============================================================================
        private void special_0c0h()
        {
            byte al = (byte)m_seg.m_buf.Get(work.si++).dat;
            if (al < 2) goto spc0_ret;
            al = (byte)~al;
            al += 0;//offset jumptable_0c0h
            jumptable_0c0h[al]();
            spc0_ret:;
        }



        //368-428
        //;==============================================================================
        //;	長さを表示
        //;==============================================================================
        private void print_length()
        {
            if (all_length[0] == 0) return;// データ無し
            string msg = part_mes+part_chr+ part_chr_n;
            int ax = all_length[0];
            int dx = all_length[1];
            int bx = max_all[0];
            int cx = max_all[1];
            if (bx - ax < 0)
            {
                max_all[0] = ax;
                max_all[1] = dx;
            }
            //not_over_all:;
            msg += string.Format("{0}", ax);
            if (loop_flag != 1) goto pe_loop;
            mc.print_mes(loop_mes2);
            return;
        pe_loop:;
            dx = loop_length[1];
            ax = loop_length[0];
            ax++;
            if (ax == 0) goto pe_00;
            msg += loop_mes;
            //mc.print_mes(loop_mes);
            ax = all_length[0];
            dx = all_length[1];
            ax -= loop_length[0];
            bx = max_loop[0];
            if (bx - ax < 0)
            {
                max_loop[0] = ax;
            }
            //not_over_loop:;
            msg += string.Format("{0}", ax);
        pe_00:;
            mc.print_mes(msg);
            //mc.print_mes(_crlf_mes);
            //pe_01:;
        }



        private Action[] jumptable;
        private Action[] jumptable_0c0h;

        private void setJumpTable()
        {
            jumptable = new Action[] {
                 jump1// 0ffh
                ,jump1
                ,jump1
                ,_tempo
                ,jump0
                ,jump2
                ,loop_start
                ,loop_end// 0f8h
                ,loop_exit
                ,loop_set
                ,jump1
                ,jump0
                ,jump0
                ,jump4
                ,jump1
                ,jump4// 0f0h
                ,jump2
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1// 0e8h
                ,jump1
                ,jump1
                ,jump2
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1// 0e0h
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,porta
                ,jump1
                ,jump1// 0d8h
                ,jump1
                ,jump2
                ,jump2
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1// 0d0h
                ,jump1
                ,jump6
                ,jump5
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump3// 0c8h
                ,jump3
                ,jump6
                ,jump1
                ,jump1
                ,jump2
                ,jump1
                ,jump0
                ,special_0c0h// 0c0h
                ,jump4
                ,jump1
                ,jump2
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump2// 0b8h
                ,jump1
                ,jump1
                ,jump2
                ,jump16// 0b4h
                ,jump1// 0b3h
                ,jump1// 0b2h
                ,jump1// 0b1h
            };

            jumptable_0c0h = new Action[] {
                jump1  // 0ffh
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1
                ,jump1// 0f8h
                ,jump1
                ,jump1
                ,jump1
            };
        }


        public string part_mes = "Part ";
        public char part_chr = ' ';
        private string part_chr_n = "\tLength : ";
        private string loop_mes = "\t/ Loop : ";
        private string loop_mes2 = "\t/ Found Infinite Local Loop!";
        //private string _crlf_mes = "\r\n$";

        public byte print_flag = 0;
        public int[] all_length = new int[2] { 0, 0 };
        public int[] loop_length = new int[2] { 0, 0 };
        public int[] max_all = new int[2] { 0, 0 };
        public int[] max_loop = new int[2] { 0, 0 };

        public int[] fm3_adr = new int[3] { 0, 0, 0 };
        public int[] pcm_adr = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

        public byte loop_flag = 0;

        public char[] _fm3_partchr = new char[3]{
            (char)0,
            (char)0,
            (char)0
        };

        public  char[] _pcm_partchr = new char[8]
        {
            (char)0,
            (char)0,
            (char)0,
            (char)0,
            (char)0,
            (char)0,
            (char)0,
            (char)0
        };

    }
}
