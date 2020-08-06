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
        private Action<long, int> WaitSendOPNA;
        private object lockObjWriteReg = new object();
        static MmlDatum[] srcBuf = null;


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

        /// <summary>
        /// ドライバ固有のタグを取得
        /// </summary>
        public List<Tuple<string, string>> GetTags()
        {
            List<Tuple<string, string>> tags = new List<Tuple<string, string>>();

            ushort adr = pmd.get_memo(1);
            string str = getNRDString(ref adr);
            tags.Add(new Tuple<string, string>("title", str));

            adr = pmd.get_memo(2);
            str = getNRDString(ref adr);
            tags.Add(new Tuple<string, string>("composer", str));

            adr = pmd.get_memo(3);
            str = getNRDString(ref adr);
            tags.Add(new Tuple<string, string>("arranger", str));
            int al = 4;
            str = "";
            do
            {
                adr = pmd.get_memo(al);
                if (adr != 0) str += "\r\n" + getNRDString(ref adr);
                al++;
            } while (adr != 0);
            str = str != "" ? str.Substring(2) : "";
            tags.Add(new Tuple<string, string>("memo", str));

            adr = pmd.get_memo(0);
            str = getNRDString(ref adr);
            tags.Add(new Tuple<string, string>("PCMFile", str));
            pmd.pw.ppcFile = str.Trim();

            adr = pmd.get_memo(-1);
            str = getNRDString(ref adr);
            tags.Add(new Tuple<string, string>("PPSFile", str));
            pmd.pw.ppsFile = str.Trim();

            adr = pmd.get_memo(-2);
            str = getNRDString(ref adr);
            tags.Add(new Tuple<string, string>("PPZFile", str));
            pmd.pw.ppz1File = str.Trim();
            string[] p = pmd.pw.ppz1File.Split(',');
            if (p.Length > 1)
            {
                pmd.pw.ppz1File = p[0];
                pmd.pw.ppz2File = p[1];
            }

            return tags;
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
            throw new NotImplementedException();
        }

        public object GetWork()
        {
            throw new NotImplementedException();
        }

        public void Init(string fileName, Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend, MmlDatum[] srcBuf, object addtionalOption)
        {
            throw new NotImplementedException();
        }

        public void Init(
            string fileName,
            Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend,
            PMDDotNETOption addtionalPMDDotNETOption, string[] addtionalPMDOption,
            Func<string, Stream> appendFileReaderCallback = null)
        {
            if (Path.GetExtension(fileName).ToLower() != ".xml")
            {
                byte[] srcBuf = File.ReadAllBytes(fileName);
                if (srcBuf == null || srcBuf.Length < 1) return;
                Init(srcBuf, opnaWrite, opnaWaitSend, addtionalPMDDotNETOption, addtionalPMDOption, appendFileReaderCallback ?? CreateAppendFileReaderCallback(Path.GetDirectoryName(fileName)));
            }
            else
            {
                XmlSerializer serializer = new XmlSerializer(typeof(MmlDatum[]), typeof(MmlDatum[]).GetNestedTypes());
                using (StreamReader sr = new StreamReader(fileName, new UTF8Encoding(false)))
                {
                    MmlDatum[] s = (MmlDatum[])serializer.Deserialize(sr);
                    Init(s, opnaWrite, opnaWaitSend, addtionalPMDDotNETOption, addtionalPMDOption, appendFileReaderCallback);
                }

            }
        }

        public void Init(
            byte[] srcBuf,
            Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend,
            PMDDotNETOption addtionalPMDDotNETOption, string[] addtionalPMDOption,
            Func<string, Stream> appendFileReaderCallback)
        {
            if (srcBuf == null || srcBuf.Length < 1) return;
            List<MmlDatum> bl = new List<MmlDatum>();
            foreach (byte b in srcBuf) bl.Add(new MmlDatum(b));
            Init(bl.ToArray(), opnaWrite, opnaWaitSend, addtionalPMDDotNETOption, addtionalPMDOption, appendFileReaderCallback);
        }

        public void Init(
            MmlDatum[] srcBuf,
            Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend,
            PMDDotNETOption addtionalPMDDotNETOption, string[] addtionalPMDOption,
            Func<string, Stream> appendFileReaderCallback)
        {
            if (srcBuf == null || srcBuf.Length < 1) return;

            Driver.srcBuf = srcBuf;

            WriteOPNA = opnaWrite;
            WaitSendOPNA = opnaWaitSend;
            work = new PW(addtionalPMDDotNETOption, addtionalPMDOption);
            work.timer = new OPNATimer(44100, 7987200);
            PPZ8em ppz8em = addtionalPMDDotNETOption.ppz8em;
            pmd = new PMD(srcBuf, WriteRegister, work, appendFileReaderCallback, ppz8em);

            GetTags();

            if (!string.IsNullOrEmpty(pmd.pw.ppcFile)) pmd.pcmload.pcm_all_load(pmd.pw.ppcFile);
            if (!string.IsNullOrEmpty(pmd.pw.ppz1File) || !string.IsNullOrEmpty(pmd.pw.ppz2File)) pmd.pcmload.ppz_load(pmd.pw.ppz1File, pmd.pw.ppz2File);
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
            catch
            {
                work.Status = -1;
                throw;
            }
        }

        public int SetLoopCount(int loopCounter)
        {
            throw new NotImplementedException();
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

        public int GetNowLoopCounter()
        {
            throw new NotImplementedException();
        }


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

    }
}
