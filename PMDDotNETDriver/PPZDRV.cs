using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PPZDRV
    {
        private PMD pmd = null;
        private PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;
        private PPZ8em ppz8em = null;
        public PCMDRV pcmdrv = null;
        private int bank = 0;
        private int ptr = 0;

        public PPZDRV(PMD pmd, PW pw, x86Register r, Pc98 pc98, PPZ8em ppz8em)
        {
            this.pmd = pmd;
            this.pw = pw;
            this.r = r;
            this.pc98 = pc98;
            this.ppz8em = ppz8em;
        }

        public void init()
        {
            SetupCmdtbl();
        }

        //;==============================================================================
        //;	ＰＣＭ音源 演奏 メイン[PPZ8]
        //;==============================================================================
        public void ppz8_call()
        {
            //出来るだけppz8emを直接コールしてください
            throw new NotImplementedException();
        }






        public void ppzmain()
        {
            r.si = pw.partWk[r.di].address;//; si = PART DATA ADDRESS
            if (r.si == 0)
                return;// goto pcmmain_ret;

            Func<object> ret = null;
            if (pw.partWk[r.di].partmask != 0)
                ret = ppzmain_nonplay;
            else
                ret = ppzmain_c_1;

            if (ret != null)
            {
                do
                {
                    ret = (Func<object>)ret();
                } while (ret != null);
            }
        }

        private Func<object> ppzmain_c_1()
        {
            //; 音長 - 1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //; KEYOFF CHECK
            if ((pw.partWk[r.di].keyoff_flag & 3) != 0)//; 既にkeyoffしたか？
                goto mp0z;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                goto mp0z;

            pw.partWk[r.di].keyoff_flag = 0xff;//-1
            keyoffz();//; ALは壊さない

        mp0z:;//; LENGTH CHECK
            if (r.al != 0) return mpexitz;
            return mp1z0;
        }

        private Func<object> mp1z0()
        {
            pw.partWk[r.di].lfoswi &= 0xf7;//; Porta off
            return mp1z;
        }

        private Func<object> mp1z()//; DATA READ
        {
            do
            {
                r.al = (byte)pw.md[r.si++].dat;
                if (r.al < 0x80) goto mp2z;
                if (r.al == 0x80) goto mp15z;

                //; ELSE COMMANDS
                object o = commandsz();
                while (o != null && (Func<object>)o != mp1z)
                {
                    o = ((Func<object>)o)();
                    if ((Func<object>)o == pmd.mnp_ret)
                        return pmd.mnp_ret;
                    if ((Func<object>)o == porta_returnz)
                        return porta_returnz;
                }
            } while (true);

        //; END OF MUSIC['L' ｶﾞ ｱｯﾀﾄｷﾊ ｿｺﾍ ﾓﾄﾞﾙ]
        mp15z:;
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) return mpexitz;

            //; 'L' ｶﾞ ｱｯﾀﾄｷ
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            return mp1z;

        mp2z:;//; F - NUMBER SET
            pmd.lfoinitp();
            pmd.oshift();
            fnumsetz();

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            pmd.calc_q();
            return porta_returnz;
        }

        private Func<object> porta_returnz()
        {
            if (pw.partWk[r.di].volpush == 0) goto mp_newz;
            if (pw.partWk[r.di].onkai == 0xff) goto mp_newz;
            pw.volpush_flag--;
            if (pw.volpush_flag == 0) goto mp_newz;
            pw.volpush_flag = 0;
            pw.partWk[r.di].volpush = 0;
        mp_newz:;
            volsetz();
            otodasiz();
            if ((pw.partWk[r.di].keyoff_flag & 1) == 0)
                goto mp3z;
            keyonz();

        mp3z:;
            pw.partWk[r.di].keyon_flag++;
            pw.partWk[r.di].address = r.si;
            r.al = 0;
            pw.tieflag = r.al;
            pw.volpush_flag = r.al;
            pw.partWk[r.di].keyoff_flag = r.al;
            if (pw.md[r.si].dat != 0xfb)//; '&'が直後にあったらkeyoffしない
                return pmd.mnp_ret;
            pw.partWk[r.di].keyoff_flag = 2;
            return pmd.mnp_ret;
        }

        private Func<object> mpexitz()
        {
            r.cl = pw.partWk[r.di].lfoswi;
            r.al = r.cl;
            r.al &= 8;
            pw.lfo_switch = r.al;
            if (r.cl == 0)
                goto volsz;
            if ((r.cl & 3) == 0)
                goto not_lfoz;

            pmd.lfo();
            if (!r.carry) goto not_lfoz;
            r.al = r.cl;
            r.al &= 3;
            pw.lfo_switch |= r.al;
        not_lfoz:;
            if ((r.cl & 0x30) == 0)
                goto not_lfoz2;
            //pushf
            //cli
            pmd.lfo_change();
            pmd.lfo();
            if (!r.carry) goto not_lfoz1;
            pmd.lfo_change();
            //popf
            r.al = pw.partWk[r.di].lfoswi;
            r.al &= 0x30;
            pw.lfo_switch |= r.al;
            goto not_lfoz2;
        not_lfoz1:;
            pmd.lfo_change();
        //popf
        not_lfoz2:;
            if ((pw.lfo_switch & 0x19) == 0)
                goto volsz;
            if ((pw.lfo_switch & 8) == 0)
                goto not_portaz;
            pmd.porta_calc();
        not_portaz:;
            otodasiz();
        volsz:;
            pmd.soft_env();
            if (r.carry) goto volsz2;
            if ((pw.lfo_switch & 0x22) != 0)
                goto volsz2;
            if (pw.fadeout_speed == 0)
                return pmd.mnp_ret;
            volsz2:;
            volsetz();
            return pmd.mnp_ret;
        }



        //146-153
        //;==============================================================================
        //;	ＰＣＭ音源演奏メイン：パートマスクされている時
        //;==============================================================================
        private Func<object> ppzmain_nonplay()
        {
            pw.partWk[r.di].keyoff_flag = 0xff;// -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return pmd.mnp_ret;

            return ppzmnp_1;
        }



        //154-181
        private Func<object> ppzmnp_1()
        {
            do
            {
                do
                {
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;
                    if (r.al < 0x80) return ppzmnp_3;

                    object o = commandsz();
                    while (o != null && (Func<object>)o != ppzmnp_1)
                    {
                        o = ((Func<object>)o)();
                        if ((Func<object>)o == pmd.mnp_ret)
                            return pmd.mnp_ret;
                    }
                } while (true);

                //pcmmnp_2:
                //; END OF MUSIC["L"があった時はそこに戻る]
                r.si--;
                pw.partWk[r.di].address = r.si;
                pw.partWk[r.di].loopcheck = 3;
                pw.partWk[r.di].onkai = 0xff;//-1
                r.bx = pw.partWk[r.di].partloop;

                if ((r.bx & r.bx) == 0) return pmd.fmmnp_4;

                //; "L"があった時
                r.si = r.bx;
                pw.partWk[r.di].loopcheck = 1;
            } while (true);
        }

        private Func<object> ppzmnp_3()
        {
            pw.partWk[r.di].fnum2 = 0;
            return pmd.fmmnp_3;
        }



        //182-
        //;==============================================================================
        //;	ＰＣＭ音源特殊コマンド処理
        //;==============================================================================

        private Func<object> commandsz()
        {
            pw.currentCommandTable = cmdtblz;
            r.bx = 0;//offset cmdtblp
            return pmd.command00();
        }

        private Func<object>[] cmdtblz;
        private void SetupCmdtbl()
        {
            cmdtblz = new Func<object>[] {
                  comAtz                    //0xff(0)
                , pmd.comq                  //0xfe(1)
                , pmd.comv                  //0xfd(2)
                , pmd.comt                  //0xfc(3)
                , pmd.comtie                //0xfb(4)
                , pmd.comd                  //0xfa(5)
                , pmd.comstloop             //0xf9(6)
                , pmd.comedloop             //0xf8(7)
                , pmd.comexloop             //0xf7(8)
                , pmd.comlopset             //0xf6(9)
                , pmd.comshift              //0xf5(10)
                , pcmdrv.comvolupm          //0xf4(11)
                , pcmdrv.comvoldownm        //0xf3(12)
                , pmd.lfoset                //0xf2(13)
                , pmd.lfoswitch             //0xf1(14)
                , pmd.psgenvset             //0xf0(15)
                , pmd.comy                  //0xef(16)
                , pmd.jump1                 //0xee(17)
                , pmd.jump1                 //0xed(18)
                //;
                , pansetz                   //0xec(19)
                , pmd.rhykey                //0xeb(20)
                , pmd.rhyvs                 //0xea(21)
                , pmd.rpnset                //0xe9(22)
                , pmd.rmsvs                 //0xe8(23)
                //;
                , pmd.comshift2             //0xe7(24)
                , pmd.rmsvs_sft             //0xe6(25)
                , pmd.rhyvs_sft             //0xe5(26)
                //;
                , pmd.jump1                 //0xe4(27)
                //;
                , pcmdrv.comvolupm2         //0xe3(28)
                , pcmdrv.comvoldownm2       //0xe2(29)
                //;
                , pmd.jump1                 //0xe1(30)
                , pmd.jump1                 //0xe0(31)
                //;
                , pmd.syousetu_lng_set      //0DFH(32)
                //;
                , pmd.vol_one_up_pcm        //0deH(33)
                , pmd.vol_one_down          //0DDH(34)
                //;
                , pmd.status_write          //0DCH(35)
                , pmd.status_add            //0DBH(36)
                //;
                , portaz                    //0DAH(37)
                //;
                , pmd.jump1                 //0D9H(38)
                , pmd.jump1                 //0D8H(39)
                , pmd.jump1                 //0D7H(40)
                //;
                , pmd.mdepth_set            //0D6H(41)
                //;
                , pmd.comdd                 //0d5h(42)
                //;
                , pmd.ssg_efct_set          //0d4h(43)
                , pmd.fm_efct_set           //0d3h(44)
                , pmd.fade_set              //0d2h(45)
                //;
                , pmd.jump1                 //0xd1(46)
                , pmd.jump1                 //0d0h(47)
                //;
                , pmd.jump1                 //0cfh(48)
                , ppzrepeat_set             //0ceh(49)
                , pmd.extend_psgenvset      //0cdh(50)
                , pmd.jump1                 //0cch(51)
                , pmd.lfowave_set           //0cbh(52)
                , pmd.lfo_extend            //0cah(53)
                , pmd.envelope_extend       //0c9h(54)
                , pmd.jump3                 //0c8h(55)
                , pmd.jump3                 //0c7h(56)
                , pmd.jump6                 //0c6h(57)
                , pmd.jump1                 //0c5h(58)
                , pmd.comq2                 //0c4h(59)
                , pansetz_ex                //0c3h(60)
                , pmd.lfoset_delay          //0c2h(61)
                , pmd.jump0                 //0c1h,sular(62)
                , ppz_mml_part_mask         //0c0h(63)
                , pmd._lfoset               //0bfh(64)
                , pmd._lfoswitch            //0beh(65)
                , pmd._mdepth_set           //0bdh(66)
                , pmd._lfowave_set          //0bch(67)
                , pmd._lfo_extend           //0bbh(68)
                , pmd._volmask_set          //0bah(69)
                , pmd._lfoset_delay         //0b9h(70)
                , pmd.jump2                 //0xb8(71)
                , pmd.mdepth_count          //0b7h(72)
                , pmd.jump1                 //0xb6(73)
                , pmd.jump2                 //0xb5(74)
                , pmd.jump16                //0b4h(75)
                , pmd.comq3                 //0b3h(76)
                , pmd.comshift_master       //0b2h(77)
                , pmd.comq4                 //0b1h(78)
            };
        }






        //284-316
        //;==============================================================================
        //;	ppz 拡張パートセット
        //;==============================================================================
        public Func<object> ppz_extpartset()
        {
            r.stack.Push(r.di);
            r.di = (ushort)pw.part10a;//offset part10a
            r.cx = 8;
        ppz_ex_loop:;
            r.ax = (ushort)((byte)pw.md[r.si].dat + (byte)pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if (r.ax == 0)
                goto no_init_ppz;
            r.ax += (ushort)pw.mmlbuf;
            pw.partWk[r.di].address = r.ax;

            pw.partWk[r.di].leng = 1;//; ｱﾄ 1ｶｳﾝﾄ ﾃﾞ ｴﾝｿｳ ｶｲｼ
            r.al = 0xff;//-1
            pw.partWk[r.di].keyoff_flag = r.al;//; 現在keyoff中
            pw.partWk[r.di].mdc = r.al;//; MDepth Counter(無限)
            pw.partWk[r.di].mdc2 = r.al;
            pw.partWk[r.di]._mdc = r.al;
            pw.partWk[r.di]._mdc2 = r.al;
            pw.partWk[r.di].onkai = r.al;//rest
            pw.partWk[r.di].volume = 128;//; PCM VOLUME DEFAULT = 128
            pw.partWk[r.di].fmpan = 5;//; PAN = Middle

        no_init_ppz:;
            r.di++;// type qq
            r.cx--;
            if (r.cx != 0) goto ppz_ex_loop;

            ppzext_exit:;
            r.di = r.stack.Pop();
            return null;
        }



        //317-348
        private Func<object> ppz_mml_part_mask()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "ppz_mml_part_mask");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 2)
                return pmd.special_0c0h;

            if (r.al == 0)
                goto ppz_part_maskoff_ret;

            pw.partWk[r.di].partmask |= 0x40;
            if (pw.partWk[r.di].partmask != 0x40)
                goto pmpz_ret;

            r.al = pw.partb;
            if (pw.ademu != 0)
            {
                if (r.al != 7)
                    goto pmpz_exec;
                if (pw.adpcm_emulate == 1)
                    goto pmpz_ret;
                pmpz_exec:;
            }
            r.ah = 2;
            ppz8em.StopPCM(r.al);

        pmpz_ret:;
            //r.ax = r.stack.Pop();//; commandsm
            return ppzmnp_1;

        ppz_part_maskoff_ret:;
            pw.partWk[r.di].partmask &= 0xbf;
            if (pw.partWk[r.di].partmask != 0)
                goto pmpz_ret;
            //r.ax = r.stack.Pop();//; commandsm
            return mp1z;//;パート復活
        }



        //349-
        //;==============================================================================
        //;	リピート設定
        //;==============================================================================
        private Func<object> ppzrepeat_set()
        {
            ppz_voicetable_calc();
            r.dx = (ushort)(ppz8em.pcmData[bank][ptr + 6] + ppz8em.pcmData[bank][ptr + 7] * 0x100);
            r.cx = (ushort)(ppz8em.pcmData[bank][ptr + 4] + ppz8em.pcmData[bank][ptr + 5] * 0x100);// dx: cx = データ量

            r.stack.Push(r.si);
            r.stack.Push(r.di);

            get_loop_ppz8();
            r.stack.Push(r.ax);
            r.stack.Push(r.bx);
            get_loop_ppz8();
            r.di = r.bx;
            r.si = r.ax;
            r.dx = r.stack.Pop();
            r.cx = r.stack.Pop();

            r.ah = 0xe;
            r.al = pw.partb;
            ppz8em.SetLoopPoint(r.al, r.dx, r.cx, r.di, r.si);
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            r.si += 6;
            return null;
        }

        private void get_loop_ppz8()
        {
            r.bx = 0;
            r.ax = (ushort)((byte)pw.md[r.si].dat + (byte)pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if ((r.ax & 0x8000) == 0)
                goto glp_ret;
            r.bx--;
            r.carry = (r.ax + r.cx) > 0xffff;
            r.ax += r.cx;
            r.bx += (ushort)(r.dx + (r.carry ? 1 : 0));
        glp_ret:;
            return;
        }

        private void ppz_voicetable_calc()
        {
            r.dx = 0;
            r.dl = pw.partWk[r.di].voicenum;

            r.ax = 0x040d;
            if ((r.dl & 0x80) == 0)
                goto pvc_a;
            r.dl &= 0x7f;
            r.al++;
        pvc_a:;
            ppz8em.ReadStatus(r.al);//;in. ES: BX
            bank = ppz8em.bank;
            ptr = ppz8em.ptr;

            ptr += 0x20;//; PZI Header Skip
            r.dx += r.dx;
            r.cx = r.dx;
            r.dx += r.dx;
            r.dx += r.dx;
            r.dx += r.dx;
            r.dx += r.cx;//; x 12h
            ptr += r.dx;
        }



        //412-467
        //352-397
        //;==============================================================================
        //;	ポルタメント(PCM)
        //;==============================================================================
        private Func<object> portaz()
        {
            if (pw.partWk[r.di].partmask != 0)
            {
                //return pmd.porta_notset;
                r.al = (byte)pw.md[r.si++].dat;//;最初の音程を読み飛ばす(Mask時)
                return null;
            }

            //pop ax; commandsp
            r.al = (byte)pw.md[r.si++].dat;
            pmd.lfoinitp();
            pmd.oshift();
            fnumsetz();

            r.ax = pw.partWk[r.di].fnum;
            r.stack.Push(r.ax);
            r.ax = pw.partWk[r.di].fnum2;
            r.stack.Push(r.ax);
            r.al = pw.partWk[r.di].onkai;
            r.stack.Push(r.ax);

            r.al = (byte)pw.md[r.si++].dat;
            pmd.oshift();
            fnumsetz();
            r.dx = pw.partWk[r.di].fnum2;
            r.ax = pw.partWk[r.di].fnum;//; ax = ポルタメント先のdelta_n値

            r.bx = r.stack.Pop();
            pw.partWk[r.di].onkai = r.bl;
            r.cx = r.stack.Pop();
            pw.partWk[r.di].fnum2 = r.cx;
            r.bx = r.stack.Pop();//; bx = ポルタメント元のdelta_n値
            pw.partWk[r.di].fnum = r.bx;

            r.carry = r.ax < r.bx;
            r.ax -= r.bx;
            r.dx -= (ushort)(r.cx + (r.carry ? 1 : 0));//; dx:ax = delta_n差

            for (int i = 0; i < 4; i++)
            {
                r.carry = (r.dx & 1) != 0;
                r.dx >>= 1;
                //bool c = (r.ax & 1) != 0;
                r.ax = (ushort)((r.carry ? 0x8000 : 0) | (r.ax >> 1)); //; /16
                //r.carry = c;
            }

            r.bl = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.bl;
            pmd.calc_q();

            r.bh = 0;
            int src = (short)r.ax;
            r.dx = (ushort)(src % (short)r.bx);//; ax = delta_n差 / 音長
            r.ax = (ushort)(src / (short)r.bx);
            pw.partWk[r.di].porta_num2 = r.ax;//;商
            pw.partWk[r.di].porta_num3 = r.dx;//;余り
            pw.partWk[r.di].lfoswi |= 8;//;Porta ON
            return porta_returnz;
        }



        //468-489
        //;==============================================================================
        //;	COMMAND 'p' [Panning Set]
        //;		0=0	無音
        //;		1=9	右
        //;		2=1	左
        //;		3=5	中央
        //;==============================================================================
        private Func<object> pansetz()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.bh = 0;
            r.bl = r.al;
            r.bx += 0;//offset ppzpandata
            r.al = pw.ppzpandata[r.bx];
            return pansetz_main;
        }

        private Func<object> pansetz_main()
        { 
            pw.partWk[r.di].fmpan = r.al;
            r.dx = 0;
            r.dl = r.al;
            r.ah = 0x13;
            r.al = pw.partb;
            ppz8em.SetPan(r.al, r.dx);
            return null;
        }



        //490-510
        //;==============================================================================
        //;	Pan setting Extend
        //;		px -4～+4
        //;==============================================================================
        private Func<object> pansetz_ex()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.si++;//;逆相flagは読み飛ばす
            if ((r.al & 0x80) != 0)
                goto pzex_minus;
            if (r.al < 5)
                goto pzex_set;
            r.al = 4;
            goto pzex_set;
        pzex_minus:;
            if (r.al >= 0xfc)
                goto pzex_set;
            r.al = 0xfc;

        pzex_set:;
            r.al += 5;
            return pansetz_main;
        }



        //511-567
        //;==============================================================================
        //;	COMMAND '@' [NEIRO Change]
        //;==============================================================================
        private Func<object> comAtz()
        {
            Func<object> ret = null;

            r.al = (byte)pw.md[r.si++].dat;
            if (pw.ademu != 0)
            {
                if (pw.adpcm_emulate != 1)
                    goto cAtz_adchk_exit;
                if ((r.al & 0x80) == 0)
                    goto cAtz_partchk;
                r.al = 127;//; ADPCMEmulate中は @128～なら @127に強制変更
            cAtz_partchk:;
                if (pw.partb != 7)
                    goto cAtz_adchk_exit;
                r.bx = (ushort)pw.part10;//; PPZADEmuPart
                pw.partWk[r.bx].partmask |= 0x10;//;Mask
                pw.partWk[r.bx].partmask &= 0xef;//;Mask off
                if (pw.partWk[r.bx].partmask != 0)
                    goto cAtz_emuoff;
                //r.bx = r.stack.Pop();
                ret = mp1z;//; Part復活準備
                //r.stack.Push(r.bx);
            cAtz_emuoff:;
                r.stack.Push(r.ax);
                r.ax = 0x1800;
                pw.adpcm_emulate = r.al;
                ppz8em.SetAdpcmEmu(r.al);//; ADPCMEmulate OFF
                r.ax = r.stack.Pop();
            cAtz_adchk_exit:;
            }
            pw.partWk[r.di].voicenum = r.al;

        ppz_neiro_reset:;
            //    push es
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            ppz_voicetable_calc();
            r.dx = (ushort)(ppz8em.pcmData[bank][ptr + 0xa] + ppz8em.pcmData[bank][ptr + 0xb] * 0x100);
            r.cx = (ushort)(ppz8em.pcmData[bank][ptr + 0x8] + ppz8em.pcmData[bank][ptr + 0x9] * 0x100);// dx: cx = Loop Start
            r.di = (ushort)(ppz8em.pcmData[bank][ptr + 0xe] + ppz8em.pcmData[bank][ptr + 0xf] * 0x100);
            r.si = (ushort)(ppz8em.pcmData[bank][ptr + 0xc] + ppz8em.pcmData[bank][ptr + 0xd] * 0x100);// dx: cx = Loop End
            r.ah = 0xe;
            r.al = pw.partb;
            //push es
            r.stack.Push(r.bx);
            ppz8em.SetLoopPoint(r.al, r.dx, r.cx, r.di, r.si);
            r.bx = r.stack.Pop();
            //pop es
            r.dx = (ushort)(ppz8em.pcmData[bank][ptr + 0x10] + ppz8em.pcmData[bank][ptr + 0x11] * 0x100);//;dx = Frequency
            r.ah = 0x15;
            r.al = pw.partb;
            ppz8em.SetSrcFrequency(r.al, r.dx);
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
        //    pop es
        cAtz_exit:;
            return ret;
        }



        //568-
        //486-602
        //;==============================================================================
        //;	PPZ VOLUME SET
        //;==============================================================================
        private void volsetz()
        {
            r.al = pw.partWk[r.di].volpush;
            if (r.al != 0)
                goto vsz_01;
            r.al = pw.partWk[r.di].volume;
        vsz_01:;
            r.dl = r.al;
            //;------------------------------------------------------------------------------
            //;	音量down計算
            //;------------------------------------------------------------------------------
            r.al = pw.ppz_voldown;
            if (r.al == 0)
                goto ppz_fade_calc;
            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	Fadeout計算
        //;------------------------------------------------------------------------------
        ppz_fade_calc:;
            r.al = pw.fadeout_volume;
            if (r.al == 0)
                goto ppz_env_calc;
            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	ENVELOPE 計算
        //;------------------------------------------------------------------------------
        ppz_env_calc:;
            r.al = r.dl;
            if (r.al == 0)//; 音量0?
                goto zv_out;
            if (pw.partWk[r.di].envf != 0xff)//-1
                goto normal_zvset;
            //; 拡張版 音量 = al * (eenv_vol + 1) / 16
            r.dl = pw.partWk[r.di].eenv_volume;
            if (r.dl == 0)
                goto zv_min;
            r.dl++;
            r.ax = (ushort)(r.al * r.dl);
            r.ax >>= 3;
            r.carry = ((r.ax & 1) != 0);
            r.ax >>= 1;
            if (!r.carry) goto zvset;
            r.ax++;
            goto zvset;

        normal_zvset:;
            r.ah = pw.partWk[r.di].eenv_volume;//.penv;
            if ((r.ah & 0x80) == 0)
                goto zvplus;
            //; -
            r.ah = (byte)-r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.carry = r.al - r.ah < 0;
            r.al -= r.ah;
            if (!r.carry) goto zvset;
            zv_min:;
            r.al = 0;
            goto zv_out;
        //; +
        zvplus:;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.carry = r.al + r.ah > 0xff;
            r.al += r.ah;
            if (!r.carry) goto zvset;
            r.al = 255;
        //;------------------------------------------------------------------------------
        //;	音量LFO計算
        //;------------------------------------------------------------------------------
        zvset:;
            if ((pw.partWk[r.di].lfoswi & 0x22) == 0)
                goto zv_out;
            r.dx = 0;
            r.ah = r.dl;
            if ((pw.partWk[r.di].lfoswi & 0x2) == 0)
                goto zv_nolfo1;
            r.dx = pw.partWk[r.di].lfodat;
        zv_nolfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x20) == 0)
                goto zv_nolfo2;
            r.dx += pw.partWk[r.di]._lfodat;
        zv_nolfo2:;
            if ((r.dx & 0x8000) != 0)
                goto zvlfo_minus;
            r.ax += r.dx;
            if (r.ah == 0)
                goto zv_out;
            r.al = 255;
            goto zv_out;
        zvlfo_minus:;
            r.carry = r.ax + r.dx > 0xffff;
            r.ax += r.dx;
            if (r.carry) goto zv_out;
            r.al = 0;

        //;------------------------------------------------------------------------------
        //;	出力
        //;------------------------------------------------------------------------------
        zv_out:;
            if (r.al == 0)
                goto zv_cut;
            r.dh = 0;
            r.dl = r.al;
            r.dx >>= 1;
            r.dx >>= 1;
            r.dx >>= 1;
            r.dx >>= 1;    //; dx = volume(0～15)
            r.ah = 0x07;
            r.al = pw.partb;
            ppz8em.SetVolume(r.al, r.dx);
            return;
        zv_cut:;
            r.ah = 0x02;
            r.al = pw.partb;
            ppz8em.StopPCM(r.al);// ; volume = 0... keyoff
            return;
        }



        //696-716
        //;==============================================================================
        //;	PPZ KEYON
        //;==============================================================================
        private void keyonz()
        {
            if (pw.partWk[r.di].onkai == 0xff) //-1
                goto keyonz_ret;

            //;;	xor dx, dx
            //;;	mov dl, fmpan[di]
            //;;	mov ah,13h
            //;;	mov al,[partb]
            //;;	call ppz8_call

            r.ah = 1;
            r.al = pw.partb;
            r.dl = pw.partWk[r.di].voicenum;
            r.dh = r.dl;
            r.dx &= 0x807f;//; dx=voicenum
            ppz8em.PlayPCM(r.al, r.dx);//; ppz keyon
        keyonz_ret:;
            return;
        }



        //717-731
        //;==============================================================================
        //;	ppz KEYOFF
        //;==============================================================================
        private void keyoffz()
        {
            if (pw.partWk[r.di].envf == 0xff)//-1
                goto kofz1_ext;
            if (pw.partWk[r.di].envf != 2)
            {
                pmd.keyoffp();
                return;
            }
            kofz_ret:;
            return;
        kofz1_ext:;
            if (pw.partWk[r.di].eenv_count == 4)
                goto kofz_ret;
            pmd.keyoffp();
            return;
        }



        //732-
//;==============================================================================
//;	PPZ OTODASI
//;==============================================================================
        private void otodasiz()
        {
            r.cx = pw.partWk[r.di].fnum;
            r.bx = pw.partWk[r.di].fnum2;//bx:cx=fnum
            r.ax = (ushort)(r.cx |r.bx);
            if (r.ax != 0)
                goto odz_00;
            return;
        odz_00:;
            //;
            //; Portament/LFO/Detune SET
            //;
            r.ax = pw.partWk[r.di].porta_num;
            if (r.ax == 0) goto odz_not_porta;
            int a = (short)r.ax;
            a += a;
            a += a;
            a += a;
            a += a;//;x16
            r.carry = (r.cx + (ushort)a) > 0xffff;
            r.cx += (ushort)a;
            r.bx += (ushort)((a >> 16) + (r.carry ? 1 : 0));
        odz_not_porta:;
            r.ax = 0;
            if ((pw.partWk[r.di].lfoswi & 0x11) == 0)
                goto odz_not_lfo;
            if ((pw.partWk[r.di].lfoswi & 0x1) == 0)
                goto odz_not_lfo1;
            r.ax += pw.partWk[r.si].lfodat;
            odz_not_lfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x10) == 0)
                goto odz_not_lfo;
            r.ax += pw.partWk[r.di]._lfodat;
        odz_not_lfo:;
            r.ax += pw.partWk[r.di].detune;
            r.dl = r.ch;
            r.dh = r.bl;
            a = (short)r.ax * (short)r.dx;
            r.dx = (ushort)(a >> 16);
            r.ax = (ushort)a;
            if ((r.dx & 0x8000) != 0)
                goto odz_minus;

            bool c = r.cx + r.ax > 0xffff;
            r.cx += r.ax;
            r.carry = (r.bx + r.dx + (c ? 1 : 0)) > 0xffff;
            r.bx += (ushort)(r.dx + (c ? 1 : 0));
            if (!r.carry) goto odz_main;
            r.cx = 0xffff;//-1
            r.bx = 0xffff;
            goto odz_main;
        odz_minus:;
            c = r.cx + r.ax > 0xffff;
            r.cx += r.ax;
            r.carry = (r.bx + r.dx + (c ? 1 : 0)) > 0xffff;
            r.bx += (ushort)(r.dx + (c ? 1 : 0));
            r.carry = r.bx + r.dx > 0xffff;
            if (r.carry) goto odz_main;
            r.cx = 0;
            r.bx = 0;
        //;
        //; TONE SET
        //;
        odz_main:;
            r.ah = 0x0b;
            r.al = pw.partb;
            r.dx = r.bx;
            ppz8em.SetFrequency(r.al, r.dx, r.cx);
        }



        //798-847
        //;==============================================================================
        //;	PPZ FNUM SET
        //;==============================================================================
        private void fnumsetz()
        {
            r.ah = r.al;
            r.ah &= 0xf;
            if (r.ah == 0xf)
            {
                fnrestz();//; 休符の場合
                return;
            }
            pw.partWk[r.di].onkai = r.al;
            r.bh = 0;
            r.bl = r.ah;//; bx=onkai
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al &= 0xf;
            r.cl = r.al;//; cl=octarb
            //r.bx += r.bx;
            r.ax = pw.ppz_tune_data[r.bx];//;o5標準
            r.dx = 0;
            r.cl -= 4;
            if ((r.cl & 0x80) == 0) goto ppz_over_o5;
            r.cl = (byte)-r.cl;
            r.ax = (ushort)(r.ax >> r.cl);
            goto ppz_fnumset;
        ppz_over_o5:;
            if (r.cl == 0) goto ppz_fnumset;
            r.ch = 0;
        ppz_over_o5_loop:;
            r.carry = (r.ax + r.ax) > 0xffff;
            r.ax += r.ax;
            r.dx += (ushort)(r.dx + (r.carry ? 1 : 0));
            r.cx--;
            if (r.cx != 0) goto ppz_over_o5_loop;
            ppz_fnumset:;
            pw.partWk[r.di].fnum = r.ax;
            pw.partWk[r.di].fnum2 = r.dx;
        }

        private void fnrestz()
        {
            pw.partWk[r.di].onkai = 0xff;
            if ((pw.partWk[r.di].lfoswi & 0x11) != 0)
                goto fnrz_ret;
            pw.partWk[r.di].fnum = 0;
            pw.partWk[r.di].fnum2 = 0;
        fnrz_ret:;
            return;
        }




    }
}