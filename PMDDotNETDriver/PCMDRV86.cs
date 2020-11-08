using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml;

namespace PMDDotNET.Driver
{
    public class PCMDRV86
    {
        private PMD pmd = null;
        private PW pw = null;
        private x86Register r = null;
        private Pc98 pc98 = null;
        private Func<ChipDatum,int> p86drv = null;
        private Action[] trans_table;
        private byte[][] pcmData;



        public PCMDRV86(PMD pmd, PW pw, x86Register r, Pc98 pc98, Func<ChipDatum, int> p86drv,byte[][] pcmData)
        {
            this.pmd = pmd;
            this.pw = pw;
            this.r = r;
            this.pc98 = pc98;
            this.p86drv = p86drv;
            this.pcmData = pcmData;

            SetupCmdtbl();
        }





        //1-130
        //;==============================================================================
        //;	ＰＣＭ音源 演奏 メイン(86B PCM)
        //;==============================================================================
        //pcmmain_ret:
        //	ret

        public void pcmmain()
        {
            r.si = pw.partWk[r.di].address;//; si = PART DATA ADDRESS
            if (r.si == 0)
                return;

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
            //; 音長 -1
            pw.partWk[r.di].leng--;
            r.al = pw.partWk[r.di].leng;

            //	; KEYOFF CHECK
            if ((pw.partWk[r.di].keyoff_flag & 3) != 0)//; 既にkeyoffしたか？
                goto mp0m;

            if (r.al > pw.partWk[r.di].qdat)//; Q値 => 残りLength値時 keyoff
                goto mp0m;

            mp00m:;
            keyoffm();//; ALは壊さない
            pw.partWk[r.di].keyoff_flag = 0xff;//-1

        mp0m:;//; LENGTH CHECK
            if (r.al != 0) return mpexitm;
            return mp1m0;
        }

        private Func<object> mp1m0()
        {
            return mp1m;
        }

        private Func<object> mp1m()//; DATA READ
        {
            do
            {
                pw.cmd = pw.md[r.si];
                r.al = (byte)pw.md[r.si].dat;

                if (r.si == pw.jumpIndex)
                    pw.jumpIndex = -1;//KUMA:Added

                r.si++;
                if (r.al < 0x80) goto mp2m;
                if (r.al == 0x80) goto mp15m;

                //; ELSE COMMANDS
                object o = commandsm();
                while (o != null && (Func<object>)o != mp1m)
                {
                    o = ((Func<object>)o)();
                    if ((Func<object>)o == pmd.mnp_ret)
                        return pmd.mnp_ret;
                    //if ((Func<object>)o == porta_returnm)
                    //return porta_returnm;
                }
            } while (true);

        //; END OF MUSIC['L' ｶﾞ ｱｯﾀﾄｷﾊ ｿｺﾍ ﾓﾄﾞﾙ]
        mp15m:;

            pmd.FlashMacroList();

            r.si--;
            pw.partWk[r.di].address = r.si;//mov[di],si
            pw.partWk[r.di].loopcheck = 3;
            pw.partWk[r.di].onkai = 0xff;//-1
            r.bx = pw.partWk[r.di].partloop;
            if (r.bx == 0) return mpexitm;

            //; 'L' ｶﾞ ｱｯﾀﾄｷ
            r.si = r.bx;
            pw.partWk[r.di].loopcheck = 1;
            pw.partWk[r.di].loopCounter++;
            return mp1m;

        mp2m:;//; F - NUMBER SET
            pmd.FlashMacroList();
            pmd.lfoinitp();
            pmd.oshift();
            fnumsetm();

            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].leng = r.al;
            pmd.calc_q();


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
            if ((r.cl & 0x22) == 0)
                goto not_lfo3m;

            pw.lfo_switch = 0;
            if ((r.cl & 2) == 0)
                goto not_lfom;

            pmd.lfo();
            r.al = r.cl;
            r.al &= 2;
            pw.lfo_switch = r.al;
        not_lfom:;
            if ((r.cl & 0x20) == 0)
                goto not_lfo2m;
            //pushf
            //cli
            pmd.lfo_change();
            pmd.lfo();
            if (!r.carry) goto not_lfo1m;
            pmd.lfo_change();
            //	popf
            r.al = pw.partWk[r.di].lfoswi;
            r.al &= 0x20;
            pw.lfo_switch |= r.al;
            goto not_lfo2m;
        not_lfo1m:;
            pmd.lfo_change();
        //	popf
        not_lfo2m:;
            pmd.soft_env();
            if (r.carry) goto volsm2;
            if ((pw.lfo_switch & 0x22) != 0)
                goto volsm2;
            volsm1:;
            if (pw.fadeout_speed == 0)
                return pmd.mnp_ret;
            volsm2:;
            volsetm();
            return pmd.mnp_ret;
        not_lfo3m:;
            pmd.soft_env();
            if (r.carry) goto volsm2;
            goto volsm1;
        }



        //131-176
        //;==============================================================================
        //;	ＰＣＭ音源演奏メイン：パートマスクされている時
        //;==============================================================================
        private Func<object> pcmmain_nonplay()
        {
            pw.partWk[r.di].leng--;
            if (pw.partWk[r.di].leng != 0) return pmd.mnp_ret;

            if ((pw.partWk[r.di].partmask & 2) == 0)//; bit1(pcm効果音中？)をcheck
                return pcmmnp_1;

            if (pw.play86_flag == 1)
                return pcmmnp_1;//; まだ割り込みPCMが鳴っている
            pw.pcmflag = 0;//; PCM効果音終了
            pw.pcm_effec_num = 255;
            pw.partWk[r.di].partmask &= 0xfd;//;bit1をclear
            if (pw.partWk[r.di].partmask != 0)
                return pcmmnp_1;

            r.al = pw.partWk[r.di].voicenum;
            neiro_set();
            r.al = pw.partWk[r.di].fmpan;
            r.ah = pw.revpan;
            set_pcm_pan();

            return mp1m0;//; partmaskが0なら復活させる
        }

        private Func<object> pcmmnp_1()
        {
            do
            {
                do
                {
                    pw.cmd = pw.md[r.si];
                    r.al = (byte)pw.md[r.si++].dat;
                    if (r.al == 0x80) break;//KUMA: 未チェック(TAG050で　==になおした)
                    if (r.al < 0x80) return pmd.fmmnp_3;

                    object o = commandsm();
                    while (o != null && (Func<object>)o != pcmmnp_1)
                    {
                        o = ((Func<object>)o)();
                        if ((Func<object>)o == pmd.mnp_ret)
                            return pmd.mnp_ret;
                    }
                } while (true);

                pmd.FlashMacroList();

                //	; END OF MUSIC["L"があった時はそこに戻る]
                r.si--;
                pw.partWk[r.di].address = r.si;
                pw.partWk[r.di].loopcheck = 3;
                pw.partWk[r.di].onkai = 0xff;//-1
                r.bx = pw.partWk[r.di].partloop;

                if ((r.bx & r.bx) == 0) return pmd.fmmnp_4;

                //    ; "L"があった時
                r.si = r.bx;
                pw.partWk[r.di].loopcheck = 1;
                pw.partWk[r.di].loopCounter++;
            } while (true);
        }



        //177-
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
	            , pmd.jump1        			//0DAH(37)
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
	            , pmd.jump4 			    //0bfh(64)
	            , pmd.jump1     		    //0beh(65)
	            , pmd.jump2     		    //0bdh(66)
	            , pmd.jump1		            //0bch(67)
	            , pmd.jump1 		        //0bbh(68)
	            , pmd.jump1	        	    //0bah(69)
	            , pmd.jump1 	    	    //0b9h(70)
	            , pmd.jump2                 //0xb8(71)
	            , pmd.mdepth_count		    //0b7h(72)
	            , pmd.jump1				    //0xb6(73)
	            , pmd.jump2				    //0xb5(74)
	            , pmd.jump16			    //0b4h(75)
	            , pmd.comq3				    //0b3h(76)
	            , pmd.comshift_master	    //0b2h(77)
	            //, pmd.comq4				    //0b1h(78)
            };

            trans_table = new Action[]{
                double_trans
                ,left_trans
                ,right_trans
                ,double_trans
                ,double_trans_g
                ,left_trans_g
                ,right_trans_g
                ,double_trans_g
            };

        }



        //278-300
        //;==============================================================================
        //;	演奏中パートのマスクon/off
        //;==============================================================================
        private Func<object> pcm_mml_part_mask()
        {
            r.al = (byte)pw.md[r.si++].dat;
            if (r.al >= 2)
                return pmd.special_0c0h;

            if (r.al == 0)
                goto pcm_part_maskoff_ret;

            pw.partWk[r.di].partmask |= 0x40;
            if (pw.partWk[r.di].partmask != 0x40)
                goto pmpm_ret;

            stop_86pcm();

        pmpm_ret:;
            //    pop ax; commandsm
            return pcmmnp_1;

        pcm_part_maskoff_ret:;
            pw.partWk[r.di].partmask &= 0xbf;
            if (pw.partWk[r.di].partmask != 0)
                goto pmpm_ret;
            //    pop ax		;commandsm
            return mp1m;//;パート復活
        }



        //301-408
        //;==============================================================================
        //;	リピート設定
        //;==============================================================================
        private Func<object> pcmrepeat_set()
        {
            r.ax = pw._start_ofs;
            pw.repeat_ofs = r.ax;
            r.ax = pw._start_ofs2;
            pw.repeat_ofs2 = r.ax;//;repeat開始位置 = start位置に設定
            r.dx = pw._size1;
            pw.repeat_size1 = r.dx;
            r.cx = pw._size2;//;cx:dx=全体size
            pw.repeat_size2 = r.cx;//; repeat_size = 今のsizeに設定
            pw.repeat_flag = 1;

            pw.release_flag1 = 0;

            r.stack.Push(r.dx);//; サイズを保存
            r.stack.Push(r.cx);//;

            //;	一個目 = リピート開始位置
            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;

            if ((r.ax & 0x8000) != 0)
                goto prs1_minus;

            //; 正の場合
            pcm86vol_chk();
            int a = pw.repeat_size2 * 0x10000 + pw.repeat_size1;
            a -= r.ax;//; リピートサイズ＝全体のサイズ-指定値
            pw.repeat_size1 = (ushort)a;
            pw.repeat_size2 = (ushort)(a >> 16);

            a = pw.repeat_ofs2 * 0x10000 + pw.repeat_ofs;
            a += r.ax;//;リピート開始位置から指定値を加算
            pw.repeat_ofs = (ushort)a;
            pw.repeat_ofs2 = (ushort)(a >> 16);

            goto prs2_set;

        //; 負の場合
        prs1_minus:;
            r.ax = (ushort)(-r.ax);
            pcm86vol_chk();

            pw.repeat_size1 = r.ax;//;リピートサイズ＝neg(指定値)
            pw.repeat_size2 = 0;

            a = r.cx * 0x10000 + r.dx;
            a -= r.ax;
            r.dx = (ushort)a;
            r.cx = (ushort)(a >> 16);

            a = pw.repeat_ofs2 * 0x10000 + pw.repeat_ofs;
            a += r.dx;//;リピート開始位置に
            a += r.cx * 0x10000;//; (全体サイズ-指定値)を加算
            pw.repeat_ofs = (ushort)a;
            pw.repeat_ofs2 = (ushort)(a >> 16);

        //;	２個目 = リピート終了位置
        prs2_set:;

            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;

            if (r.ax == 0)
                goto prs3_set;//	;0なら計算しない
            if ((r.ax & 0x8000) != 0)
                goto prs2_minus;

            //;正の場合
            pcm86vol_chk();
            pw._size1 = r.ax;// ; 正ならpcmサイズ＝指定値
            pw._size2 = 0;

            a = r.cx * 0x10000 + r.dx;
            a -= r.ax;//; リピートサイズから(旧サイズ-新サイズ)を引く
            r.dx = (ushort)a;
            r.cx = (ushort)(a >> 16);

            a = pw.repeat_size2 * 0x10000 + pw.repeat_size1;
            a -= r.ax;//; リピートサイズ＝全体のサイズ-指定値
            a -= r.cx * 0x10000;
            pw.repeat_size1 = (ushort)a;
            pw.repeat_size2 = (ushort)(a >> 16);

            goto prs3_set;

        //; 負の場合
        prs2_minus:;
            r.ax = (ushort)(-r.ax);
            pcm86vol_chk();

            a = pw.repeat_size2 * 0x10000 + pw.repeat_size1;
            a -= r.ax;//; リピートサイズから
            //; neg(指定値)を引く
            pw.repeat_size1 = (ushort)a;
            pw.repeat_size2 = (ushort)(a >> 16);

            a = pw._size2 * 0x10000 + pw._size1;
            a -= r.ax;//; 本来のサイズから指定値を引く

        //;	３個目 = リリース開始位置
        prs3_set:;
            r.cx = r.stack.Pop();
            r.dx = r.stack.Pop();//; cx:dx=全体サイズ復帰

            r.ax = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
            r.si += 2;

            if (r.ax == 0x8000)
                goto prs_exit;//;8000Hなら設定しない
            r.carry = r.ax < 0x8000;

            r.bx = pw._start_ofs;
            pw.release_ofs = r.bx;
            r.bx = pw._start_ofs2;
            pw.release_ofs2 = r.bx;//;release開始位置 = start位置に設定
            pw.release_size1 = r.dx;
            pw.release_size2 = r.cx;//;release_size = 今のsizeに設定
            pw.release_flag1 = 1;//; リリースするに設定
            if (!r.carry) goto prs3_minus;

            //;正の場合
            pcm86vol_chk();
            //; リリースサイズ＝全体のサイズ-指定値
            a = pw.release_size2 * 0x10000 + pw.release_size1;
            a -= r.ax;
            pw.release_size1 = (ushort)a;
            pw.release_size2 = (ushort)(a >> 16);

            //;リリース開始位置から指定値を加算
            a = pw.release_ofs2 * 0x10000 + pw.release_ofs;
            a += r.ax;
            pw.release_ofs = (ushort)a;
            pw.release_ofs2 = (ushort)(a >> 16);

            goto prs_exit;

        //; 負の場合
        prs3_minus:;
            r.ax = (ushort)(-r.ax);
            pcm86vol_chk();
            pw.release_size1 = r.ax;//;リリースサイズ＝neg(指定値)
            pw.release_size2 = 0;

            a = r.cx * 0x10000 + r.dx;
            a -= r.ax;
            r.dx = (ushort)a;
            r.cx = (ushort)(a >> 16);

            a = pw.release_ofs2 * 0x10000 + pw.release_ofs;
            a += r.dx;//;リリース開始位置に
            a += r.cx * 0x10000;//; (全体サイズ-指定値)を加算
            pw.release_ofs = (ushort)a;
            pw.release_ofs2 = (ushort)(a >> 16);

        prs_exit:;

            return null;
        }



        //409-422
        //;==============================================================================
        //;	/Sオプション指定時はAXを32倍する
        //;==============================================================================
        private void pcm86vol_chk()
        {
            if (pw.pcm86_vol == 0) return;

            r.ax += r.ax;
            r.ax += r.ax;
            r.ax += r.ax;
            r.ax += r.ax;
            r.ax += r.ax;

        not_p86chk:;
        }



        //423-440
        //;==============================================================================
        //;	COMMAND ')' [VOLUME UP]
        //;==============================================================================
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



        //441-459
        //;==============================================================================
        //;	COMMAND '(' [VOLUME DOWN]
        //;==============================================================================
        public Func<object> comvoldownm()
        {
            r.al = pw.partWk[r.di].volume;
            r.carry = r.al - 16 < 0;
            r.al -= 16;
            if (r.carry) r.al = 0;
            return vsetm;
        }

        //    ; Ｖ２．３　ＥＸＴＥＮＤ
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



        //460-486
        //;==============================================================================
        //;	COMMAND 'p' [Panning Set]
        //;	p0 逆相
        //; p1 右
        //; p2 左
        //; p3 中
        //;==============================================================================
        private Func<object> pansetm()
        {
            r.ah = 0;
            r.al = (byte)pw.md[r.si++].dat;
            r.al--;
            if (r.al == 0) goto psm_right;
            r.al--;
            if (r.al == 0) goto psm_left;
            r.al--;
            if (r.al == 0) goto psm_mid;
            r.ah++; //;逆相

        psm_mid:;
            r.al = 0;
            return set_pcm_pan;

        psm_left:;
            r.al = 0x80;// -128;
            return set_pcm_pan;

        psm_right:;
            r.al = +127;
            return set_pcm_pan;
        }



        //487-527
        //;==============================================================================
        //;	COMMAND 'px' [Panning Set Extend]
        //;	px-127～+127,0or1
        //;==============================================================================
        private Func<object> pansetm_ex()
        {
            r.al = (byte)pw.md[r.si++].dat;
            r.ah = (byte)pw.md[r.si++].dat;
            return set_pcm_pan;
        }

        private Func<object> set_pcm_pan()
        {
            pw.partWk[r.di].fmpan = r.al;
            pw.revpan = r.ah;

            return set_pcm_pan2();
        }

        private Func<object> set_pcm_pan2()
        {
            if ((r.al & 0x80) != 0)
                goto psmex_left;
            if (r.al == 0)
                goto psmex_mid;

            //; 右寄り
            pw.pcm86_pan_flag = 2;//; Right
            r.al = (byte)~r.al;
            r.al &= 127;
            goto psmex_gs_set;

        //; 左寄り
        psmex_left:;
            pw.pcm86_pan_flag = 1;//; Left
            r.al += 128;
            r.al &= 127;
            goto psmex_gs_set;

        //; 真ん中
        psmex_mid:;
            pw.pcm86_pan_flag = 3;//; Middle
            r.al = 0;

        psmex_gs_set:;
            pw.pcm86_pan_dat = r.al;

            if ((r.ah & 1) == 0)
                goto psmex_ret;

            pw.pcm86_pan_flag |= 4;//;逆相

        psmex_ret:;
            return null;
        }



        //528-560
        //;==============================================================================
        //;	COMMAND '@' [NEIRO Change]
        //;==============================================================================
        private Func<object> comAtm()
        {
            r.al = (byte)pw.md[r.si++].dat;
            pw.partWk[r.di].voicenum = r.al;
            return neiro_set;
        }

        private Func<object> neiro_set()
        {
            //r.ah = 0;
            //r.ax += r.ax;
            //r.bx = r.ax;
            //r.ax += r.ax;
            //r.bx += r.ax;//; bx=al*6
            //r.bx += 0;//offset pcmadrs
            //r.ax = (ushort)(pw.pcmadrs_86[r.bx] + pw.pcmadrs_86[r.bx + 1] * 0x100);//;ofs2(w)
            //r.carry = (r.bx + 2) > 0xffff;
            //r.bx += 2;
            //pw._start_ofs = r.ax;
            //r.ax = 0;
            //r.al += (byte)(pw.pcmadrs_86[r.bx] + (r.carry ? 1 : 0));//;ofs1(b)
            //r.bx++;
            //pw._start_ofs2 = r.ax;
            //r.ax = (ushort)(pw.pcmadrs_86[r.bx] + pw.pcmadrs_86[r.bx + 1] * 0x100);
            //r.bx++;
            //r.bx++;
            //pw._size1 = r.ax;
            //r.ah = 0;
            //r.al = pw.pcmadrs_86[r.bx];
            //pw._size2 = r.ax;
            //pw.repeat_flag = 0;
            //pw.release_flag1 = 0;

            ChipDatum cd = new ChipDatum(2, 0, r.al);
            p86drv(cd);

            return null;
        }



        //561-
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
            r.ax = (ushort)(r.al * r.dl);
            r.dl = r.ah;
        //;------------------------------------------------------------------------------
        //;	ENVELOPE 計算
        //;------------------------------------------------------------------------------
        pcm_env_calc:;
            r.al = r.dl;
            if (r.al == 0)//; 音量0?
            {
                mv_out();
                return;
            }
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
            mv_out();
            return;

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
            {
                mv_out();
                return;
            }
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
            {
                mv_out();
                return;
            }
            r.al = 255;
            mv_out();
            return;

        mvlfo_minus:;
            r.carry = r.ax + r.dx > 0xffff;
            r.ax += r.dx;
            if (r.carry)
            {
                mv_out();
                return;
            }
            r.al = 0;
            mv_out();
        }

        //;------------------------------------------------------------------------------
        //;	出力
        //;------------------------------------------------------------------------------
        private void mv_out()
        {
            //; 音量設定
            if (pw.pcm86_vol == 0)
                goto pcm_normal_set;
            //; SPBと同様の音量設定
            //; al = sqr(al)
            r.ah = r.al;
            r.al = 0;
            r.carry = true;

        sqr_loop:;
            bool c = (r.ah - (byte)(r.al + (r.carry ? 1 : 0))) < 0;
            r.ah -= (byte)(r.al + (r.carry ? 1 : 0));
            if (c) goto pcm_vol_set;

            c = (r.ah - (byte)(r.al + (c ? 1 : 0))) < 0;
            r.ah -= (byte)(r.al + (c ? 1 : 0));
            if (c) goto pcm_vol_set;

            r.al++;
            if (r.al == 15)
                goto pcm_vol_set;
            goto sqr_loop;

        pcm_normal_set:;
            r.al >>= 4;

        pcm_vol_set:;
            ChipDatum cd = new ChipDatum(4, 0, r.al);
            p86drv(cd);
            //r.al &= 0b0000_1111;
            //r.al ^= 0b0000_1111;
            //r.al |= 0xa0;//; PCM音量
            //r.dx = 0xa466;
            //pc98.OutPort(r.dx, r.al);
        }



        //704-718
        //;==============================================================================
        //;	PCM KEYON
        //;==============================================================================
        private void keyonm()
        {
            if (pw.partWk[r.di].onkai != 0xff) //-1
                goto keyonm_00;
            return;//; ｷｭｳﾌ ﾉ ﾄｷ
        keyonm_00:;
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            play_86pcm();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
        }



        //719-748
        //;==============================================================================
        //;	PCM KEYOFF
        //;==============================================================================
        private void keyoffm()
        {
            ChipDatum cd = new ChipDatum(9, 0, 0);
            p86drv(cd);
        //    if (pw.release_flag1 != 1)//;リリースが設定されているか?
        //        goto kofm_not_release;
        //    r.stack.Push(r.ax);
        //    r.ax = pw.release_ofs;
        //    pw.start_ofs = r.ax;
        //    r.ax = pw.release_ofs2;
        //    pw.start_ofs2 = r.ax;
        //    r.ax = pw.release_size1;
        //    pw.size1 = r.ax;
        //    r.ax = pw.release_size2;
        //    pw.size2 = r.ax;
        //    r.ax = r.stack.Pop();
        //    pw.release_flag2 = 1;//; リリースした

        //kofm_not_release:;
        //    if (pw.partWk[r.di].envf == 0xff)//-1
        //        goto kofm1_ext;
        //    if (pw.partWk[r.di].envf != 2)
        //    {
        //        pmd.keyoffp();
        //        return;
        //    }

        //kofm_ret:;
        //    return;

        //kofm1_ext:;
        //    if (pw.partWk[r.di].eenv_count == 4)
        //        goto kofm_ret;

        //    pmd.keyoffp();
            return;
        }



        //749-813
        //;==============================================================================
        //;	PCM 周波数設定
        //;==============================================================================
        private void otodasim()
        {
            r.bx = pw.partWk[r.di].fnum;
            if (r.bx != 0)
                goto tone_set;
            return;

        tone_set:;
            r.ax = pw.partWk[r.di].fnum2;
            if (pw.pcm86_vol == 1)//	;ADPCMに合わせる場合
            {
                tone_set2();//;DetuneはCut
                return;
            }
            if (pw.partWk[r.di].detune == 0)
            {
                tone_set2();
                return;
            }

            r.cx = r.ax;
            r.dx = r.bx;

            int c = r.dx * 0x10000 + r.cx;
            c >>= 5;
            r.dx = (ushort)(c >> 16);
            r.cx = (ushort)c;

            //for (int i = 0; i < 5; i++)//rept	5
            //{
            //    r.carry = (r.dx & 1) != 0;
            //    r.dx >>= 1;
            //    r.cx >>= 1;
            //    if (r.carry) r.cx |= 0x8000;
            //}//endm			;cx=zzzzzxxx xxxxxxxx

            r.dx = pw.partWk[r.di].detune;
            if ((r.dx & 0x8000) != 0)
                goto tsdt_minus;
            r.carry = r.cx + r.dx > 0xffff;
            r.cx += r.dx;
            if (!r.carry) goto tone_set1;
            r.cx = 0xffff;//-1
            goto tone_set1;

        tsdt_minus:;
            r.carry = r.cx + r.dx > 0xffff;
            r.cx += r.dx;
            if (r.cx == 0) goto tsdtm0;
            if (r.carry) goto tone_set1;

            tsdtm0:;
            r.cx = 1;//;0にすると加算値0になる危険があるので1にする

        tone_set1:;
            r.dx = 0;

            c = r.dx * 0x10000 + r.cx;
            c <<= 5;
            r.dx = (ushort)(c >> 16);
            r.cx = (ushort)c;
            //for (int i = 0; i < 5; i++)//rept	5
            //{
            //    r.carry = (r.cx & 0x8000) != 0;
            //    r.cx <<= 1;
            //    r.dx <<= 1;
            //    if (r.carry) r.dx |= 0x0001;
            //}//    endm			;dx:cx=00000000 000zzzzz xxxxxxxx xxx00000

            r.bx &= 0b1111_1111_1110_0000;
            r.ax &= 0b0000_0000_0001_1111;
            r.bx |= r.dx;
            r.ax |= r.cx;

            tone_set2();
        }

        private void tone_set2()
        {
            pw.addsize2 = r.ax;
            pw.addsize1 = r.bl;
            //pw.addsize1 &= 0x1f;
            //Console.WriteLine("{0:x}  {1:x}", pw.addsize1, pw.addsize2);
            ChipDatum cd = new ChipDatum(5, pw.addsize1, pw.addsize2);
            p86drv(cd);

            //r.carry = false;
            //r.bl = r.rol(r.bl, 1);
            //r.bl = r.rol(r.bl, 1);
            //r.bl = r.rol(r.bl, 1);
            //r.bl &= 7;
            //r.bl ^= 7;
            ////; 周波数設定
            //r.dx = 0xa468;
            ////pushf
            ////cli
            //r.al = pc98.InPort(r.dx);
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xf8;
            //r.al |= r.bl;
            //pc98.OutPort(r.dx, r.al);
            ////popf
        }



        //814-853
        //;==============================================================================
        //;	PCM FNUM SET
        //;==============================================================================
        private void fnumsetm()
        {
            r.ah = r.al;
            r.ah &= 0xf;
            if (r.ah == 0xf)
            {
                pmd.fnrest();//; 休符の場合
                return;
            }

            if (pw.pcm86_vol != 1)
                goto fsm_noad;

            if (r.al < 0x65)//; o7e?
                goto fsm_noad;
            r.al = 0x50;//;o6
            if (r.ah >= 5)//;ah=onkai
                goto fsm_00;
            r.al = 0x60;//;o7

        fsm_00:;
            r.al |= r.ah;

        fsm_noad:;
            pw.partWk[r.di].onkai = r.al;

            r.al &= 0xf0;
            r.al >>= 1;
            r.bl = r.al;//; bl=octave*8
            r.al >>= 1;//; al=octave*4
            r.bl += r.al;

            r.bl += r.ah;//; bl=octave*12 + 音階
            r.bh = 0;
            r.ax = r.bx;
            //r.bx += r.bx;
            //r.bx += r.ax;
            //r.bx += 0;//offset pcm_tune_data
            //Console.WriteLine("bx:{0}",r.bx);
            r.al = pw.pcm_tune_data86[r.bx].Item1;
            r.ax |= 0xff00;
            pw.partWk[r.di].fnum = r.ax;//;ax=0ff00h + addsize1
            //r.bx++;
            r.ax = pw.pcm_tune_data86[r.bx].Item2;//;ax=addsize2
            pw.partWk[r.di].fnum2 = r.ax;
            //Console.WriteLine("fnum:{0:x} fnum2:{1:x}", pw.partWk[r.di].fnum, pw.partWk[r.di].fnum2);
        }



        //854-908
        //;==============================================================================
        //;	FIFO int Subroutine
        //;		*FIFOが来ている事を確認してから飛んで来ること。
        //;		 pushしてあるレジスタは ax/dx/ds のみ。
        //;==============================================================================
        private void fifo_main()
        {
            //;------------------------------------------------------------------------------
            //;	割り込み許可
            //;------------------------------------------------------------------------------
            if (pw.disint == 1)
                goto fifo_not_sti;

            //sti			;早速割り込み許可
            fifo_not_sti:;

            //;------------------------------------------------------------------------------
            //;	PCM処理 main
            //;------------------------------------------------------------------------------
            if (pw.play86_flag == 0)//	;PCM再生中か？
                goto not_trans;

            if (pw.trans_flag != 0)//	;次を転送するか？
                goto i5_trans;

            stop_86pcm();// ; PLAY中で且つ次にはもうデータはない= stop
            return;//; FIFOは許可しないで終了

        i5_trans:;
            r.stack.Push(r.bx);
            r.stack.Push(r.cx);
            r.stack.Push(r.si);
            r.stack.Push(r.di);
            r.stack.Push(r.bp);

            pcm_trans();

            r.bp = r.stack.Pop();
            r.di = r.stack.Pop();
            r.si = r.stack.Pop();
            r.cx = r.stack.Pop();
            r.bx = r.stack.Pop();

        //;------------------------------------------------------------------------------
        //;	割り込み禁止
        //;------------------------------------------------------------------------------
        not_trans:;
            //	cli
            //;------------------------------------------------------------------------------
            //;	FIFO割り込みフラグreset
            //;------------------------------------------------------------------------------
            r.dx = 0xa468;
            r.al = pc98.InPort(r.dx);
            pc98.OutPort(0x5f, r.al);
            r.al &= 0xef;
            pc98.OutPort(r.dx, r.al);//;FIFO割り込みフラグ消去
            pc98.OutPort(0x5f, r.al);
            r.al |= 0x10;
            pc98.OutPort(r.dx, r.al);//;FIFO割り込みフラグ消去解除
            return;
        }



        //909-1201
        //;==============================================================================
        //;	PCMdata 転送
        //; use ax/bx/cx/dx/si/di/bp
        //;==============================================================================
        private void pcm_trans2()
        {
            r.cx = (ushort)pw.trans_size;// 転送するbytes
            pcm_trans_main();
        }

        private void pcm_trans()
        {
            r.cx = (ushort)(pw.trans_size / 2);// 転送するbytes
            pcm_trans_main();
        }

        private void pcm_trans_main()
        {
            //KUMA: P86drv常駐チェック
            //      恐らく不要なため未実装。
            //goto zero_trans;// 常駐していない場合

            //KUMA: P86drvのバージョンチェック
            //      恐らく不要なため未実装。
            //r.ah=0xff://-1
            //int	65h
            //if (r.al < 0x10)
            //    goto zero_trans;//;ver.1.0以前の場合


            r.ah = 0;
            r.al = pw.pcm86_pan_flag;
            r.ax += r.ax;
            r.ax += 0;//offset trans_table
            r.bp = r.ax;//;bp=転送処理sub offset

            r.dx = 0xa46c;
            r.ax = pw.size1;
            r.di = r.ax;//; di=残りsize(下位16bit)
            r.ax |= pw.size2;
            if (r.ax == 0)
            {
                zero_trans();
                return;
            }

            //r.stack.Push(r.ds);

            r.ah = 0xfb;//-5
            ChipDatum cd = new ChipDatum(r.ah, -1, -1);
            p86drv(cd);//;p86drv pushems

            r.ah = pw.addsize1;
            r.bx = pw.addsize2;

            get_data_offset();//; ds:si = data offset
            trans_table[r.bp / 2]();

            r.ah = 0xfc;//-4
            cd = new ChipDatum(r.ah, -1, -1);
            p86drv(cd);//;p86drv popems

            //    pop ds

            return;
        }

        //;------------------------------------------------------------------------------
        //;	真ん中
        //;------------------------------------------------------------------------------
        private void double_trans()
        {
            r.bp = 0;
        double_trans_loop:;
            do
            {
                //	mov al,[si]
                pc98.OutPort(r.dx, r.al);//;左
                pc98.OutPort(r.dx, r.al);//;右
                add_address();
                if (r.carry)
                {
                    trans_fin();
                    return;
                }
                r.cx--;
            } while (r.cx != 0);
            trans_exit();
        }

        private void trans_exit()
        {
            pw.start_ofs += r.bp;//;bp=転送したサイズ
            pw.start_ofs2 += (ushort)(r.carry ? 1 : 0);
            pw.size1 = r.di;
            return;
        }

        //;------------------------------------------------------------------------------
        //;	真ん中(逆相)
        //;------------------------------------------------------------------------------
        private void double_trans_g()
        {
            r.bp = 0;
        double_trans_g_loop:;
            do
            {
                //	mov al,[si]
                pc98.OutPort(r.dx, r.al);//;左
                r.al = (byte)-r.al;
                pc98.OutPort(r.dx, r.al);//;右
                add_address();
                if (r.carry)
                {
                    trans_fin();
                    return;
                }
                r.cx--;
            } while (r.cx != 0);
            trans_exit();
        }

        //;------------------------------------------------------------------------------
        //;	左寄り
        //;------------------------------------------------------------------------------
        private void left_trans()
        {
            r.bp = 0;
        left_trans_loop:;
            do
            {
                //	mov al,[si]
                pc98.OutPort(r.dx, r.al);//;左
                r.stack.Push(r.ax);
                r.ax = (ushort)(r.ax * pw.pcm86_pan_dat);
                r.ax += r.ax;
                r.al = r.ah;
                pc98.OutPort(r.dx, r.al);//;右
                r.ax = r.stack.Pop();
                add_address();
                if (r.carry)
                {
                    trans_fin();
                    return;
                }
                r.cx--;
            } while (r.cx != 0);
            trans_exit();
        }

        //;------------------------------------------------------------------------------
        //;	左寄り(逆相)
        //;------------------------------------------------------------------------------
        private void left_trans_g()
        {
            r.bp = 0;
        left_trans_g_loop:;
            do
            {
                //	mov al,[si]
                pc98.OutPort(r.dx, r.al);//;左
                r.al = (byte)-r.al;
                r.stack.Push(r.ax);
                r.ax = (ushort)(r.ax * pw.pcm86_pan_dat);
                r.ax += r.ax;
                r.al = r.ah;
                pc98.OutPort(r.dx, r.al);//;右
                r.ax = r.stack.Pop();
                add_address();
                if (r.carry)
                {
                    trans_fin();
                    return;
                }
                r.cx--;
            } while (r.cx != 0);
            trans_exit();
        }

        //;------------------------------------------------------------------------------
        //;	右寄り
        //;------------------------------------------------------------------------------
        private void right_trans()
        {
            r.bp = 0;
        right_trans_loop:;
            do
            {
                //	mov al,[si]
                r.stack.Push(r.ax);
                r.ax = (ushort)(r.ax * pw.pcm86_pan_dat);
                r.ax += r.ax;
                r.al = r.ah;
                pc98.OutPort(r.dx, r.al);//;左
                r.ax = r.stack.Pop();
                pc98.OutPort(r.dx, r.al);//;右
                add_address();
                if (r.carry)
                {
                    trans_fin();
                    return;
                }
                r.cx--;
            } while (r.cx != 0);
            trans_exit();
        }

        //;------------------------------------------------------------------------------
        //;	右寄り(逆相)
        //;------------------------------------------------------------------------------
        private void right_trans_g()
        {
            r.bp = 0;
        right_trans_g_loop:;
            do
            {
                //	mov al,[si]
                r.stack.Push(r.ax);
                r.ax = (ushort)(r.ax * pw.pcm86_pan_dat);
                r.ax += r.ax;
                r.al = r.ah;
                pc98.OutPort(r.dx, r.al);//;左
                r.ax = r.stack.Pop();
                r.al = (byte)-r.al;//;逆相
                pc98.OutPort(r.dx, r.al);//;右
                add_address();
                if (r.carry)
                {
                    trans_fin();
                    return;
                }
                r.cx--;
            } while (r.cx != 0);
            trans_exit();
        }

        //;------------------------------------------------------------------------------
        //;	Addressを進める
        //;		cy=1 ･･･ 転送終了
        //;------------------------------------------------------------------------------
        private void add_address()
        {
            pw.addsizew += r.bx;//;bx=addsize2
            //pushf
            r.al = r.ah;
            r.ah = 0;//; ax=addsize1
            r.bp += (ushort)(r.ax + (r.carry ? 1 : 0));//;bpをaddsizeに従って加算
            //popf
            //pushf
            r.si += (ushort)(r.ax + (r.carry ? 1 : 0));//;addressをaddsizeに従って加算
            if (r.si < 0x4000)//;16K Over Check(for EMS)
                goto not_add_ofs2;

            //;[[[segment over]]]
            r.carry = (pw.start_ofs + r.bp) > 0xffff;
            pw.start_ofs += r.bp;
            pw.start_ofs2 += (ushort)(0 + (r.carry ? 1 : 0));

            r.bp = 0;//; 転送サイズのreset
            get_data_offset();

        not_add_ofs2:;
            //popf
            bool c = (r.di - (ushort)(r.ax + (r.carry ? 1 : 0))) < 0;
            r.di -= (ushort)(r.ax + (r.carry ? 1 : 0));//;sizeをaddsizeに従って減算
            r.ah = r.al;//;ah=addsize1 に戻す
            if (c) goto addadd_sizeseg;
            if (r.di == 0) goto addadd_justcheck;
            return;

        addadd_justcheck:;
            if (pw.size2 == 0)//;ジャスト０
                goto addadd_repchk;
            return;

        addadd_sizeseg:;
            r.carry = (pw.size2 - 1) < 0;
            pw.size2 -= 1;
            if (r.carry) goto addadd_repchk;
            return;

        addadd_repchk:;
            if (pw.repeat_flag == 0)
                goto addadd_stc_ret;

            if (pw.release_flag2 == 1)
                goto addadd_stc_ret;

            //; repeat設定
            r.stack.Push(r.ax);
            r.stack.Push(r.dx);
            r.ax = pw.repeat_size2;
            pw.size2 = r.ax;
            r.di = pw.repeat_size1;
            r.ax = pw.repeat_ofs2;
            pw.start_ofs2 = r.ax;
            r.dx = pw.repeat_ofs;
            pw.start_ofs = r.dx;
            r.bp = 0;

            r.ah = 0xfd;
            ChipDatum cd = new ChipDatum(r.ah, -1, -1);
            p86drv(cd);//;get data offset = ds:dx

            r.si = r.dx;//;DS:SI= DATA ADDRESS
            r.dx = r.stack.Pop();
            r.ax = r.stack.Pop();
            r.carry = false;
            return;

        addadd_stc_ret:;
            r.carry = true;
            return;
        }

        //;------------------------------------------------------------------------------
        //;	新規にpcmdata offsetを得る
        //;------------------------------------------------------------------------------
        private void get_data_offset()
        {
            r.stack.Push(r.ax);
            r.stack.Push(r.dx);

            r.dx = pw.start_ofs;//cs:[start_ofs]
            r.ax = pw.start_ofs2;//cs:[start_ofs2]

            r.ah = 0xfd;
            ChipDatum cd = new ChipDatum(r.ah, -1, -1);
            p86drv(cd);//;get data offset = ds:dx

            r.si = r.dx;//;DS:SI= DATA ADDRESS

            r.dx = r.stack.Pop();
            r.ax = r.stack.Pop();
            return;
        }

        //;------------------------------------------------------------------------------
        //;	転送終了･･･残りを０で埋める
        //;------------------------------------------------------------------------------
        private void trans_fin()
        {
            r.cx--;
            if (r.cx == 0) goto tfin_ret;

            r.al = 0;

        tfin_loop:;
            do
            {
                pc98.OutPort(r.dx, r.al);//;左
                pc98.OutPort(r.dx, r.al);//;右
                r.cx--;
            } while (r.cx != 0);

        tfin_ret:;
            pw.size1 = r.cx;//cs:[size1]	;cx=0
            pw.size2 = r.cx;//cs:[size2]
            return;
        }

        //;------------------------------------------------------------------------------
        //;	0で埋める
        //;------------------------------------------------------------------------------
        private void zero_trans()
        {
            r.al = 0;
        ztr_loop:;
            do
            {
                pc98.OutPort(r.dx, r.al);//;左
                pc98.OutPort(r.dx, r.al);//;右
                r.cx--;
            } while (r.cx != 0);
            pw.trans_flag = 0;//; もう転送しないでいいよ
            return;
        }



        //1202-1296
        //;==============================================================================
        //;	86B play PCM
        //;==============================================================================
        private void play_86pcm()
        {
            ChipDatum cd = new ChipDatum(3, pw.pcm86_pan_flag, pw.pcm86_pan_dat);
            p86drv(cd);
            
            cd = new ChipDatum(7, 0, 0);
            p86drv(cd);
            
            ////pushf
            ////cli

            //r.dx = 0xa468;
            //r.al = pc98.InPort(r.dx);
            ////;	A468 bit7をreset	（FIFO停止）
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0x7f;
            //pc98.OutPort(r.dx, r.al);

            ////;	A468 bit6をreset	（CPU->FIFO モード）
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xbf;
            //pc98.OutPort(r.dx, r.al);

            ////;	A468 bit3をset		（FIFO リセット設定）
            //pc98.OutPort(0x5f, r.al);
            //r.al |= 8;
            //pc98.OutPort(r.dx, r.al);

            ////;	A468 bit3をreset	（FIFO リセット解除）
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xf7;
            //pc98.OutPort(r.dx, r.al);

            ////;	A468 bit5をreset	（FIFO割り込み禁止/A46A設定準備）
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xdf;
            //pc98.OutPort(r.dx, r.al);

            ////;	A468 bit4をreset	（割り込みフラグ消去）
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xef;
            //pc98.OutPort(r.dx, r.al);

            ////;	A46A に PAN を OUT	（8bit L/Rch）
            //r.dx = 0xa46a;
            //r.al = 0xf2;
            //pc98.OutPort(r.dx, r.al);

            ////popf

            ////; 最初のdataを転送
            //r.si = 0;//offset _start_ofs
            //r.di = 0;//offset start_ofs
            //r.cx = 4;
            ////rep movsw

            //pw.addsizew = 0;
            //pw.release_flag2 = 0;

            //r.stack.Push(r.bp);
            //pcm_trans2();
            //r.bp = r.stack.Pop();

            ////pushf
            ////cli
            ////;------------------------------------------------------------------------------
            ////;	割り込み設定
            ////;------------------------------------------------------------------------------
            //r.dx = 0xa468;
            //r.al = pc98.InPort(r.dx);

            ////;	A468 bit4をset		（割り込みフラグ消去解除）
            //pc98.OutPort(0x5f, r.al);
            //r.al |= 0x10;
            //pc98.OutPort(r.dx, r.al);

            ////;	A468 bit5をset		（FIFO割り込み許可/A46A設定準備）
            //pc98.OutPort(0x5f, r.al);
            //r.al |= 0x20;
            //pc98.OutPort(r.dx, r.al);

            ////;	A46AのFIFO割り込みサイズを設定
            //r.dx = 0xa46a;
            //r.al = (byte)(+(pw.trans_size / 128) - 1);
            //pc98.OutPort(r.dx, r.al);

            ////;------------------------------------------------------------------------------
            ////;	再生開始
            ////;------------------------------------------------------------------------------
            //r.dx = 0xa468;
            //r.al = pc98.InPort(r.dx);
            ////;	A468 bit7をset		（PCM 再生開始）
            //pc98.OutPort(0x5f, r.al);
            //r.al |= 0x80;
            //pc98.OutPort(r.dx, r.al);

            //pw.play86_flag = 1;
            //pw.trans_flag = 1;

            ////popf

            return;
        }



        //1297-1339
        //;==============================================================================
        //;	86B PCM stop
        //;==============================================================================
        public void stop_86pcm()
        {
            ChipDatum cd = new ChipDatum(8, 0, 0);
            p86drv(cd);

            //r.stack.Push(r.ax);
            //r.stack.Push(r.dx);

            ////pushf
            ////cli

            //r.dx = 0xa468;
            //r.al = pc98.InPort(r.dx);

            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0x7f;
            //pc98.OutPort(r.dx, r.al);

            ////;	FIFO reset
            //pc98.OutPort(0x5f, r.al);
            //r.al |= 0x08;
            //pc98.OutPort(r.dx, r.al);//;Reset処理

            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xf7;
            //pc98.OutPort(r.dx, r.al);//;Reset処理おわり

            ////;	FIFO 割り込み禁止
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xdf;
            //pc98.OutPort(r.dx, r.al);

            ////;	FIFO 割り込みフラグreset
            //pc98.OutPort(0x5f, r.al);
            //r.al &= 0xef;
            //pc98.OutPort(r.dx, r.al);

            //pc98.OutPort(0x5f, r.al);
            //r.al |= 0x10;
            //pc98.OutPort(r.dx, r.al);

            //pw.play86_flag = 0;//cs:[play86_flag]
            //pw.trans_flag = 0;//cs:[trans_flag]

            ////popf

            //r.dx = r.stack.Pop();
            //r.ax = r.stack.Pop();
            return;
        }



        //1340-1382
        //;==============================================================================
        //;	ＰＣＭ効果音ルーチン
        //;		input dx  fnum
        //;			ch Pan
        //; cl Volume
        //; al Number
        //;==============================================================================
        private void pcm_effect()
        {
            r.bx = 0; //offset part10
            pw.partWk[pw.part10].partmask |= 2;//;PCM Part Mask
            pw.pcmflag = 1;
            pw.pcm_effec_num = r.al;
            pw._voice_delta_n = r.dx;
            pw._pcm_volume = r.cl;
            pw._pcmpan = r.ch;

            stop_86pcm();

            //cli

            r.al = pw.pcm_effec_num;
            neiro_set();

            r.al = pw._pcmpan;
            r.ah = 0;
            set_pcm_pan2();

            r.bx = (ushort)pw._voice_delta_n;
            r.ax = r.bx;
            r.bl = r.bh;
            r.bx &= 0b0111_0000_0000_1111;
            r.bh <<= 1;
            r.bl |= r.bh;
            r.ah = r.al;
            r.al = 0;
            tone_set2();
            r.al = pw._pcm_volume;
            mv_out();

            //sti

            play_86pcm();

            return;
        }

    }
}