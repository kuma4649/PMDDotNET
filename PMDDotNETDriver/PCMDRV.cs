using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PCMDRV
    {
        private PMD pmd = null;
        private PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;
        private PPZDRV ppzdrv = null;

        public PCMDRV(PMD pmd, PW pw, x86Register r, Pc98 pc98,PPZDRV ppzdrv)
        {
            this.pmd = pmd;
            this.pw = pw;
            this.r = r;
            this.pc98 = pc98;
            this.ppzdrv = ppzdrv;

            SetupCmdtbl();
        }




        //;==============================================================================
        //;	ＰＣＭ音源 演奏 メイン
        //;==============================================================================
        //pcmmain_ret:
        //	ret

        public void pcmmain()
        {
            r.si = pw.partWk[r.di].address;//; si = PART DATA ADDRESS
            if (r.si == 0)
                return;// goto pcmmain_ret;

            Func<object> ret = null;
            if (pw.partWk[r.di].partmask != 0)
                ret = pcmmain_nonplay;
            else
                ret = pcmmain_c_1;

            if (ret != null)
            {
                do
                {
                    ret = (Func<object>)ret();
                } while (ret != null);
            }
        }

        private Func<object> pcmmain_c_1()
        {
            //; 音長 - 1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //; KEYOFF CHECK
            if ((pw.partWk[r.di].keyoff_flag & 3) != 0)//; 既にkeyoffしたか？
                goto mp0m;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                goto mp0m;

            pw.partWk[r.di].keyoff_flag = 0xff;//-1
            keyoffm();//; ALは壊さない

        mp0m:;//; LENGTH CHECK
            if (r.al != 0) return mpexitm;
            return mp1m0;
        }

        private Func<object> mp1m0()
        {
            pw.partWk[r.di].lfoswi &= 0xf7;//; Porta off
            return mp1m;
        }

        private Func<object> mp1m()//; DATA READ
        {
            do
            {
                r.al = (byte)pw.md[r.si++].dat;
                if (r.al < 0x80) goto mp2m;
                if (r.al == 0x80) goto mp15m;

                //; ELSE COMMANDS
                object o = commandsm();
                while (o != null && (Func<object>)o != mp1m)
                {
                    o = ((Func<object>)o)();
                    if ((Func<object>)o == pmd.mnp_ret)
                        return pmd.mnp_ret;
                    if ((Func<object>)o == porta_returnm)
                        return porta_returnm;
                }
            } while (true);

        //; END OF MUSIC['L' ｶﾞ ｱｯﾀﾄｷﾊ ｿｺﾍ ﾓﾄﾞﾙ]
        mp15m:;
            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) return mpexitm;

            //; 'L' ｶﾞ ｱｯﾀﾄｷ
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            return mp1m;

        mp2m:;//; F - NUMBER SET
            pmd.lfoinitp();
            pmd.oshift();
            fnumsetm();

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            pmd.calc_q();
            return porta_returnm;
        }

        private Func<object> porta_returnm()
        {
            if (pw.partWk[r.di].volpush == 0) goto mp_newm;
            if (pw.partWk[r.di].onkai == 0xff) goto mp_newm;
            pw.volpush_flag--;
            if (pw.volpush_flag == 0) goto mp_newm;
            pw.volpush_flag = 0;
            pw.partWk[r.di].volpush = 0;
        mp_newm:;
            volsetm();
            otodasim();
            if ((pw.partWk[r.di].keyoff_flag & 1) == 0)
                goto mp3m;
            keyonm();

        mp3m:;
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

        private Func<object> mpexitm()
        {
            r.cl = pw.partWk[r.di].lfoswi;
            r.al = r.cl;
            r.al &= 8;
            pw.lfo_switch = r.al;
            if (r.cl == 0)
                goto volsm;
            if ((r.cl & 3) == 0)
                goto not_lfom;

            pmd.lfo();
            if (!r.carry) goto not_lfom;
            r.al = r.cl;
            r.al &= 3;
            pw.lfo_switch |= r.al;
        not_lfom:;
            if ((r.cl & 0x30) == 0)
                goto not_lfom2;
            //pushf
            //cli
            pmd.lfo_change();
            pmd.lfo();
            if (!r.carry) goto not_lfom1;
            pmd.lfo_change();
            //popf
            r.al = pw.partWk[r.di].lfoswi;
            r.al &= 0x30;
            pw.lfo_switch |= r.al;
            goto not_lfom2;
        not_lfom1:;
            pmd.lfo_change();
        //popf
        not_lfom2:;
            if ((pw.lfo_switch & 0x19) == 0)
                goto volsm;
            if ((pw.lfo_switch & 8) == 0)
                goto not_portam;
            pmd.porta_calc();
        not_portam:;
            otodasim();
        volsm:;
            pmd.soft_env();
            if (r.carry) goto volsm2;
            if ((pw.lfo_switch & 0x22) != 0)
                goto volsm2;
            if (pw.fadeout_speed == 0)
                return pmd.mnp_ret;
            volsm2:;
            volsetm();
            return pmd.mnp_ret;
        }



        //139-181
        //;==============================================================================
        //;	ＰＣＭ音源演奏メイン：パートマスクされている時
        //;==============================================================================
        private Func<object> pcmmain_nonplay()
        {
            pw.partWk[r.di].keyoff_flag = 0xff;// -1
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return pmd.mnp_ret;

            if ((pw.partWk[r.di].partmask & 2) == 0)//; bit1(pcm効果音中？)をcheck
                return pcmmnp_1;
            r.dx = (ushort)pw.fm2_port1;
            r.al = pc98.InPort(r.dx);
            if ((r.al & 0b0000_0100) == 0)//;EOS check
                return pcmmnp_1;//; まだ割り込みPCMが鳴っている
            pw.pcmflag = 0;//; PCM効果音終了
            pw.pcm_effec_num = 255;
            pw.partWk[r.di].partmask &= 0xfd;//;bit1をclear
            if (pw.partWk[r.di].partmask == 0)
                return mp1m0;//;partmaskが0なら復活させる
            return pcmmnp_1;
        }

        private Func<object> pcmmnp_1()
        {
            do
            {
                do
                {
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;
                    if (r.al < 0x80) return pmd.fmmnp_3;

                    object o = commandsm();
                    while (o != null && (Func<object>)o != pcmmnp_1)
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



        //182-
        //;==============================================================================
        //;	ＰＣＭ音源特殊コマンド処理
        //;==============================================================================

        private Func<object> commandsm()
        {
            pw.currentCommandTable = cmdtblm;
            r.bx = 0;//offset cmdtblp
            return pmd.command00();
        }

        private Func<object>[] cmdtblm;
        private void SetupCmdtbl()
        {
            cmdtblm = new Func<object>[] {
                  comAtm                    //0xff(0)
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
	            , comvolupm                 //0xf4(11)
	            , comvoldownm               //0xf3(12)
	            , pmd.lfoset                //0xf2(13)
	            , pmd.lfoswitch             //0xf1(14)
	            , pmd.psgenvset             //0xf0(15)
	            , pmd.comy                  //0xef(16)
	            , pmd.jump1                 //0xee(17)
	            , pmd.jump1                 //0xed(18)
	            //                  
	            , pansetm                   //0xec(19)
	            , pmd.rhykey                //0xeb(20)
	            , pmd.rhyvs                 //0xea(21)
	            , pmd.rpnset                //0xe9(22)
	            , pmd.rmsvs                 //0xe8(23)
	            //                  
	            , pmd.comshift2             //0xe7(24)
	            , pmd.rmsvs_sft             //0xe6(25)
	            , pmd.rhyvs_sft             //0xe5(26)
	            //                  
	            , pmd.jump1                 //0xe4(27)
	            //Ｖ２．３　ＥＸＴＥＮＤ
	            , comvolupm2                //0xe3(28)
	            , comvoldownm2              //0xe2(29)
	            //                  
	            , pmd.jump1                 //0xe1(30)
	            , pmd.jump1                 //0xe0(31)
	            //                  
	            , pmd.syousetu_lng_set	//0DFH(32)
	            //                  
	            , pmd.vol_one_up_pcm	//0deH(33)
	            , pmd.vol_one_down		//0DDH(34)
	            //
	            , pmd.status_write		//0DCH(35)
                , pmd.status_add		//0DBH(36)
	            //                  
	            , portam        			//0DAH(37)
	            //                  
	            , pmd.jump1 				//0D9H(38)
	            , pmd.jump1	    			//0D8H(39)
	            , pmd.jump1		    		//0D7H(40)
	            //                  
	            , pmd.mdepth_set	    	//0D6H(41)
	            //                  
	            , pmd.comdd				    //0d5h(42)
	            //                  
	            , pmd.ssg_efct_set		    //0d4h(43)
	            , pmd.fm_efct_set		    //0d3h(44)
	            , pmd.fade_set			    //0d2h(45)
	            //                  
	            , pmd.jump1                 //0xd1(46)
	            , pmd.jump1	    			//0d0h(47)
	            //                  
	            , pmd.jump1 				//0cfh(48)
	            , pcmrepeat_set     		//0ceh(49)
	            , pmd.extend_psgenvset	    //0cdh(50)
	            , pmd.jump1	       			//0cch(51)
	            , pmd.lfowave_set		    //0cbh(52)
	            , pmd.lfo_extend		    //0cah(53)
	            , pmd.envelope_extend	    //0c9h(54)
	            , pmd.jump3 				//0c8h(55)
	            , pmd.jump3	    			//0c7h(56)
	            , pmd.jump6		    		//0c6h(57)
	            , pmd.jump1			    	//0c5h(58)
	            , pmd.comq2				    //0c4h(59)
	            , pansetm_ex	        	//0c3h(60)
	            , pmd.lfoset_delay		    //0c2h(61)
	            , pmd.jump0				    //0c1h,sular(62)
	            , pcm_mml_part_mask     	//0c0h(63)
	            , pmd._lfoset			    //0bfh(64)
	            , pmd._lfoswitch		    //0beh(65)
	            , pmd._mdepth_set		    //0bdh(66)
	            , pmd._lfowave_set		    //0bch(67)
	            , pmd._lfo_extend		    //0bbh(68)
	            , pmd._volmask_set		    //0bah(69)
	            , pmd._lfoset_delay		    //0b9h(70)
	            , pmd.jump2                 //0xb8(71)
	            , pmd.mdepth_count		    //0b7h(72)
	            , pmd.jump1				    //0xb6(73)
	            , pmd.jump2				    //0xb5(74)
	            , pmd.jump16			    //0b4h(75)
	            , pmd.comq3				    //0b3h(76)
	            , pmd.comshift_master	    //0b2h(77)
	            , pmd.comq4				    //0b1h(78)
            };

            if (pw.ppz != 0) cmdtblm[75] = ppzdrv.ppz_extpartset;//0b4h in ppzdrv.asm(75)
        }



        //288-313
        //;==============================================================================
        //;	演奏中パートのマスクon/off
        //;==============================================================================
        private Func<object> pcm_mml_part_mask()
        {
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, "pcm_mml_part_mask");
#endif

            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 2)
                return pmd.special_0c0h;

            if (r.al == 0)
                goto pcm_part_maskoff_ret;

            pw.partWk[r.di].partmask |= 0x40;
            if (pw.partWk[r.di].partmask != 0x40)
                goto pmpm_ret;

            r.dx = 0x0102;//; PAN=0 / x8 bit mode
            pmd.opnset46();
            r.dx = 0x0001;//; PCM RESET
            pmd.opnset46();

        pmpm_ret:;
            //r.ax = r.stack.Pop();//; commandsm
            return pcmmnp_1;

        pcm_part_maskoff_ret:;
            pw.partWk[r.di].partmask &= 0xbf;
            if (pw.partWk[r.di].partmask != 0)
                goto pmpm_ret;
            //r.ax = r.stack.Pop();//; commandsm
            return mp1m;//;パート復活
        }



        //314-351
        //;==============================================================================
        //;	リピート設定
        //;==============================================================================
        private Func<object> pcmrepeat_set()
        {
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if ((r.ax & 0x8000) != 0)
                goto prs1_minus;
            r.ax += (ushort)pw.pcmstart;
            goto prs1_set;
        prs1_minus:;
            r.ax += (ushort)pw.pcmstop;
        prs1_set:;
            pw.pcmrepeat1 = r.ax;
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if (r.ax == 0)
                goto prs2_minus;
            if ((r.ax&0x8000)!= 0)
                goto prs2_minus;
            r.ax += (ushort)pw.pcmstart;
            goto prs2_set;
        prs2_minus:;
            r.ax += (ushort)pw.pcmstop;
        prs2_set:;
            pw.pcmrepeat2 = r.ax;
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;
            if (r.ax == 0x8000)
                goto prs3_set;
            if (r.ax >= 0x8000)
                goto prs3_minus;
            r.ax += (ushort)pw.pcmstart;
            goto prs3_set;

        prs3_minus:;
            r.ax += (ushort)pw.pcmstop;

        prs3_set:;
            pw.pcmrelease = r.ax;

            return null;
        }



        //352-397
        //;==============================================================================
        //;	ポルタメント(PCM)
        //;==============================================================================
        private Func<object> portam()
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
            fnumsetm();
            r.ax = pw.partWk[r.di].fnum;
            r.stack.Push(r.ax);
            r.al = pw.partWk[r.di].onkai;
            r.stack.Push(r.ax);
            r.al = (byte)pw.md[r.si++].dat;
            pmd.oshift();
            fnumsetm();
            r.ax = pw.partWk[r.di].fnum;//; ax = ポルタメント先のdelta_n値
            r.bx = r.stack.Pop();
            pw.partWk[r.di].onkai = r.bl;
            r.bx = r.stack.Pop();//; bx = ポルタメント元のdelta_n値
            pw.partWk[r.di].fnum = r.bx;
            r.ax -= r.bx;//; ax = delta_n差
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
            return porta_returnm;
        }



        //398-409
        //;
        //;	COMMAND ']' [VOLUME UP]
        //;
        public Func<object> comvolupm()
        {
            r.al = pw.partWk[r.di].volume;
            r.carry = (r.al + 16) > 0xff;
            r.al += 16;
            return vupckm();
        }

        private Func<object> vupckm()
        {
            if (r.carry) r.al = 255;
            return vsetm();
        }

        private Func<object> vsetm()
        {
            pw.partWk[r.di].volume = r.al;

            //IDE向け
            ChipDatum cd = new ChipDatum(-1, -1, -1);
            MmlDatum md = new MmlDatum(-1, enmMMLType.Volume, pw.cmd.linePos, (int)r.al);
            cd.addtionalData = md;
            pmd.WriteDummy(cd);

            return null;
        }

        //; Ｖ２．３　ＥＸＴＥＮＤ
        public Func<object> comvolupm2()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.carry = (r.al + pw.partWk[r.di].volume) > 0xff;
            r.al += pw.partWk[r.di].volume;
            return vupckm();
        }



        //415-433
        //;
        //;	COMMAND '[' [VOLUME DOWN]
        //;
        public Func<object> comvoldownm()
        {
            r.al = pw.partWk[r.di].volume;
            r.carry = r.al - 16 < 0;
            r.al -= 16;
            if (r.carry) r.al = 0;
            return vsetm;
        }

        //; Ｖ２．３　ＥＸＴＥＮＤ
        public Func<object> comvoldownm2()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.ah = r.al;
            r.al = pw.partWk[r.di].volume;
            r.carry = r.al - r.ah < 0;
            r.al -= r.ah;
            if (r.carry) r.al = 0;
            return vsetm;
        }



        //434-445
        //;==============================================================================
        //;	COMMAND 'p' [Panning Set]
        //;==============================================================================
        private Func<object> pansetm()
        {
            r.al = (byte)pw.md[r.si++].dat;
            return pansetm_main;
        }

        private Func<object> pansetm_main()
        { 
            r.al = r.ror(r.al, 1);
            r.al = r.ror(r.al, 1);
            r.al &= 0b1100_0000;
            pw.partWk[r.di].fmpan = r.al;
            return null;
        }



        //446-463
        //;==============================================================================
        //;	Pan setting Extend
        //;==============================================================================
        private Func<object> pansetm_ex()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.si++;//;逆走flagは読み飛ばす
            if (r.al == 0)
                goto pmex_mid;
            if ((r.al&0x80)!= 0)
                goto pmex_left;
            r.al = 2;
            return pansetm_main;
        pmex_mid:;
            r.al = 3;
            return pansetm_main;
        pmex_left:;
            r.al = 3;
            return pansetm_main;
        }



        //464-485
        //;
        //;	COMMAND '@' [NEIRO Change]
        //;
        private Func<object> comAtm()
        {
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].voicenum = r.al;
            r.ah = 0;
            r.ax += r.ax;
            r.ax += r.ax;

            r.bx = 0;//offset pcmadrs
            r.bx += r.ax;
            r.ax = (ushort)(pw.pcmWk[r.bx] + pw.pcmWk[r.bx+1]*0x100);// pw.pcmadrs[r.bx];
            r.bx++;
            r.bx++;
            pw.pcmstart = r.ax;
            r.ax = (ushort)(pw.pcmWk[r.bx] + pw.pcmWk[r.bx + 1] * 0x100);//pw.pcmadrs[r.bx];
            pw.pcmstop = r.ax;
            pw.pcmrepeat1 = 0;
            pw.pcmrepeat2 = 0;
            pw.pcmrelease = 0x8000;

            return null;
        }



        //486-602
        //;==============================================================================
        //;	PCM VOLUME SET
        //;==============================================================================
        private void volsetm()
        {
            r.al = pw.partWk[r.di].volpush;
            if (r.al != 0)
                goto vsm_01;
            r.al = pw.partWk[r.di].volume;
        vsm_01:;
            r.dl = r.al;
            //;------------------------------------------------------------------------------
            //;	音量down計算
            //;------------------------------------------------------------------------------
            r.al = pw.pcm_voldown;
            if (r.al == 0)
                goto pcm_fade_calc;
            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	Fadeout計算
        //;------------------------------------------------------------------------------
        pcm_fade_calc:;
            r.al = pw.fadeout_volume;
            if (r.al == 0)
                goto pcm_env_calc;
            r.al = (byte)-r.al;
            r.ax = (ushort)(r.al * r.al);//; al=al^2
            r.al = r.ah;
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	ENVELOPE 計算
        //;------------------------------------------------------------------------------
        pcm_env_calc:;
            r.al = r.dl;
            if (r.al == 0)//; 音量0?
                goto mv_out;
            if (pw.partWk[r.di].envf != 0xff)//-1
                goto normal_mvset;
            //; 拡張版 音量 = al * (eenv_vol + 1) / 16
            r.dl = pw.partWk[r.di].eenv_volume;
            if (r.dl == 0)
                goto mv_min;
            r.dl++;
            r.ax = (ushort)(r.al * r.dl);
            r.ax >>= 3;
            r.carry = ((r.ax % 2) != 0);
            r.ax >>= 1;
            if (!r.carry) goto mvset;
            r.ax++;
            goto mvset;
        normal_mvset:;
            r.ah = pw.partWk[r.di].eenv_volume;//.penv;
            if ((r.ah & 0x80) == 0)
                goto mvplus;
            //; -
            r.ah = (byte)-r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.carry = r.al - r.ah < 0;
            r.al -= r.ah;
            if (!r.carry) goto mvset;
            mv_min:;
            r.al = 0;
            goto mv_out;
            //; +
            mvplus:;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.ah += r.ah;
            r.carry = r.al + r.ah > 0xff;
            r.al += r.ah;
            if (!r.carry) goto mvset;
            r.al = 255;
        //;------------------------------------------------------------------------------
        //;	音量LFO計算
        //;------------------------------------------------------------------------------
        mvset:;
            if ((pw.partWk[r.di].lfoswi & 0x22) == 0)
                goto mv_out;
            r.dx = 0;
            r.ah = r.dl;
            if ((pw.partWk[r.di].lfoswi & 0x2) == 0)
                goto mv_nolfo1;
            r.dx = pw.partWk[r.di].lfodat;
        mv_nolfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x20) == 0)
                goto mv_nolfo2;
            r.dx += pw.partWk[r.di]._lfodat;
        mv_nolfo2:;
            if ((r.dx & 0x8000) != 0)
                goto mvlfo_minus;
            r.ax += r.dx;
            if (r.ah == 0)
                goto mv_out;
            r.al = 255;
            goto mv_out;
        mvlfo_minus:;
            r.carry = r.ax + r.dx > 0xffff;
            r.ax += r.dx;
            if (r.carry) goto mv_out;
            r.al = 0;

        //;------------------------------------------------------------------------------
        //;	出力
        //;------------------------------------------------------------------------------
        mv_out:;
            r.dl = r.al;
            r.dh = 0x0b;
            pmd.opnset46();
        }



        //603-672
        //;==============================================================================
        //;	PCM KEYON
        //;==============================================================================
        private void keyonm()
        {
            if (pw.partWk[r.di].onkai != 0xff) //-1
                goto keyonm_00;
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        keyonm_00:;
            r.dx = 0x0102;//; PAN=0 / x8 bit mode
            pmd.opnset46();
            r.dx = 0x0021;//; PCM RESET
            pmd.opnset46();
            r.bx = (ushort)pw.pcmstart;
            r.dh = 2;
            r.dl = r.bl;
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
            r.bx = (ushort)pw.pcmstop;
            r.dh++;
            r.dl = r.bl;
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
            r.ax = pw.pcmrepeat1;
            r.ax |= pw.pcmrepeat2;
            if (r.ax != 0) goto pcm_repeat_keyon;
            r.dx = 0x00a0;//;PCM PLAY(non_repeat)
            pmd.opnset46();
            r.dl = pw.partWk[r.di].fmpan;//; PAN SET
            r.dl |= 2;//; x8 bit mode
            r.dh = 1;
            pmd.opnset46();
            return;
        pcm_repeat_keyon:;
            r.dx = 0x00b0;//;PCM PLAY(repeat)
            pmd.opnset46();
            r.dl = pw.partWk[r.di].fmpan;//; PAN SET
            r.dl |= 2;//; x8 bit mode
            r.dh = 1;
            pmd.opnset46();
            r.bx = pw.pcmrepeat1;//; REPEAT ADDRESS set 1
            r.dh = 2;
            r.dl = r.bl;
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
            r.bx = pw.pcmrepeat2;//; REPEAT ADDRESS set 2
            r.dh++;
            r.dl = r.bl;
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
        }



        //673-714
        //;
        //;	PCM KEYOFF
        //;
        private void keyoffm()
        {
            if (pw.partWk[r.di].envf == 0xff)//-1
                goto kofm1_ext;
            if (pw.partWk[r.di].envf !=2)
                goto keyoffm_main;
            kofm_ret:;
            return;
        kofm1_ext:;
            if (pw.partWk[r.di].eenv_count == 4)
                goto kofm_ret;
            keyoffm_main:;
            if (pw.pcmrelease == 0x8000)
            {
                keyoffp();
                return;
            }
            r.dx = 0x0021;//; PCM RESET
            pmd.opnset46();
            r.bx = pw.pcmrelease;
            r.dh = 2;
            r.dl = r.bl;
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
            r.bx = (ushort)pw.pcmstop;//; Stop ADDRESS for Release
            r.dh++;
            r.dl = r.bl;
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
            r.dx = 0x00a0;//;PCM PLAY(non_repeat)
            pmd.opnset46();
            keyoffp();
        }

        public void keyoffp()
        {
            if (pw.partWk[r.di].onkai != 0xff)
            {
                kofp1();
                return;
            }
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        }

        private void kofp1()
        {
            if (pw.partWk[r.di].envf == 0xff)
                goto kofp1_ext;
            pw.partWk[r.di].envf = 2;
            return;

        kofp1_ext:;
            pw.partWk[r.di].eenv_count = 4;
            return;
        }



        //715-767
        //;
        //;	PCM OTODASI
        //;
        private void otodasim()
        {
            r.bx = pw.partWk[r.di].fnum;
            if (r.bx != 0)
                goto odm_00;
            return;
        odm_00:;
            //;
            //; Portament/LFO/Detune SET
            //;
            r.bx = (ushort)((short)r.bx + (short)pw.partWk[r.di].porta_num);
            r.dx = 0;
            if ((pw.partWk[r.di].lfoswi & 0x11) == 0)
                goto odm_not_lfo;
            if ((pw.partWk[r.di].lfoswi & 0x1) == 0)
                goto odm_not_lfo1;
            r.dx = pw.partWk[r.di].lfodat;
        odm_not_lfo1:;
            if ((pw.partWk[r.di].lfoswi & 0x10) == 0)
                goto odm_not_lfo2;
            r.dx += pw.partWk[r.di]._lfodat;
        odm_not_lfo2:;
            r.dx += r.dx;// ; PCM ﾊ LFO ｶﾞ ｶｶﾘﾆｸｲ ﾉﾃﾞ depth ｦ 4ﾊﾞｲ ｽﾙ
            r.dx += r.dx;
        odm_not_lfo:;
            r.dx += pw.partWk[r.di].detune;
            if ((r.dx & 0x8000) != 0)
                goto odm_minus;
            r.carry = r.bx + r.dx > 0xffff;
            r.bx += r.dx;
            if (!r.carry) goto odm_main;
            r.bx = 0xffff;//-1
            goto odm_main;
        odm_minus:;
            r.carry = r.bx + r.dx > 0xffff;
            r.bx += r.dx;
            if (r.carry) goto odm_main;
            r.bx = 0;
        odm_main:;
            //;
            //; TONE SET
            //;
            r.dh = 9;
            r.dl = r.bl;
            //pushf
            //cli
            pmd.opnset46();
            r.dh++;
            r.dl = r.bh;
            pmd.opnset46();
            //popf
        }



        //768-813
        //;
        //;	PCM FNUM SET
        //;
        private void fnumsetm()
        {
            r.ah = r.al;
            r.ah &= 0xf;
            if (r.ah == 0xf)
            {
                fnrest();//; 休符の場合
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
            r.ch = r.al;
            r.al = 5;
            r.carry = r.al - r.cl < 0;
            r.al -= r.cl;
            if (!r.carry) goto fnm00;
            r.al = 0;
        fnm00:;
            r.cl = r.al;//; cl=5-octarb
            //r.bx += r.bx;
            r.ax = pw.pcm_tune_data[r.bx];
            if (r.ch < 6)//;o7以上?
                goto pts01m;
            r.ch = 0x50;
            if ((r.ax & 0x8000) != 0)
                goto pts00m;
            r.ax += r.ax;//;o7以上で2倍できる場合は2倍
            r.ch = 0x60;
        pts00m:;
            pw.partWk[r.di].onkai &= 0x0f;
            pw.partWk[r.di].onkai |= r.ch;//; onkai値修正
            goto fnm01;
        pts01m:;
            r.ax = (ushort)(r.ax >> r.cl);//; ax=ax/[2^OCTARB]
        fnm01:;
            pw.partWk[r.di].fnum = r.ax;
        }

        private void fnrest()
        {
            pw.partWk[r.di].onkai = 0xff;
            if ((pw.partWk[r.di].lfoswi & 0x11) != 0)
                goto fnr_ret;
            pw.partWk[r.di].fnum = 0;//;音程LFO未使用
        fnr_ret:;
            return;
        }



    }
}