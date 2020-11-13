using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class EFCDRV
    {
        private PMD pmd = null;
        private PW pw = null;
        private x86Register r = null;
        private Func<ChipDatum, int> ppsdrv = null;

        public EFCDRV(PMD pmd, PW pw, x86Register r, Func<ChipDatum, int> ppsdrv)
        {
            this.pmd = pmd;
            this.pw = pw;
            this.r = r;
            this.ppsdrv = ppsdrv;
        }

        public void effgo()
        {
            if (pw.ppsdrv_flag == 0)
                goto effgo2;
            r.al |= 0x80;
            r.zero = pw.last_shot_data == r.al;
            pw.last_shot_data = r.al;
            if (!r.zero) goto effgo2;
            r.stack.Push(r.ax);
            r.ah = 0;
            ChipDatum cd = new ChipDatum(0x02, 0, 0);
            ppsdrv(cd);//.Stop();
            r.ax = r.stack.Pop();
        effgo2:;
            pw.hosei_flag = 3;
            eff_main();
        }

        public void eff_on2()
        {
            pw.hosei_flag = 1;
            eff_main();
        }

        public void eff_on()
        {
            pw.hosei_flag = 0;
            eff_main();
        }

        private void eff_main()
        {
            //r.ds = r.cs;

            if (pw.effflag == 0)
                goto eg_00;
            return;//; 効果音を使用しないモード

        eg_00:;
            if (pw.ppsdrv_flag == 0)
                goto eg_nonppsdrv;
            r.al |= r.al;
            if ((r.al & 0x80) == 0) goto eg_nonppsdrv;

            //; ppsdrv
            if (pw.effon >= 2) return;// goto effret;// ; 通常効果音発音時は発声させない

            r.bx = (ushort)pw.part9;//; PSG 3ch
            pw.partWk[r.bx].partmask |= 2;//; Part Mask
            pw.effon = 1;//; 優先度１(ppsdrv)
            pw.psgefcnum = r.al;//; 音色番号設定(80H～)

            r.bx = 15;
            r.ah = pw.hosei_flag;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto not_tone_hosei;
            r.bx = pw.partWk[r.di].detune;
            r.bh = r.bl;//; BH = Detuneの下位 8bit
            r.bl = 15;
        not_tone_hosei:;
            r.ah = r.ror(r.ah, 1);
            if (!r.carry) goto not_volume_hosei;
            r.ah = pw.partWk[r.di].volume;
            if (r.ah >= 15) goto fade_hosei;
            r.bl = r.ah;//; BL = volume値(0～15)
        fade_hosei:;
            r.ah = pw.fadeout_volume;
            if (r.ah == 0)
                goto not_volume_hosei;
            r.stack.Push(r.ax);
            r.al = r.bl;
            r.ah = (byte)-r.ah;
            r.ax = (ushort)(r.al * r.ah);
            r.bl = r.ah;
            r.ax = r.stack.Pop();
        not_volume_hosei:;
            if (r.bl == 0)
                goto ppsdrm_ret;
            r.bl ^= 0b0000_1111;//volume
            r.ah = 1;//command
            r.al &= 0x7f;//num?

            ChipDatum cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = pw.cmd;
            pmd.WriteOPNARegister(cd);
            if (pw.cmd != null && pw.cmd.args != null && pw.cmd.args.Count > 2 && pw.cmd.args[2] is MmlDatum[])
            {
                foreach (MmlDatum md in (MmlDatum[])pw.cmd.args[2])
                {
                    pmd.ExecIDESpecialCommand(md);
                }
            }

            cd = new ChipDatum(0x01, (r.al << 8) | r.bh, r.bl);
            ppsdrv(cd);//.Play(r.al, r.bh, r.bl);//; ppsdrv keyon
        ppsdrm_ret:;
            return;

        //; TimerA
        eg_nonppsdrv:;
            pw.psgefcnum = r.al;
            r.ah = 0;
            r.bx = r.ax;
            //r.bx += r.bx;
            //r.bx = r.ax;
            //r.bx += 0;//offset efftbl

            r.al = pw.effon;
            if (r.al > pw.efftbl[r.bx].Item1)//	cmp al,[bx]; 優先順位
                return;// goto eg_ret;

            if (pw.ppsdrv_flag == 0)
                goto eok_nonppsdrv;
            r.ah = 0;
            cd = new ChipDatum(0x02, 0, 0);
            ppsdrv(cd);//.Stop();//; ppsdrv 強制keyoff
        eok_nonppsdrv:;


            cd = new ChipDatum(-1, -1, -1);
            cd.addtionalData = pw.cmd;
            pmd.WriteOPNARegister(cd);
            if (pw.cmd != null && pw.cmd.args != null && pw.cmd.args.Count > 2 && pw.cmd.args[2] is MmlDatum[])
            {
                foreach (MmlDatum md in (MmlDatum[])pw.cmd.args[2])
                {
                    pmd.ExecIDESpecialCommand(md);
                }
            }


            r.si = 0;// pw.efftbl[r.bx].Item2;
            r.si += 0;//offset efftbl
            pw.crtEfcDat = pw.efftbl[r.bx].Item2;
            r.al = pw.efftbl[r.bx].Item1;//; AL = 優先順位
            r.stack.Push(r.ax);
            r.bx = (ushort)pw.part9;//; PSG 3ch
            pw.partWk[r.bx].partmask |= 2;//; Part Mask
            efffor();//;１発目を発音
            r.ax = r.stack.Pop();
            pw.effon = r.al;//; 優先順位を設定(発音開始)
        eg_ret:;
            return;
        }



        //;
        //;	こーかおん えんそう めいん	
        //; 	Ｆｒｏｍ ＶＲＴＣ
        //;



        public void effplay()
        {
            r.dl = pw.effcnt;
            pw.effcnt--;
            if (pw.effcnt != 0)
            {
                effsweep();
                return;
            }

            r.si = pw.effadr;
            efffor();
        }

        private void efffor()
        {
            r.al = (byte)pw.crtEfcDat[r.si++].dat;
            if (r.al == 0xff)//-1
            {
                effend();
                return;
            }

            pw.effcnt = r.al;//; カウント数

            r.dh = 4;//; 周波数レジスタ
                     //pushf
                     //cli
            efsnd();//; 周波数セット
            r.cl = r.dl;
            efsnd();//; 周波数セット
                    //popf
            r.ch = r.dl;
            pw.eswthz = r.cx;
            r.dl = (byte)pw.crtEfcDat[r.si].dat;
            pw.eswnhz = r.dl;
            r.dh = 6;
            efsnd();//; ノイズ
            pw.psnoi_last = r.dl;

            r.al = (byte)pw.crtEfcDat[r.si++].dat;//; データ
            r.dl = r.al;
            r.dl = r.rol(r.dl, 1);
            r.dl = r.rol(r.dl, 1);
            r.dl &= 0b0010_0100;

            //pushf
            //cli
            pmd.get07();
            r.al &= 0b1101_1011;
            r.dl |= r.al;
            pmd.opnset44();//; MIX CONTROLL...
            //popf

            r.dh = 10;
            efsnd();//; ボリューム
            efsnd();//; エンベロープ周波数
            efsnd();
            efsnd();//; エンベロープPATTARN

            r.al = (byte)pw.crtEfcDat[r.si++].dat;

            r.ax = (ushort)(sbyte)r.al;//    cbw
            pw.eswtst = r.ax;//; スイープ増分(TONE)
            r.al = (byte)pw.crtEfcDat[r.si++].dat;
            pw.eswnst = r.al;//; スイープ増分(NOISE)
            r.al &= 15;
            pw.eswnct = r.al;//; スイープカウント(NOISE)
            pw.effadr = r.si;
        //effret:;
            return;
        }

        private void efsnd()
        {
            r.al = (byte)pw.crtEfcDat[r.si++].dat;
            r.dl = r.al;
            pmd.opnset44();
            r.dh++;
            return;
        }

        public void effoff()
        {
            //r.dx = r.cs;
            //r.ds = r.dx;
            effend();
        }

        public void effend()
        {
            if (pw.ppsdrv_flag == 0)
                goto ee_nonppsdrv;
            r.ah = 0;
            ChipDatum cd = new ChipDatum(0x02, 0, 0);
            ppsdrv(cd);//.Stop();//; ppsdrv keyoff
        ee_nonppsdrv:;
            r.dx = 0xa00;
            pmd.opnset44();//; volume min
            r.dh = 7;
            //pushf
            //cli
            pmd.get07();
            r.dl = r.al;//; NOISE CUT
            r.dl &= 0b1101_1011;
            r.dl |= 0b0010_0100;
            pmd.opnset44();
            //popf
            pw.effon = 0;
            pw.psgefcnum = 0xff;//-1
            return;
        }

        //; 普段の処理

        private void effsweep()
        {
            r.ax = pw.eswthz;//; スイープ周波
            r.ax += pw.eswtst;
            pw.eswthz = r.ax;//;スイープ周波
            r.dh = 4;//;REG
            r.dl = r.al;//;DATA
            //pushf
            //cli
            pmd.opnset44();
            r.dh++;
            r.dl = r.ah;
            pmd.opnset44();
            pmd.get07();
            r.dl = r.al;
            r.dh = 7;
            pmd.opnset44();
            //popf
            r.dl = pw.eswnst;
            if (r.dl == 0) return;//goto effret;//; ノイズスイープ無し
            pw.eswnct--;
            if (pw.eswnct != 0) return;//goto effret;
            r.al = r.dl;
            r.al &= 15;
            pw.eswnct = r.al;
            r.dl >>= 1;
            r.dl >>= 1;
            r.dl >>= 1;
            r.dl >>= 1;
            pw.eswnhz += r.dl;
            r.dl = pw.eswnhz;
            r.dh = 6;
            pmd.opnset44();
            pw.psnoi_last = r.dl;
            return;
        }

    }
}