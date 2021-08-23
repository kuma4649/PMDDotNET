using System;
using System.Collections.Generic;
using System.Text;
using PMDDotNET.Common;
using musicDriverInterface;
using System.IO;
using System.Xml.Serialization;

namespace PMDDotNET.Driver
{
    public class Driver : iDriver
    {
        private iEncoding enc = null;
        private PMD pmd = null;
        private PW work = null;
        private int renderingFreq = 44100;
        private int opnaMasterClock = 7987200;
        private Action<ChipDatum> WriteOPNA;
        private Func<ChipDatum, int> WritePPZ8;
        private Func<ChipDatum, int> WritePPSDRV;
        private Func<ChipDatum, int> WriteP86;
        private Action<long, int> WaitSendOPNA;
        private object lockObjWriteReg = new object();
        static MmlDatum[] srcBuf = null;
        public Exception renderingException = null;

        public Driver(iEncoding enc = null)
        {
            this.enc = enc ?? myEncoding.Default;
        }

        public void FadeOut()
        {
            throw new NotImplementedException();
        }

        public MmlDatum[] GetDATA()
        {
            throw new NotImplementedException();
        }

        public byte[] GetPCMFromSrcBuf()
        {
            throw new NotImplementedException();
        }

        public ChipDatum[] GetPCMSendData()
        {
            throw new NotImplementedException();
        }

        public Tuple<string, ushort[]>[] GetPCMTable()
        {
            throw new NotImplementedException();
        }

        public int GetStatus()
        {
            if (work.Status < 0) return -1;
            pmd.int60_main(0x0a00);
            return pmd.pw.status2 != 0xff ? 1 : 0;
        }

        public int GetNowLoopCounter()
        {
            return pmd.pw.nowLoopCounter;
        }

        /// <summary>
        /// ドライバ固有のタグを取得
        /// </summary>
        public List<Tuple<string, string>> GetTags()
        {
            List<Tuple<string, string>> tags = new List<Tuple<string, string>>();
            x86Register lr = new x86Register();
            PW lpw = new PW();
            lpw.mmlbuf = 1;
            lpw.md =srcBuf;

            string str;
            ushort adr = get_memo(1,lr,lpw);
            if (adr != 0)
            {
                str = getNRDString(ref adr);
                tags.Add(new Tuple<string, string>("title", str));
            }

            adr = get_memo(2, lr, lpw);
            if (adr != 0)
            {
                str = getNRDString(ref adr);
                tags.Add(new Tuple<string, string>("composer", str));
            }

            adr = get_memo(3, lr, lpw);
            if (adr != 0)
            {
                str = getNRDString(ref adr);
                tags.Add(new Tuple<string, string>("arranger", str));
            }
            
            int al = 4;
            str = "";
            do
            {
                adr = get_memo(al, lr, lpw);
                if (adr != 0) str += "\r\n" + getNRDString(ref adr);
                al++;
            } while (adr != 0);
            str = str != "" ? str.Substring(2) : "";
            if (!string.IsNullOrEmpty(str))
            {
                tags.Add(new Tuple<string, string>("memo", str));
            }

            adr = get_memo(0, lr, lpw);
            if (adr != 0)
            {
                str = getNRDString(ref adr);
                tags.Add(new Tuple<string, string>("PCMFile", str));
                if(work!=null) work.ppcFile = str.Trim();
            }

            adr = get_memo(-1, lr, lpw);
            if (adr != 0)
            {
                str = getNRDString(ref adr);
                tags.Add(new Tuple<string, string>("PPSFile", str));
                if (work != null) work.ppsFile = str.Trim();
            }

            adr = get_memo(-2, lr, lpw);
            if (adr != 0)
            {
                str = getNRDString(ref adr);
                tags.Add(new Tuple<string, string>("PPZFile", str));
                if (work != null)
                {
                    work.ppz1File = str.Trim();
                    string[] p = work.ppz1File.Split(',');
                    if (p.Length > 1)
                    {
                        work.ppz1File = p[0];
                        work.ppz2File = p[1];
                    }
                }
            }

            return tags;
        }

        public ushort get_memo(int al, x86Register r, PW pw)
        {
            try
            {

                r.al = (byte)al;
                r.si = (ushort)pw.mmlbuf;
                if (pw.md[r.si].dat != 0x1a)
                    goto getmemo_errret;//;音色がないfile=メモのアドレス取得不能
                r.si += 0x18;
                r.si = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
                r.si += (ushort)pw.mmlbuf;
                r.si -= 4;
                r.bx = (ushort)(pw.md[r.si + 2].dat + pw.md[r.si + 3].dat * 0x100);//;bh=0feh,bl=ver
                if (r.bl == 0x40)//;Ver4.0 & 00Hの場合
                    goto getmemo_exec;
                if (r.bh != 0xfe)
                    goto getmemo_errret;//;Ver.4.1以降は 0feh
                if (r.bl < 0x41)
                    goto getmemo_errret;//;MC version 4.1以前だったらError
                getmemo_exec:;
                if (r.bl < 0x42)//;Ver.4.2以降か？
                    goto getmemo_oldver41;
                r.al++;//; ならalを +1 (0FFHで#PPSFile)
            getmemo_oldver41:;
                if (r.bl < 0x48)//;Ver.4.8以降か？
                    goto getmemo_oldver47;
                r.al++;//; ならalを +1 (0FEHで#PPZFile)
            getmemo_oldver47:;
                r.si = (ushort)(pw.md[r.si].dat + pw.md[r.si + 1].dat * 0x100);
                r.si += (ushort)pw.mmlbuf;
                r.al++;
            getmemo_loop:;
                r.dx = (ushort)(pw.md[r.si + 0].dat + pw.md[r.si + 1].dat * 0x100);
                if (r.dx == 0)
                    goto getmemo_errret;
                r.si += 2;
                r.al--;
                if (r.al != 0)
                    goto getmemo_loop;
                getmemo_exit:;
                r.dx += (ushort)pw.mmlbuf;
                pw.ds_push = 0;// r.cs; セグメントなし
                pw.dx_push = r.dx;
                return r.dx;

            getmemo_errret:;
                pw.ds_push = 0;
                pw.dx_push = 0;
                return 0;
            }
            catch
            {
                Log.WriteLine(LogLevel.WARNING, "メモのアドレスが範囲外を指していることを検出しました。無視します。");
                pw.ds_push = 0;
                pw.dx_push = 0;
                return 0;
            }
        }

        private static string getNRDString(ref ushort index)
        {
            if (srcBuf== null || srcBuf.Length < 1 || index < 0 || index >= srcBuf.Length) return "";

            try
            {
                List<byte> lst = new List<byte>();
                for (; srcBuf[index].dat != 0; index++)
                {
                    lst.Add((byte)srcBuf[index].dat);
                }

                string n = System.Text.Encoding.GetEncoding(932).GetString(lst.ToArray());
                index++;

                return n;
            }
            catch (Exception e)
            {
            }
            return "";
        }

        /// <summary>
        /// GD3タグを取得(一般的な曲情報)
        /// </summary>
        public GD3Tag GetGD3TagInfo(byte[] srcBuf)
        {
            List<MmlDatum> sc = new List<MmlDatum>();
            foreach (byte b in srcBuf) sc.Add(new MmlDatum(b));
            Driver.srcBuf = sc.ToArray();
            List<Tuple<string, string>> lstTag = GetTags();
            GD3Tag gd3tag = new GD3Tag();
            gd3tag.dicItem.Clear();
            foreach (Tuple<string, string> ttag in lstTag)
            {
                if (ttag.Item1 == "title")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Title)) gd3tag.dicItem.Remove(enmTag.Title);
                    gd3tag.dicItem.Add(enmTag.Title, new string[] { ttag.Item2 });
                    if (gd3tag.dicItem.ContainsKey(enmTag.TitleJ)) gd3tag.dicItem.Remove(enmTag.TitleJ);
                    gd3tag.dicItem.Add(enmTag.TitleJ, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1 == "composer")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Composer)) gd3tag.dicItem.Remove(enmTag.Composer);
                    gd3tag.dicItem.Add(enmTag.Composer, new string[] { ttag.Item2 });
                    if (gd3tag.dicItem.ContainsKey(enmTag.ComposerJ)) gd3tag.dicItem.Remove(enmTag.ComposerJ);
                    gd3tag.dicItem.Add(enmTag.ComposerJ, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1 == "arranger")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Arranger)) gd3tag.dicItem.Remove(enmTag.Arranger);
                    gd3tag.dicItem.Add(enmTag.Arranger, new string[] { ttag.Item2 });
                    if (gd3tag.dicItem.ContainsKey(enmTag.ArrangerJ)) gd3tag.dicItem.Remove(enmTag.ArrangerJ);
                    gd3tag.dicItem.Add(enmTag.ArrangerJ, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1 == "memo")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Memo)) gd3tag.dicItem.Remove(enmTag.Memo);
                    gd3tag.dicItem.Add(enmTag.Memo, new string[] { ttag.Item2 });
                }
            }
            return gd3tag;

        }

        public object GetWork()
        {
            throw new NotImplementedException();
        }

        public void Init(List<ChipAction> chipsAction, MmlDatum[] srcBuf, Func<string, Stream> appendFileReaderCallback_, object addtionalOption)
        {
            //    throw new NotImplementedException();
            //}

            //public void Init(Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend, MmlDatum[] srcBuf, object addtionalOption)
            //{
            Action<ChipDatum> opnaWrite = chipsAction[0].WriteRegister;
            Action<long, int> opnaWaitSend = chipsAction[0].WaitSend;

            object[] option = (object[])addtionalOption;

            object[] pdnos = (object[])option[0];
            PMDDotNETOption pdno = new PMDDotNETOption()
            {
                isLoadADPCM = (bool)pdnos[0],
                loadADPCMOnly = (bool)pdnos[1],
                isAUTO = (bool)pdnos[2],
                isVA = (bool)pdnos[3],
                isNRM = (bool)pdnos[4],
                usePPS = (bool)pdnos[5],
                usePPZ = (bool)pdnos[6],
                isSPB = (bool)pdnos[7],
                envPmd = (string[])pdnos[8],
                envPmdOpt = (string[])pdnos[9],
                srcFile = (string)pdnos[10],
                PPCHeader = (string)pdnos[11],
                jumpIndex = -1
            };

            Func<string, Stream> appendFileReaderCallback = 
                (pdnos.Length < 13 || pdnos[12] == null) 
                ? CreateAppendFileReaderCallback(Path.GetDirectoryName(pdno.srcFile)) 
                : (Func<string, Stream>)pdnos[12];

            if (pdnos.Length == 14)
            {
                pdno.jumpIndex = (int)pdnos[13];
            }


            string[] po = (string[])option[1];
            Func<ChipDatum, int> ppz8Write = (Func<ChipDatum, int>)option[2];
            Func<ChipDatum, int> ppsdrvWrite = (Func<ChipDatum, int>)option[3];
            Func<ChipDatum, int> p86Write = (Func<ChipDatum, int>)option[4];
            Init(
                srcBuf,
                opnaWrite, opnaWaitSend,
                pdno, po
                , appendFileReaderCallback
                , ppz8Write
                , ppsdrvWrite
                , p86Write);

            pdnos[2] = pdno.isAUTO;
            pdnos[3] = pdno.isVA;
            pdnos[4] = pdno.isNRM;
            pdnos[5] = pdno.usePPS;
            pdnos[6] = pdno.usePPZ;
            pdnos[7] = pdno.isSPB;
        }

        public void Init(
            string fileName,
            Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend,
            PMDDotNETOption addtionalPMDDotNETOption, string[] addtionalPMDOption,
            Func<string, Stream> appendFileReaderCallback,
            Func<ChipDatum, int> ppz8Write,
            Func<ChipDatum, int> ppsdrvWrite,
            Func<ChipDatum, int> p86Write)
        {
            if (Path.GetExtension(fileName).ToLower() != ".xml")
            {
                byte[] srcBuf = File.ReadAllBytes(fileName);
                if (srcBuf == null || srcBuf.Length < 1) return;
                Init(srcBuf, opnaWrite, opnaWaitSend, addtionalPMDDotNETOption, addtionalPMDOption
                    , appendFileReaderCallback ?? CreateAppendFileReaderCallback(Path.GetDirectoryName(fileName))
                    , ppz8Write
                    , ppsdrvWrite
                    , p86Write);
            }
            else
            {
                XmlSerializer serializer = new XmlSerializer(typeof(MmlDatum[]), typeof(MmlDatum[]).GetNestedTypes());
                using (StreamReader sr = new StreamReader(fileName, new UTF8Encoding(false)))
                {
                    MmlDatum[] s = (MmlDatum[])serializer.Deserialize(sr);
                    Init(s, opnaWrite, opnaWaitSend, addtionalPMDDotNETOption, addtionalPMDOption
                        , appendFileReaderCallback ?? CreateAppendFileReaderCallback(Path.GetDirectoryName(fileName))
                        , ppz8Write
                        , ppsdrvWrite
                        , p86Write
                        );
                }

            }
        }

        public void resetOption(string[] pmdOption)
        {
            pmd.resetOption(pmdOption);
        }

        public void Init(
            byte[] srcBuf,
            Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend,
            PMDDotNETOption addtionalPMDDotNETOption, string[] addtionalPMDOption,
            Func<string, Stream> appendFileReaderCallback,
            Func<ChipDatum, int> ppz8Write,
            Func<ChipDatum, int> ppsdrvWrite,
            Func<ChipDatum, int> p86Write)
        {
            if (srcBuf == null || srcBuf.Length < 1) return;
            List<MmlDatum> bl = new List<MmlDatum>();
            foreach (byte b in srcBuf) bl.Add(new MmlDatum(b));
            Init(bl.ToArray(), opnaWrite, opnaWaitSend, addtionalPMDDotNETOption, addtionalPMDOption
                        , appendFileReaderCallback
                        , ppz8Write
                        , ppsdrvWrite
                        , p86Write
                        );
        }


        public void Init(
            MmlDatum[] srcBuf,
            Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend,
            PMDDotNETOption addtionalPMDDotNETOption, string[] addtionalPMDOption,
            Func<string, Stream> appendFileReaderCallback
            ,Func<ChipDatum,int> ppz8Write            , Func<ChipDatum, int> ppsdrvWrite, Func<ChipDatum, int> p86Write)
        {
            if (srcBuf == null || srcBuf.Length < 1) return;

            Driver.srcBuf = srcBuf;

            WriteOPNA = opnaWrite;
            WaitSendOPNA = opnaWaitSend;
            WritePPZ8 = ppz8Write;
            WritePPSDRV = ppsdrvWrite;
            WriteP86 = p86Write;

            work = new PW();
            GetTags();
            addtionalPMDDotNETOption.PPCHeader = CheckPPC(appendFileReaderCallback);

            work.SetOption(addtionalPMDDotNETOption, addtionalPMDOption);
            work.timer = new OPNATimer(44100, 7987200);

            //PPZ8em ppz8em = addtionalPMDDotNETOption.ppz8em;
            //PPSDRV ppsdrv = addtionalPMDDotNETOption.ppsdrv;

            pmd = new PMD(
                srcBuf,
                WriteRegister,
                work,
                appendFileReaderCallback,
                WritePPZ8,
                WritePPSDRV,
                WriteP86
                );

            if (!string.IsNullOrEmpty(pmd.pw.ppcFile)) pmd.pcmload.pcm_all_load(pmd.pw.ppcFile);
            if (!string.IsNullOrEmpty(pmd.pw.ppz1File) || !string.IsNullOrEmpty(pmd.pw.ppz2File)) pmd.pcmload.ppz_load(pmd.pw.ppz1File, pmd.pw.ppz2File);
            if (!string.IsNullOrEmpty(pmd.pw.ppsFile)) pmd.pcmload.pps_load(pmd.pw.ppsFile);

        }

        private string CheckPPC(Func<string, Stream> appendFileReaderCallback)
        {
            if (string.IsNullOrEmpty(work.ppcFile))
            {
                return "";
            }

            byte[] buf = null;
            string ext = Path.GetExtension(work.ppcFile);
            string fn = work.ppcFile;
            int extn = 0;
            string[] ppcExtTbl = new string[] { ".PPC", ".P86", ".PVI" };
            while (true)
            {
                buf = Common.Common.GetPCMDataFromFile(work.ppcFile, appendFileReaderCallback);
                if (buf != null) break;
                if (extn == 3) break;
                extn++;
                fn = Path.ChangeExtension(fn, ppcExtTbl[extn - 1]);
            }
            if (buf == null) return "";
            if (buf.Length < 3) return "";

            string head = string.Format("{0}{1}{2}", (char)buf[0], (char)buf[1], (char)buf[2]);
            return head;
        }

        public void MusicSTART(int musicNumber)
        {
            Log.WriteLine(LogLevel.DEBUG, "演奏開始");
            pmd.int60_main(0);
            work.Status = 1;
        }

        public void MusicSTOP()
        {
            Log.WriteLine(LogLevel.DEBUG, "演奏停止");
            pmd.int60_main(0x0100);
            work.Status = 0;
        }

        public void dispStatus()
        {
            pmd.int60_main(5);
            int syosetu = pmd.pw.al_push + pmd.pw.ah_push * 0x100;
#if DEBUG
            Log.WriteLine(LogLevel.TRACE, string.Format("小節:{0}", syosetu));
#endif
        }

        public void Rendering()
        {
            if (work.Status < 0) return;

            try
            {
                pmd.Rendering();
            }
            catch(Exception e)
            {
                renderingException = e;
                work.Status = -1;
                throw;
            }
        }

        public int SetLoopCount(int loopCounter)
        {
            //throw new NotImplementedException();
            return 0;
        }

        public void ShotEffect()
        {
            throw new NotImplementedException();
        }

        public void StartRendering(int renderingFreq, Tuple<string, int>[] chipsMasterClock)
        {
            lock (work.SystemInterrupt)
            {

                work.timeCounter = 0L;
                this.renderingFreq = renderingFreq <= 0 ? 44100 : renderingFreq;
                this.opnaMasterClock = 7987200;
                if (chipsMasterClock != null && chipsMasterClock.Length > 0)
                {
                    this.opnaMasterClock = chipsMasterClock[0].Item2 <= 0 ? 7987200 : chipsMasterClock[0].Item2;
                }
                work.timer.setClock(renderingFreq, opnaMasterClock);
#if DEBUG
                Log.WriteLine(LogLevel.TRACE, "Start rendering.");
#endif
            }
        }

        public void StopRendering()
        {
            lock (work.SystemInterrupt)
            {
                if (work.Status > 0) work.Status = 0;
#if DEBUG
                Log.WriteLine(LogLevel.TRACE, "Stop rendering.");
#endif
            }
        }

        public void WriteRegister(ChipDatum reg)
        {
            if (work == null) return;
            lock (lockObjWriteReg)
            {
                if (reg.port == 0) { work.timer?.WriteReg((byte)reg.address, (byte)reg.data); }
                WriteOPNA(reg);
            }
        }

        //public int GetNowLoopCounter()
        //{
        //    //throw new NotImplementedException();
        //    return 0;
        //}


        private static Func<string, Stream> CreateAppendFileReaderCallback(string dir)
        {
            return fname =>
            {
                if (!string.IsNullOrEmpty(dir))
                {
                    var path = Path.Combine(dir, fname);
                    if (File.Exists(path))
                    {
                        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }
                if (File.Exists(fname))
                {
                    return new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                return null;
            };
        }

        public void SetDriverSwitch(params object[] param)
        {
            throw new NotImplementedException();
        }

    }
}
