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


        //入力データ

        public iEncoding enc = null;
        public string[] mcArgs = null;
        public string[] env = null;



        //出力データ

        public int memo_writeAddress { get; private set; } = -1;
        public int vdat_setAddress { get; private set; } = -1;
        public mml_seg mml_seg = null;
        public voice_seg voice_seg = null;
        public byte[] outFFFileBuf { get; private set; } = null;
        public string outFFFileName { get; private set; } = null;
        public int skipIndex = -1;//スキップ位置

        //内部
        private string srcBuf = null;
        private bool isIDE = false;
        private Point skipPoint = Point.Empty;
        private Func<string, Stream> appendFileReaderCallback;
        private work work = null;
        private byte[] ffBuf = null;



        public Compiler(iEncoding enc = null)
        {
            this.enc = enc ?? myEncoding.Default;
        }

        public void Init()
        {
            this.isIDE = false;
            this.skipPoint = Point.Empty;
        }

        public void SetCompileSwitch(params object[] param)
        {

            if (param == null) return;

            foreach (object prm in param)
            {
                if(prm is Func<string, Stream>)
                {
                    appendFileReaderCallback=(Func<string, Stream>)prm;
                    continue;
                }

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

                //PMD option 指定
                if (((string)prm).IndexOf("PmdOption=") == 0)
                {
                    try
                    {
                        string[] p = ((string)prm).Split('=')[1].Split(' ');
                        mcArgs = p;
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
            using (var ms = ReadAllBytesToMemoryStream(sourceMML))
            {
                ms.Seek(0, SeekOrigin.Begin);
                int c = 0;
                int offset = 0;
                while ((c = ms.ReadByte()) >= 0)
                {
                    if (c == 0x1a)
                    {
                        ms.SetLength(offset);
                        break;
                    }
                    offset++;
                }
                ms.Seek(0, SeekOrigin.Begin);

                using (StreamReader sr = new StreamReader(ms, Encoding.GetEncoding("Shift_JIS")))
                {
                    srcBuf = sr.ReadToEnd();
                }
            }

            //Console.WriteLine(srcBuf);

            this.appendFileReaderCallback = appendFileReaderCallback;

            try
            {
                work = new work();
                work.isIDE = isIDE;
                mc mc = new mc(this, mcArgs, srcBuf, ffBuf, work, env);

                mc.skipPoint = this.skipPoint;
                MmlDatum[] ret = mc.compile_start();
                memo_writeAddress = mc.memo_writeAddress;
                vdat_setAddress = mc.vdat_setAddress;
                mml_seg = mc.mml_seg;
                voice_seg = mc.voice_seg;
                work.compilerInfo.jumpClock = -1;
                if (mc.skipSW == 3)
                {
                    skipIndex = mc.skipIndex + 1;//ひとつずらす
                    work.compilerInfo.jumpClock = skipIndex;
                }

                outFFFileBuf = null; if (mc.outVoiceBuf != null)
                {
                    outFFFileBuf = mc.outVoiceBuf;
                    outFFFileName= voice_seg.v_filename;
                }

                //foreach (MmlDatum d in ret)
                //{
                //    if (d.type == enmMMLType.Note)
                //    {
                //        Console.WriteLine("{0} {1}", d.linePos.row, d.linePos.col);
                //        ;
                //    }
                //}

                return ret;
                
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

        public CompilerInfo GetCompilerInfo()
        {
            return work.compilerInfo;
        }

        public Tuple<string, string>[] GetTags(string srcBuf, Func<string, Stream> appendFileReaderCallback)
        {
            this.appendFileReaderCallback = appendFileReaderCallback;
            List<string> lstTag = new List<string>();
            List<Tuple<string, string>> tags = new List<Tuple<string, string>>();

            try
            {
                string[] srcList = srcBuf.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string lin in srcList)
                {
                    if (string.IsNullOrEmpty(lin)) continue;
                    if (lin[0] != '#') continue;

                    lstTag.Add(lin);
                    if (lin.ToUpper().IndexOf("#INCLUDE") != 0) continue;

                    GetTagReca(lstTag, lin);
                }

                foreach (string tag in lstTag)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    int i = 0;
                    for (; i < tag.Length; i++)
                    {
                        if (tag[i] == '\t' || tag[i] == ' ') break;
                    }
                    if (i == tag.Length) continue;

                    string k = tag.Substring(0, i).Trim();
                    string v = tag.Substring(i + 1).Trim();
                    if (string.IsNullOrEmpty(k)) continue;
                    if (string.IsNullOrEmpty(v)) continue;

                    Tuple<string, string> keyVal = new Tuple<string, string>(k, v);
                    tags.Add(keyVal);
                }
            }
            catch 
            {
                ;
            }

            return tags.ToArray();
        }

        public void SetFfFileBuf(byte[] ffFileBuf)
        {
            ffBuf = ffFileBuf;
        }



        internal byte[] ReadFile(string filename)
        {
            Stream strm = appendFileReaderCallback(filename);
            return ReadAllBytes(strm);
        }

        internal string ReadFileText(string mml_filename2)
        {
            Stream strm = appendFileReaderCallback(mml_filename2);
            if (strm == null)
            {
                Log.WriteLine(LogLevel.ERROR, string.Format(msg.get("E0201"), mml_filename2));
                throw new FileNotFoundException(mml_filename2);
                //return "";
            }
            string text;
            using (StreamReader sr = new StreamReader(strm, Encoding.GetEncoding("Shift_JIS")))
            {
                text = sr.ReadToEnd();
            }

            return text;
        }



        private void GetTagReca(List<string> lstTag, string lin)
        {
            if (lin.Length < 9) return;

            string inc = ReadFileText(lin.Substring(8).Trim());
            if (string.IsNullOrEmpty(inc)) return;

            string[] incList = inc.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string ilin in incList)
            {
                if (string.IsNullOrEmpty(ilin)) continue;
                if (ilin[0] != '#') continue;

                lstTag.Add(ilin);
                if (ilin.ToUpper().IndexOf("#INCLUDE") != 0) continue;

                GetTagReca(lstTag, ilin);
            }
        }

        /// <summary>
		/// ストリームから一括でバイナリを読み込む
		/// </summary>
		private byte[] ReadAllBytes(Stream stream)
        {
            using (var ms = ReadAllBytesToMemoryStream(stream))
            {
                return ms?.ToArray();
            }
        }

		private MemoryStream ReadAllBytesToMemoryStream(Stream stream)
        {
            if (stream == null) return null;

            var buf = new byte[8192];
            var ms = new MemoryStream();
            while (true)
            {
                var r = stream.Read(buf, 0, buf.Length);
                if (r < 1)
                {
                    break;
                }
                ms.Write(buf, 0, r);
            }
            return ms;
        }

        public GD3Tag GetGD3TagInfo(byte[] srcBuf)
        {
            string text = System.Text.Encoding.GetEncoding("shift_jis").GetString(srcBuf);
            Tuple<string, string>[] tags = GetTags(text, appendFileReaderCallback);
            GD3Tag gd3tag = new GD3Tag();
            gd3tag.dicItem.Clear();
            foreach (Tuple<string, string> ttag in tags)
            {
                if (ttag.Item1.ToLower().Trim() == "#title")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Title)) gd3tag.dicItem.Remove(enmTag.Title);
                    gd3tag.dicItem.Add(enmTag.Title, new string[] { ttag.Item2 });
                    if (gd3tag.dicItem.ContainsKey(enmTag.TitleJ)) gd3tag.dicItem.Remove(enmTag.TitleJ);
                    gd3tag.dicItem.Add(enmTag.TitleJ, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1.ToLower().Trim() == "#composer")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Composer)) gd3tag.dicItem.Remove(enmTag.Composer);
                    gd3tag.dicItem.Add(enmTag.Composer, new string[] { ttag.Item2 });
                    if (gd3tag.dicItem.ContainsKey(enmTag.ComposerJ)) gd3tag.dicItem.Remove(enmTag.ComposerJ);
                    gd3tag.dicItem.Add(enmTag.ComposerJ, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1.ToLower().Trim() == "#arranger")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Arranger)) gd3tag.dicItem.Remove(enmTag.Arranger);
                    gd3tag.dicItem.Add(enmTag.Arranger, new string[] { ttag.Item2 });
                    if (gd3tag.dicItem.ContainsKey(enmTag.ArrangerJ)) gd3tag.dicItem.Remove(enmTag.ArrangerJ);
                    gd3tag.dicItem.Add(enmTag.ArrangerJ, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1.ToLower().Trim() == "#memo")
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.Memo)) gd3tag.dicItem.Remove(enmTag.Memo);
                    gd3tag.dicItem.Add(enmTag.Memo, new string[] { ttag.Item2 });
                }
                else if (ttag.Item1.ToLower().Trim().IndexOf("#fi") != -1)
                {
                    if (gd3tag.dicItem.ContainsKey(enmTag.SongObjFilename)) gd3tag.dicItem.Remove(enmTag.SongObjFilename);
                    gd3tag.dicItem.Add(enmTag.SongObjFilename, new string[] { ttag.Item2 });
                }
            }
            return gd3tag;
        }
    }
}
