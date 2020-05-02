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
            throw new NotImplementedException();
        }

        public List<Tuple<string, string>> GetTags()
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

            //bool notSoundBoard2 = (bool)((object[])addtionalOption)[0];
            //bool isLoadADPCM = (bool)((object[])addtionalOption)[1];
            //bool loadADPCMOnly = (bool)((object[])addtionalOption)[2];

            //work = new Work();
            //header = new MUBHeader(srcBuf, enc);
            //work.mData = GetDATA();
            //tags = GetTags();
            //GetFileNameFromTag();
            //work.fmVoice = GetFMVoiceFromFile(appendFileReaderCallback);
            //pcm = GetPCMFromSrcBuf() ?? GetPCMDataFromFile(appendFileReaderCallback);
            //work.pcmTables = GetPCMTable();
            //work.isDotNET = IsDotNETFromTAG();

            //WriteOPNA = chipWriteRegister;
            //WaitSendOPNA = chipWaitSend;

            ////PCMを送信する
            //if (pcm != null)
            //{
            //    if (isLoadADPCM)
            //    {
            //        ChipDatum[] pcmSendData = GetPCMSendData();

            //        var sw = new System.Diagnostics.Stopwatch();
            //        sw.Start();
            //        foreach (ChipDatum dat in pcmSendData) { WriteRegister(dat); }
            //        sw.Stop();

            //        WaitSendOPNA(sw.ElapsedMilliseconds, pcmSendData.Length);
            //    }
            //}

            //if (loadADPCMOnly) return;

            //music2 = new Music2(work, WriteRegister);
            //music2.notSoundBoard2 = notSoundBoard2;
        }

        public void MusicSTART(int musicNumber)
        {
            throw new NotImplementedException();
        }

        public void MusicSTOP()
        {
            throw new NotImplementedException();
        }

        public void Rendering()
        {
            throw new NotImplementedException();
        }

        public int SetLoopCount(int loopCounter)
        {
            throw new NotImplementedException();
        }

        public void ShotEffect()
        {
            throw new NotImplementedException();
        }

        public void StartRendering(int renderingFreq = 44100, int chipMasterClock = 7987200)
        {
            throw new NotImplementedException();
        }

        public void StopRendering()
        {
            throw new NotImplementedException();
        }

        public void WriteRegister(ChipDatum reg)
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
