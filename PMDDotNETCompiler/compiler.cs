using musicDriverInterface;
using PMDDotNET.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace PMDDotNET.Compiler
{
    public class Compiler : iCompiler
    {
        public iEncoding enc = null;
        private string srcBuf = null;
        private bool isIDE=false;
        private Point skipPoint=Point.Empty;
        public string[] mcArgs = null;
        public string[] env = null;
        private Func<string, Stream> appendFileReaderCallback;
        private work work = null;
        private byte[] ffBuf = null;


        public Compiler(iEncoding enc = null)
        {
            this.enc = enc ?? myEncoding.Default;
        }

        public void Init()
        {
        }

        public void SetCompileSwitch(params object[] param)
        {
            this.isIDE = false;
            this.skipPoint = Point.Empty;

            if (param == null) return;

            foreach (object prm in param)
            {
                if (!(prm is string)) continue;

                //IDEフラグオン
                if ((string)prm == "IDE")
                {
                    this.isIDE = true;
                }

                //スキップ再生指定
                if (((string)prm).IndexOf("SkipPoint=") == 0)
                {
                    try
                    {
                        string[] p = ((string)prm).Split('=')[1].Split(':');
                        int r = int.Parse(p[0].Substring(1));
                        int c = int.Parse(p[1].Substring(1));
                        this.skipPoint = new Point(c, r);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        public MmlDatum[] Compile(Stream sourceMML, Func<string, Stream> appendFileReaderCallback)
        {
            this.appendFileReaderCallback = appendFileReaderCallback;

            try
            {
                using (StreamReader sr = new StreamReader(sourceMML, Encoding.GetEncoding("Shift_JIS")))
                {
                    srcBuf = sr.ReadToEnd();
                }
                work = new work();
                mc mc = new mc(this, mcArgs, srcBuf, ffBuf, work, env);
                return mc.compile_start();
                
            }
            catch(PmdDosExitException)
            {
                ;
            }
            catch (PmdErrorExitException peee)
            {
                work.compilerInfo.errorList.Add(new Tuple<int, int, string>(-1, -1, peee.Message));
                Log.WriteLine(LogLevel.ERROR, peee.Message);
            }
            catch (PmdException pe)
            {
                work.compilerInfo.errorList.Add(new Tuple<int, int, string>(-1, -1, pe.Message));
                Log.WriteLine(LogLevel.ERROR, pe.Message);
            }
            catch (Exception e)
            {
                work.compilerInfo.errorList.Add(new Tuple<int, int, string>(-1, -1, e.Message));
                Log.WriteLine(LogLevel.ERROR, string.Format(
                    msg.get("E0000")
                    , e.Message
                    , e.StackTrace));
            }

            return null;
        }

        public CompilerInfo GetCompilerInfo()
        {
            throw new NotImplementedException();
        }


        public bool Compile(Stream sourceMML, Stream destCompiledBin, Func<string, Stream> appendFileReaderCallback)
        {
            var dat = Compile(sourceMML, appendFileReaderCallback);
            if (dat == null)
            {
                return false;
            }
            foreach (MmlDatum md in dat)
            {
                if (md == null)
                {
                    destCompiledBin.WriteByte(0);
                }
                else
                {
                    destCompiledBin.WriteByte((byte)md.dat);
                }
            }
            return true;
        }

        /// <summary>
		/// ストリームから一括でバイナリを読み込む
		/// </summary>
		private byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null) return null;

            var buf = new byte[8192];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var r = stream.Read(buf, 0, buf.Length);
                    if (r < 1)
                    {
                        break;
                    }
                    ms.Write(buf, 0, r);
                }
                return ms.ToArray();
            }
        }

        internal byte[] ReadFile(string v_filename)
        {
            Stream strm = appendFileReaderCallback(v_filename);
            return ReadAllBytes(strm);
        }

        internal string ReadFileText(string mml_filename2)
        {
            Stream strm = appendFileReaderCallback(mml_filename2);
            string text;
            using (StreamReader sr = new StreamReader(strm, Encoding.GetEncoding("Shift_JIS")))
            {
                text = sr.ReadToEnd();
            }

            return text;
        }

        public void SetFfFileBuf(byte[] ffFileBuf)
        {
            ffBuf = ffFileBuf;
        }
    }
}
