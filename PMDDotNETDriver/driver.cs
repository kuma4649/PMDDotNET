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

        /// <summary>
        /// ドライバ固有のタグを取得
        /// </summary>
        public List<Tuple<string, string>> GetTags()
        {
            throw new NotImplementedException();
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

        public void StartRendering(int renderingFreq, Tuple<string, int>[] chipsMasterClock)
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
