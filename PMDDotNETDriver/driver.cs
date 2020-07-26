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
            pmd.int60_main(0x0a00);
            return pmd.pw.status2 != 0xff ? 1 : 0;
        }

        /// <summary>
        /// ドライバ固有のタグを取得
        /// </summary>
        public List<Tuple<string, string>> GetTags()
        {
            //throw new NotImplementedException();
            return null;
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

        public void Init(string fileName, Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend, bool notSoundBoard2, bool isLoadADPCM, bool loadADPCMOnly, Func<string, Stream> appendFileReaderCallback = null)
        {
            if (Path.GetExtension(fileName).ToLower() != ".xml")
            {
                byte[] srcBuf = File.ReadAllBytes(fileName);
                if (srcBuf == null || srcBuf.Length < 1) return;
                Init(opnaWrite, opnaWaitSend, notSoundBoard2, srcBuf, isLoadADPCM, loadADPCMOnly, appendFileReaderCallback ?? CreateAppendFileReaderCallback(Path.GetDirectoryName(fileName)));
            }
            else
            {
                XmlSerializer serializer = new XmlSerializer(typeof(MmlDatum[]), typeof(MmlDatum[]).GetNestedTypes());
                using (StreamReader sr = new StreamReader(fileName, new UTF8Encoding(false)))
                {
                    MmlDatum[] s = (MmlDatum[])serializer.Deserialize(sr);
                    Init(opnaWrite, opnaWaitSend, s, new object[] { notSoundBoard2, isLoadADPCM, loadADPCMOnly }, appendFileReaderCallback);
                }

            }
        }

        public void Init(Action<ChipDatum> opnaWrite, Action<long, int> opnaWaitSend, bool notSoundBoard2, byte[] srcBuf, bool isLoadADPCM, bool loadADPCMOnly, Func<string, Stream> appendFileReaderCallback)
        {
            if (srcBuf == null || srcBuf.Length < 1) return;
            List<MmlDatum> bl = new List<MmlDatum>();
            foreach (byte b in srcBuf) bl.Add(new MmlDatum(b));
            Init(opnaWrite, opnaWaitSend, bl.ToArray(), new object[] { notSoundBoard2, isLoadADPCM, loadADPCMOnly }, appendFileReaderCallback);
        }

        public void Init(string fileName, Action<ChipDatum> chipWriteRegister, Action<long, int> chipWaitSend, MmlDatum[] srcBuf, object addtionalOption)
        {
            throw new NotImplementedException();
        }

        public void Init(Action<ChipDatum> chipWriteRegister, Action<long, int> chipWaitSend, MmlDatum[] srcBuf, object addtionalOption, Func<string, Stream> appendFileReaderCallback)
        {
            if (srcBuf == null || srcBuf.Length < 1) return;

            bool notSoundBoard2 = (bool)((object[])addtionalOption)[0];
            bool isLoadADPCM = (bool)((object[])addtionalOption)[1];
            bool loadADPCMOnly = (bool)((object[])addtionalOption)[2];

            pmd = new PMD(srcBuf, WriteRegister, !notSoundBoard2, false);
            work = pmd.pw;
            work.board = 1;//音源あり
            //ポート番号の指定
            work.fm1_port1 = 0x188;//レジスタ
            work.fm1_port2 = 0x18a;//データ
            work.fm2_port1 = 0x18c;//レジスタ(拡張)
            work.fm2_port2 = 0x18e;//データ(拡張)

            WriteOPNA = chipWriteRegister;
            WaitSendOPNA = chipWaitSend;
        }

        public void MusicSTART(int musicNumber)
        {
            Log.WriteLine(LogLevel.INFO, "演奏開始");
            pmd.int60_main(0);
            work.Status = 1;
        }

        public void MusicSTOP()
        {
            Log.WriteLine(LogLevel.INFO, "演奏停止");
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
                work.timer = new OPNATimer(renderingFreq, opnaMasterClock);
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
