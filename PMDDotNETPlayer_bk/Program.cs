using PMDDotNET.Common;
using musicDriverInterface;
using NAudio.Wave;
using Nc86ctl;
using NScci;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;
using PMDDotNET.Driver;
using System.Linq;

namespace PMDDotNET.Player
{
    class Program
    {
        private static DirectSoundOut audioOutput = null;
        public delegate int naudioCallBack(short[] buffer, int offset, int sampleCount);
        private static naudioCallBack callBack = null;
        private static Thread trdMain = null;
        private static Stopwatch sw = null;
        private static double swFreq = 0;
        public static bool trdClosed = false;
        private static object lockObj = new object();
        private static bool _trdStopped = true;
        public static bool trdStopped
        {
            get
            {
                lock (lockObj)
                {
                    return _trdStopped;
                }
            }
            set
            {
                lock (lockObj)
                {
                    _trdStopped = value;
                }
            }
        }
        private static readonly uint SamplingRate = 55467;//44100;
        private static readonly uint SamplingRatePPSGIMIC = 44100;
        private static readonly uint SamplingRatePPSSCCI = 16000;
        private static readonly uint samplingBuffer = 1024;
        private static short[] frames = new short[samplingBuffer * 4];
        private static MDSound.MDSound mds = null;
        private static short[] emuRenderBuf = new short[2];
        private static musicDriverInterface.iDriver drv = null;
        private static readonly uint opnaMasterClock = 7987200;
        private static int device = 0;
        private static int loop = 0;
        private static NScci.NScci nScci;
        private static Nc86ctl.Nc86ctl nc86ctl;
        private static RSoundChip rsc;

        private static bool isAUTO = true;
        private static bool isNRM = true;
        private static bool isSPB = true;
        private static bool isVA = false;
        private static bool usePPS = false;
        private static bool usePPZ = false;
        private static int[] VolumeV = null;
        private static int[] VolumeR = null;
        private static bool isGimicOPNA = false;
        private static MDSound.PPZ8 ppz8em = null;
        private static MDSound.PPSDRV ppsdrv = null;
        private static MDSound.P86 p86em = null;
        private static string[] envPmd = null;
        private static string[] envPmdOpt = null;
        private static string srcFile = null;
        private static int userPPSFREQ = -1;
        private static int ppsdrvWait = 1;

        static int Main(string[] args)
        {
            Log.writeLine += WriteLine;
#if DEBUG
            //Log.writeLine += WriteLineF;
            Log.level = LogLevel.INFO;
#else
            Log.level = LogLevel.INFO;
#endif
            int fnIndex = AnalyzeOption(args);
            int mIndex = -1;

            if (args != null)
            {
                for (int i = fnIndex; i < args.Length; i++)
                {
                    if ((Path.GetExtension(args[i]).ToUpper().IndexOf(".M") < 0)
                        && (Path.GetExtension(args[i]).ToUpper().IndexOf(".XML") < 0)
                        ) continue;
                    mIndex = i;
                    break;
                }
            }

            if (mIndex<0)
            {
                Log.WriteLine(LogLevel.INFO, "引数(.Mファイル)１個欲しいよぉ...");
                return -1;
            }

            srcFile = args[mIndex];

            if (!File.Exists(args[mIndex]))
            {
                Log.WriteLine(LogLevel.ERROR, string.Format("ファイル[{0}]が見つかりません", args[mIndex]));
                return -1;
            }

            rsc = CheckDevice();

            try
            {

                SineWaveProvider16 waveProvider;
                int latency = 1000;

                switch (device)
                {
                    case 0:
                        waveProvider = new SineWaveProvider16();
                        waveProvider.SetWaveFormat((int)SamplingRate, 2);
                        callBack = EmuCallback;
                        audioOutput = new DirectSoundOut(latency);
                        audioOutput.Init(waveProvider);
                        break;
                    case 1:
                    case 2:
                        trdMain = new Thread(new ThreadStart(RealCallback));
                        trdMain.Priority = ThreadPriority.Highest;
                        trdMain.IsBackground = true;
                        trdMain.Name = "trdVgmReal";
                        sw = Stopwatch.StartNew();
                        swFreq = Stopwatch.Frequency;
                        break;
                }

                MDSound.ym2608 ym2608 = new MDSound.ym2608();
                MDSound.MDSound.Chip chip = new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2608,
                    ID = 0,
                    Instrument = ym2608,
                    Update = ym2608.Update,
                    Start = ym2608.Start,
                    Stop = ym2608.Stop,
                    Reset = ym2608.Reset,
                    SamplingRate = SamplingRate,
                    Clock = opnaMasterClock,
                    Volume = 0,
                    Option = new object[] { GetApplicationFolder() }
                };

                ppz8em = new MDSound.PPZ8();
                MDSound.MDSound.Chip chipp = new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.PPZ8,
                    ID = 0,
                    Instrument = ppz8em,
                    Update = ppz8em.Update,
                    Start = ppz8em.Start,
                    Stop = ppz8em.Stop,
                    Reset = ppz8em.Reset,
                    SamplingRate = SamplingRate,
                    Clock = opnaMasterClock,
                    Volume = 0,
                    Option = null
                };

                ppsdrv = new MDSound.PPSDRV();
                MDSound.MDSound.Chip chipps = new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.PPSDRV,
                    ID = 0,
                    Instrument = ppsdrv,
                    Update = ppsdrv.Update,
                    Start = ppsdrv.Start,
                    Stop = ppsdrv.Stop,
                    Reset = ppsdrv.Reset,
                    SamplingRate = (uint)
                        (
                            device == 0 
                            ? SamplingRate 
                            : (
                                userPPSFREQ == -1
                                ? (
                                device == 1 
                                ? SamplingRatePPSGIMIC 
                                : SamplingRatePPSSCCI
                                )
                                : (uint)userPPSFREQ
                            )
                        ),
                    Clock = opnaMasterClock,
                    Volume = 0,
                    Option = device == 0 ? null : (new object[] { (Action<int, int>)PPSDRVpsg })
                };

                p86em = new MDSound.P86();
                MDSound.MDSound.Chip chip86 = new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.mpcmX68k,//TBD
                    ID = 0,
                    Instrument = p86em,
                    Update = p86em.Update,
                    Start = p86em.Start,
                    Stop = p86em.Stop,
                    Reset = p86em.Reset,
                    SamplingRate = SamplingRate,
                    Clock = opnaMasterClock,
                    Volume = 0,
                    Option = null
                };


                mds = new MDSound.MDSound(SamplingRate, samplingBuffer, new MDSound.MDSound.Chip[] { chip, chipp, chipps, chip86 });
                //ppz8em = new PPZ8em(SamplingRate);
                //ppsdrv = new PPSDRV(SamplingRate);



                Common.Environment env = new Common.Environment();
                env.AddEnv("pmd");
                env.AddEnv("pmdopt");
                envPmd = env.GetEnvVal("pmd");
                envPmdOpt = env.GetEnvVal("pmdopt");

                List<string> opt = (envPmdOpt==null) ? (new List<string>()) : envPmdOpt.ToList();
                for (int i = fnIndex; i < args.Length; i++)
                {
                    opt.Add(args[i]);
                }
                mIndex += (envPmdOpt == null ? 0 : envPmdOpt.Length) - fnIndex;

#if NETCOREAPP
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
                drv = new Driver.Driver();
                Driver.PMDDotNETOption dop = new Driver.PMDDotNETOption();
                dop.isAUTO = isAUTO;
                dop.isNRM = isNRM;
                dop.isSPB = isSPB;
                dop.isVA = isVA;
                dop.usePPS = usePPS;
                dop.usePPZ = usePPZ;
                dop.isLoadADPCM = false;
                dop.loadADPCMOnly = false;
                //dop.ppz8em = ppz8em;
                //dop.ppsdrv = ppsdrv;
                dop.envPmd = envPmd;
                dop.srcFile = srcFile;
                dop.jumpIndex = -1;// -1;
                List<string> pop = new List<string>();
                bool pmdvolFound = false;
                for(int i = 0; i < opt.Count; i++)
                {
                    if (i == mIndex) continue;
                    string op = opt[i].ToUpper().Trim();
                    pop.Add(op);
                    if(op.IndexOf("-D") >= 0 || op.IndexOf("/D") >= 0)
                        pmdvolFound = true;
                }

                Log.WriteLine(LogLevel.INFO, "");

                ((Driver.Driver)drv).Init(
                    srcFile
                    , OPNAWrite
                    , OPNAWaitSend
                    , dop
                    , pop.ToArray()
                    , appendFileReaderCallback
                    , PPZ8Write
                    , PPSDRVWrite
                    , P86Write
                    );


                //AUTO指定の場合に構成が変わるので、構成情報を受け取ってから音量設定を行う
                isNRM = dop.isNRM;
                isSPB=dop.isSPB;
                isVA = dop.isVA;
                usePPS = dop.usePPS;
                usePPZ = dop.usePPZ;
                string[] pmdOptionVol = SetVolume();
                //ユーザーがコマンドラインでDオプションを指定していない場合はpmdVolを適用させる
                if (!pmdvolFound && pmdOptionVol != null && pmdOptionVol.Length>0)
                {
                    ((Driver.Driver)drv).resetOption(pmdOptionVol);//
                }


                List<Tuple<string, string>> tags = drv.GetTags();
                if (tags != null)
                {
                    foreach (Tuple<string, string> tag in tags)
                    {
                        if (tag.Item1 == "") continue;
                        WriteLine2(LogLevel.INFO, string.Format("{0,-16} : {1}", tag.Item1, tag.Item2), 16 + 3);
                    }
                }

                Log.WriteLine(LogLevel.INFO, "");

                drv.StartRendering((int)SamplingRate
                    , new Tuple<string, int>[] { new Tuple<string, int>("YM2608", (int)opnaMasterClock) });

                drv.MusicSTART(0);

                switch (device)
                {
                    case 0:
                        audioOutput.Play();
                        break;
                    case 1:
                    case 2:
                        trdMain.Start();
                        break;
                }

                Log.WriteLine(LogLevel.INFO, "演奏を終了する場合は何かキーを押してください(実chip時は特に。)");

                while (true)
                {
                    System.Threading.Thread.Sleep(1);
                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    //ステータスが0(終了)又は0未満(エラー)の場合はループを抜けて終了
                    if (drv.GetStatus() <= 0)
                    {
                        if (drv.GetStatus() == 0)
                        {
                            System.Threading.Thread.Sleep((int)(latency * 2.0));//実際の音声が発音しきるまでlatency*2の分だけ待つ
                        }
                        break;
                    }

                    if (loop != 0 && drv.GetNowLoopCounter() > loop)
                    {
                        System.Threading.Thread.Sleep((int)(latency * 2.0));//実際の音声が発音しきるまでlatency*2の分だけ待つ
                        break;
                    }
                }

                drv.MusicSTOP();
                drv.StopRendering();
                ((Driver.Driver)drv).dispStatus();
            }
            catch(PmdException pe)
            {
                Log.WriteLine(LogLevel.ERROR, pe.Message);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.FATAL, "演奏失敗");
                Log.WriteLine(LogLevel.FATAL, string.Format("message:{0}", ex.Message));
                Log.WriteLine(LogLevel.FATAL, string.Format("stackTrace:{0}", ex.StackTrace));
            }
            finally
            {
                if (((Driver.Driver)drv).renderingException != null)
                {
                    Log.WriteLine(LogLevel.FATAL, "演奏失敗");
                    Log.WriteLine(LogLevel.FATAL, string.Format("message:{0}", ((Driver.Driver)drv).renderingException.Message));
                    Log.WriteLine(LogLevel.FATAL, string.Format("stackTrace:{0}", ((Driver.Driver)drv).renderingException.StackTrace));
                }

                if (audioOutput != null)
                {
                    audioOutput.Stop();
                    while (audioOutput.PlaybackState == PlaybackState.Playing) { Thread.Sleep(1); }
                    audioOutput.Dispose();
                    audioOutput = null;
                }
                if (trdMain != null)
                {
                    trdClosed = true;
                    while (!trdStopped) { Thread.Sleep(1); }
                }
                if (nc86ctl != null)
                {
                    nc86ctl.deinitialize();
                    nc86ctl = null;
                }
                if (nScci != null)
                {
                    nScci.Dispose();
                    nScci = null;
                }
            }

            return 0;
        }

        private static string[] SetVolume()
        {
            List<string> ret = new List<string>();

            if (device == 0 || device == 3)//EMU or wav
            {
                //fmgen向け設定
                //fm:ssg = 1:0.25で調整
                //
                //  pmd内で1:(0.45～0.50)に補正される
                //  ・OPNの場合のみpmdのコード上でfmの音量を下げるコードを通過する
                //  ・GIMIC ProとLiteのターミナルでも mファイルを再生し確認
                VolumeV = new int[] { 0, 0, 0, 0 };
                if (isNRM)
                {
                    //PC98のOPNを想定
                    VolumeV[0] = 12;//FM  98は88よりFMが大きい
                    VolumeV[1] = -5;//SSG
                    VolumeV[2] = -191;//Rhythm
                    VolumeV[3] = -191;//Adpcm
                }
                else
                {
                    //OPNA(-86/SPB)を想定
                    VolumeV[0] = 0;//FM
                    VolumeV[1] = -5;//SSG
                    VolumeV[2] = 0;//Rhythm //未調査
                    VolumeV[3] = 0;//Adpcm //未調査
                }
            }
            else if (device == 1)//GIMIC
            {
                if (VolumeR == null)
                {
                    VolumeR = new int[] { 0 };
                    if (isNRM)
                        VolumeR[0] = 31;//GMC-OPNA に31を送信
                    else
                        VolumeR[0] = 66;//GMC-OPNA に66を送信
                }

                //GMC-OPNA以外のOPNA系モジュール
                if (!isGimicOPNA)
                {
                    //pmdのオプションで調整
                    ret.Add("/DF12");
                    ret.Add("/DS0");
                }
            }
            else if (device == 2)//SCCI
            {
                //SCCIの場合はバランス調整はユーザー任せ
            }


            //一度目の音量設定時は反映を行わない
            if (VolumeV != null)
            {
                mds.SetVolumeYM2608FM(VolumeV[0]);
                mds.SetVolumeYM2608PSG(VolumeV[1]);
                mds.SetVolumeYM2608Rhythm(VolumeV[2]);
                mds.SetVolumeYM2608Adpcm(VolumeV[3]);
            }

            if (VolumeR != null)
            {
                if (isGimicOPNA) //GMC-OPNA
                {
                    rsc.setSSGVolume((byte)VolumeR[0]);
                    Thread.Sleep(500);//少し休む(即再生を始めると音が飛ぶ)
                }
            }

            return ret.ToArray();
        }

        public static string GetApplicationFolder()
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(path))
            {
                path += path[path.Length - 1] == '\\' ? "" : "\\";
            }
            return path;
        }

        static void WriteLine(LogLevel level, string msg)
        {
            if (level == LogLevel.ERROR || level== LogLevel.FATAL)
                Console.ForegroundColor = ConsoleColor.Red;

#if DEBUG
            Console.WriteLine("[{0,-7}] {1}", level, msg);
#else
            Console.WriteLine("{0}", msg);
#endif

            if (level == LogLevel.ERROR || level == LogLevel.FATAL)
                Console.ResetColor();
        }

        static void WriteLine2(LogLevel level, string msg, int wrapPos = 0)
        {
            if (wrapPos == 0)
            {
                Log.WriteLine(level, msg);
            }
            else
            {
                string[] mes = msg.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                Log.WriteLine(level, mes[0]);
                for (int i = 1; i < mes.Length; i++)
                {
                    Log.WriteLine(level, string.Format("{0}{1}", new string(' ', wrapPos), mes[i]));
                }
            }
        }

        private static Stream appendFileReaderCallback(string arg)
        {
            string fn;
            fn = Path.Combine(
                Path.GetDirectoryName(srcFile)
                , arg
                );

            if (envPmd != null)
            {
                int i = 0;
                while (!File.Exists(fn) && i < envPmd.Length)
                {
                    fn = Path.Combine(
                        envPmd[i++]
                        , arg
                        );
                }
            }

            FileStream strm;
            try
            {
                strm = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                strm = null;
            }

            return strm;
        }

        private static int AnalyzeOption(string[] args)
        {
            if (args == null || args.Length < 1) return 0;

            int i = 0;
            device = 0;
            loop = 0;

            while (i < args.Length && args[i] != null && args[i].Length > 0 && (args[i][0] == '-' || args[i][0] == '/'))
            {
                string op = args[i].Substring(1).ToUpper();
                if (op == "D=EMU") device = 0;
                else if (op == "D=GIMIC") device = 1;
                else if (op == "D=SCCI") device = 2;
                else if (op == "D=WAVE") device = 3;
                else if (op.Length > 2 && op.Substring(0, 2) == "L=") OptionSetLoop(op);
                else if (op == "H" || op == "?") OptionDispHelp();
                else if (op.Length > 2 && op.Substring(0, 2) == "B=") OptionSetBoard(op.Substring(2));
                else if (op.Length > 3 && op.Substring(0, 3) == "VV=") OptionSetVolumeV(op.Substring(3));
                else if (op.Length > 3 && op.Substring(0, 3) == "VR=") OptionSetVolumeR(op.Substring(3));
                else if (op.Length > 4 && op.Substring(0, 4) == "PPS=") OptionSetPPS(op.Substring(4));
                else if (op.Length > 4 && op.Substring(0, 4) == "PPZ=") OptionSetPPZ(op.Substring(4));
                else if (op.Length > 8 && op.Substring(0, 8) == "PPSFREQ=") OptionSetPPSFREQ(op.Substring(8));
                else if (op.Length > 8 && op.Substring(0, 8) == "PPSWAIT=") OptionSetPPSWAIT(op.Substring(8));
                else break;

                i++;
            }

            if (device == 3 && loop == 0) loop = 1;//wave出力の場合、無限ループは1に変更
            return i;
        }

        private static void OptionSetPPZ(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            int n;
            if (int.TryParse(v, out n))
            {
                usePPZ = n != 0;
            }
        }

        private static void OptionSetPPS(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            int n;
            if (int.TryParse(v, out n))
            {
                usePPS = n != 0;
            }
        }

        private static void OptionSetPPSFREQ(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            int n;
            if (int.TryParse(v, out n))
            {
                userPPSFREQ = Math.Min(Math.Max(n, 2000), 192000);
            }
        }
        private static void OptionSetPPSWAIT(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            int n;
            if (int.TryParse(v, out n))
            {
                ppsdrvWait = Math.Min(Math.Max(n, -1), 100);
            }
        }

        private static void OptionSetLoop(string op)
        {
            if (!int.TryParse(op.Substring(2), out loop))
            {
                loop = 0;
            }
        }

        private static void OptionSetVolumeR(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            int n;
            if (int.TryParse(v, out n))
            {
                VolumeR = new int[] { Math.Min(Math.Max(n, 0), 127) };
            }
        }

        private static void OptionSetVolumeV(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            string[] prm = v.Split(',');
            if (prm == null || prm.Length < 1) return;
            VolumeV = new int[4] { 0, 0, 0, 0 };
            for(int i = 0; i < prm.Length; i++)
            {
                if(int.TryParse(prm[i],out VolumeV[i]))
                    VolumeV[i] = Math.Min(Math.Max(VolumeV[i], -191), 20);
            }
        }

        private static void OptionSetBoard(string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            if (v == "AUTO")
            {
                isAUTO = true;
            }
            else if (v == "NRM" || v == "OPN" || v == "2203" || v == "26")
            {
                isAUTO = false;
                isNRM = true;
                isVA = false;
                isSPB = false;
            }
            else if (v == "86" || v == "86B")
            {
                isAUTO = false;
                isNRM = false;
                isVA = false;
                isSPB = false;
            }
            else if (v == "SPB" || v == "OPNA" || v == "2608")
            {
                isAUTO = false;
                isNRM = false;
                isVA = false;
                isSPB = true;
            }
            else if (v == "VA_NRM")
            {
                isAUTO = false;
                isNRM = true;
                isVA = true;
                isSPB = false;
            }
            else if (v == "VA_86")
            {
                isAUTO = false;
                isNRM = false;
                isVA = true;
                isSPB = false;
            }
        }

        private static void OptionDispHelp()
        {
            Log.WriteLine(LogLevel.INFO, @"
Welcome to PMDDotNET !

 Usage
   PMDDotNETPlayer.exe  [-[H|?]] [-D=[EMU|GIMIC|SCCI|WAVE]] [-L=n] [-B=[NRM|OPN|2203|26|86|SPB|86B|OPNA|2608|VA_NRM|VA_86]] [-VV=n,n,n,n] [-VR=n] [PMD options] [file.m]

 Options
  オプションは大文字小文字を区別しません。

   -D=
     -D=オプションを指定することにより再生デバイスを変更できます。
       -D=EMU
         デフォルト値です。
         エミュレーションによる再生をWindowsの音声デバイスから行います。
       -D=GIMIC
         G.I.M.I.CのOPNAモジュールによる再生を行います。モジュールが見つからない場合はEMUと同じ動作になります。
       -D=SCCI
         SCCIのOPNAモジュールによる再生を行います。モジュールが見つからない場合はEMUと同じ動作になります。

   -L=n
     ループ回数を0以上の数値で指定します。(TBD)
     但し0は無限ループになります。デフォルト値は0です。
     ループ回数というオプションですが実際は演奏回数です。つまり1を指定した場合、一通り演奏するとループせずに終了します。
     解析できない数値を指定した場合は0(無限ループ)となります。
     再生デバイスがWAVEの場合に0を指定した場合は1に修正されます。

   -B=
     想定する音源ボードを指定します。
     PMDの振る舞いが変わるほか、エミュレーションや実チップに設定するボリューム値も設定します。
     ボリューム値については後述の-VV=などにて変更可能です。
       -B=AUTO
         デフォルト値です。
         -B= -PPS= -PPZ=のオプションが自動で設定されます。
         設定は曲データ中タグのPCMファイル指定状況から判断されます。以下の通りです。

             #    .PPC(ヘッダ)  .PPS    .PZI       自動設定オプション
             ------------------------------------------------------------
             01   未使用        未使用  未使用     -B=SPB -PPS=0 -PPZ=0
             02   .PPC/.PVI     未使用  未使用     -B=SPB -PPS=0 -PPZ=0
             03   .P86          未使用  未使用     -B=86B -PPS=0 -PPZ=0
             04   未使用          使用  未使用     -B=SPB -PPS=1 -PPZ=0
             05   .PPC/.PVI       使用  未使用     -B=SPB -PPS=1 -PPZ=0
             06   .P86            使用  未使用     -B=86B -PPS=1 -PPZ=0
             07   未使用        未使用    使用     -B=SPB -PPS=0 -PPZ=1
             08   .PPC/.PVI     未使用    使用     -B=SPB -PPS=0 -PPZ=1
             09   .P86          未使用    使用     -B=86B -PPS=0 -PPZ=1
             10   未使用          使用    使用     -B=SPB -PPS=1 -PPZ=1
             11   .PPC/.PVI       使用    使用     -B=SPB -PPS=1 -PPZ=1
             12   .P86            使用    使用     -B=86B -PPS=1 -PPZ=1

       -B=NRM|OPN|2203|26
         ノーマル音源(OPN)を指定します。
         以下のオプションが暗黙で指定されます。
           -VV=12,-5,-191,-191
         GIMIC GMC-OPNAの場合
           -VR=31
         GIMIC GMC-OPNA以外のモジュールの場合(PMDのオプション)
           -DF12 -DS0
         SCCIの場合(PMDのオプション)
           -DF1 -DS0
       -B=86|86B|SPB|OPNA|2608
         拡張音源(OPNA)を指定します。
         SPB/OPNA/2608を指定するとADPCMを利用します。(PMDB2相当)
         .PPC/.PVIファイルが指定されている場合は再生前にADPCMデータを転送する処理が発生します。
         (エミュレーションの場合以外は転送に時間がかかります。)
         以下のオプションが暗黙で指定されます。
           -VV=0,-5,0,0
         GIMIC GMC-OPNAの場合
           -VR=66
         GIMIC GMC-OPNA以外のモジュールの場合(PMDのオプション)
           -DF12 -DS0
         SCCIの場合(PMDのオプション)(TBD)
           -DF1 -DS0
       -B=VA_NRM
         PC-88VAノーマル音源を指定します。(TBD)
         以下のオプションが暗黙で指定されます。(TBD)
           -VV=0,0,0,0
         GIMIC GMC-OPNAの場合(TBD)
           -VR=31
         GIMIC GMC-OPNA以外のモジュール又はSCCIの場合(PMDのオプション)(TBD)
           -DFn -DSn -DRn -DPn -DZn
       -B=VA_86
         PC-88VA拡張音源を指定します。(TBD)
         以下のオプションが暗黙で指定されます。(TBD)
           -VV=0,0,0,0
         GIMIC GMC-OPNAの場合(TBD)
           -VR=31
         GIMIC GMC-OPNA以外のモジュール又はSCCIの場合(PMDのオプション)(TBD)
           -DFn -DSn -DRn -DPn -DZn

   -VV=n,n,n,n
     エミュレーション向けボリューム値を設定します。
     カンマ区切りでFM,SSG,Rhythm,Adpcmの順に音量を指定します。
     nの指定可能範囲は-191～20です。

   -VR=n
     実チップ向けボリューム値を設定します。
     実質、GIMICのOPNAモジュール専用オプションで、SSGの音量を0～127で指定します。

   -PPS=n
     PPSDRVを使用するときは1を指定します。0を指定すると使用しません。
     デフォルト値は0です。
     nの指定可能値は0または1です。

   -PPZ=n
     PPZ8を使用するときは1を指定します。0を指定すると使用しません。
     デフォルト値は0です。
     nの指定可能値は0または1です。

   -PPSFREQ=n
     PPSDRVの周波数(Hz)を指定します。
     実Chipのみ有効です。
     デフォルト値はGIMICは44100、SCCIは16000です。
     nの指定可能値は2000～192000です。

   -PPSWAIT=n
     SCCIへ送信する同期の為のウエイト値を指定します。
     SCCIのみ有効です。
     デフォルト値は1です。
     -1の場合は送信しません。
     nの指定可能値は-1～100です。

   [PMD options]
     オリジナルのPMDへ送るオプションを指定します。
     実際には上記以外のオプションや、ファイル名を指定すると全てオリジナルのPMDへ指定したものと解釈されます。

   [file.m]
     .mファイルを指定します。拡張子のチェックはしません。
");
        }

        private static void OPNAWaitSend(long elapsed, int size)
        {
            switch (device)
            {
                case 0://EMU
                    return;
                case 1://GIMIC

                    //サイズと経過時間から、追加でウエイトする。
                    int m = Math.Max((int)(size / 20 - elapsed), 0);//20 閾値(magic number)
                    Thread.Sleep(m);

                    //ポートも一応見る
                    int n = nc86ctl.getNumberOfChip();
                    for (int i = 0; i < n; i++)
                    {
                        NIRealChip rc = nc86ctl.getChipInterface(i);
                        if (rc != null)
                        {
                            while ((rc.@in(0x0) & 0x83) != 0)
                                Thread.Sleep(0);
                            while ((rc.@in(0x100) & 0xbf) != 0)
                                Thread.Sleep(0);
                        }
                    }

                    break;
                case 2://SCCI
                    nScci.NSoundInterfaceManager_.sendData();
                    while (!nScci.NSoundInterfaceManager_.isBufferEmpty())
                    {
                        Thread.Sleep(0);
                    }
                    break;
            }
        }

        private static RSoundChip CheckDevice()
        {
            SChipType ct = null;
            int iCount = 0;

            switch (device)
            {
                case 1://GIMIC存在チェック
                    nc86ctl = new Nc86ctl.Nc86ctl();
                    try
                    {
                        nc86ctl.initialize();
                        iCount = nc86ctl.getNumberOfChip();
                    }
                    catch
                    {
                        iCount = 0;
                    }
                    if (iCount == 0)
                    {
                        try { nc86ctl.deinitialize(); } catch { }
                        nc86ctl = null;
                        Log.WriteLine(LogLevel.ERROR, "Not found G.I.M.I.C");
                        device = 0;
                        break;
                    }
                    for (int i = 0; i < iCount; i++)
                    {
                        NIRealChip rc = nc86ctl.getChipInterface(i);
                        NIGimic2 gm = rc.QueryInterface();
                        ChipType cct = gm.getModuleType();
                        int o = -1;
                        if (cct == ChipType.CHIP_YM2608 || cct == ChipType.CHIP_YMF288 || cct == ChipType.CHIP_YM2203)
                        {
                            ct = new SChipType();
                            ct.SoundLocation = -1;
                            ct.BusID = i;
                            string seri = gm.getModuleInfo().Serial;
                            if (!int.TryParse(seri, out o))
                            {
                                o = -1;
                                ct = null;
                                continue;
                            }
                            ct.SoundChip = o;
                            ct.ChipName = gm.getModuleInfo().Devname;
                            ct.InterfaceName = gm.getMBInfo().Devname;
                            isGimicOPNA = (ct.ChipName == "GMC-OPNA");
                            break;
                        }
                    }
                    RC86ctlSoundChip rsc = null;
                    if (ct == null)
                    {
                        nc86ctl.deinitialize();
                        nc86ctl = null;
                        Log.WriteLine(LogLevel.ERROR, "Not found G.I.M.I.C(OPNA module)");
                        device = 0;
                    }
                    else
                    {
                        rsc = new RC86ctlSoundChip(-1, ct.BusID, ct.SoundChip);
                        rsc.c86ctl = nc86ctl;
                        rsc.init();

                        rsc.SetMasterClock(7987200);//SoundBoardII
                        rsc.setSSGVolume(63);//PC-8801
                    }
                    return rsc;
                case 2://SCCI存在チェック
                    nScci = new NScci.NScci();
                    iCount = nScci.NSoundInterfaceManager_.getInterfaceCount();
                    if (iCount == 0)
                    {
                        nScci.Dispose();
                        nScci = null;
                        Log.WriteLine(LogLevel.ERROR, "Not found SCCI.");
                        device = 0;
                        break;
                    }
                    for (int i = 0; i < iCount; i++)
                    {
                        NSoundInterface iIntfc = nScci.NSoundInterfaceManager_.getInterface(i);
                        NSCCI_INTERFACE_INFO iInfo = nScci.NSoundInterfaceManager_.getInterfaceInfo(i);
                        int sCount = iIntfc.getSoundChipCount();
                        for (int s = 0; s < sCount; s++)
                        {
                            NSoundChip sc = iIntfc.getSoundChip(s);
                            int t = sc.getSoundChipType();
                            if (t == 1)
                            {
                                ct = new SChipType();
                                ct.SoundLocation = 0;
                                ct.BusID = i;
                                ct.SoundChip = s;
                                ct.ChipName = sc.getSoundChipInfo().cSoundChipName;
                                ct.InterfaceName = iInfo.cInterfaceName;
                                goto scciExit;
                            }
                        }
                    }
                scciExit:;
                    RScciSoundChip rssc = null;
                    if (ct == null)
                    {
                        nScci.Dispose();
                        nScci = null;
                        Log.WriteLine(LogLevel.ERROR, "Not found SCCI(OPNA module).");
                        device = 0;
                    }
                    else
                    {
                        rssc = new RScciSoundChip(0, ct.BusID, ct.SoundChip);
                        rssc.scci = nScci;
                        rssc.init();
                    }
                    return rssc;
            }

            return null;
        }

        private static int EmuCallback(short[] buffer, int offset, int count)
        {
            try
            {
                long bufCnt = count / 2;

                for (int i = 0; i < bufCnt; i++)
                {
                    mds.Update(emuRenderBuf, 0, 2, OneFrame);
                    //ppz8em.Update(emuRenderBuf);
                    //ppsdrv.Update(emuRenderBuf);

                    buffer[offset + i * 2 + 0] = emuRenderBuf[0];
                    buffer[offset + i * 2 + 1] = emuRenderBuf[1];

                }
            }
            catch(Exception ex)
            {
                Log.WriteLine(LogLevel.FATAL, string.Format("{0} {1}", ex.Message, ex.StackTrace));
            }

            return count;
        }

        private static void RealCallback()
        {

            double o = sw.ElapsedTicks / swFreq;
            double oPPS = sw.ElapsedTicks / swFreq;
            double step = 1 / (double)SamplingRate;
            uint PPSSamplingRate = (uint)(
                userPPSFREQ == -1 
                ? (device == 1 ? SamplingRatePPSGIMIC : SamplingRatePPSSCCI) 
                : (uint)userPPSFREQ);
            double stepPPS = 1 / (double)PPSSamplingRate;

            trdStopped = false;
            try
            {
                while (!trdClosed)
                {
                    Thread.Sleep(0);

                    double el1 = sw.ElapsedTicks / swFreq;
                    if (el1 - o >= step)
                    {
                        if (el1 - o >= step * SamplingRate / 100.0)//閾値10ms
                        {
                            do
                            {
                                o += step;
                            } while (el1 - o >= step);
                        }
                        else
                        {
                            o += step;
                        }

                        OneFrame();
                    }

                    if (el1 - oPPS >= stepPPS)
                    {
                        if (el1 - oPPS >= stepPPS * PPSSamplingRate / 100.0)//閾値10ms
                        {
                            do
                            {
                                oPPS += stepPPS;
                            } while (el1 - oPPS >= stepPPS);
                        }
                        else
                        {
                            oPPS += stepPPS;
                        }

                        ppsdrv.Update(0, null, 1);
                    }

                }
            }
            catch
            {
            }
            trdStopped = true;
        }

        private static void OneFrame()
        {
            drv.Rendering();
        }

        private static void OPNAWrite(ChipDatum dat)
        {
            if (dat != null && dat.addtionalData != null)
            {
                MmlDatum md = (MmlDatum)dat.addtionalData;
                if (md.linePos != null)
                {
                    Log.WriteLine(LogLevel.TRACE, string.Format("! r{0} c{1}"
                        , md.linePos.row
                        , md.linePos.col
                        ));
                }
            }

#if DEBUG
            //if (dat.address == 0x29)
                //Log.WriteLine(LogLevel.INFO, string.Format("FM P{2} Out:Adr[{0:x02}] val[{1:x02}]", (int)dat.address, (int)dat.data, dat.port));
#endif

            switch (device)
            {
                case 0:
                    mds.WriteYM2608(0, (byte)dat.port, (byte)dat.address, (byte)dat.data);
                    break;
                case 1:
                case 2:
                    rsc.setRegister(dat.port * 0x100 + dat.address, dat.data);
                    break;
            }
        }

        private static int PPZ8Write(ChipDatum arg)
        {
            if (arg == null) return 0;

            if (arg.port == 0x03)
            {
                return ppz8em.LoadPcm(0, (byte)arg.address, (byte)arg.data, (byte[][])arg.addtionalData);
            }
            else
            {
                return ppz8em.Write(0, arg.port, arg.address, arg.data);
            }
        }

        private static int PPSDRVWrite(ChipDatum arg)
        {
            if (arg == null) return 0;

            if (arg.port == 0x05)
            {
                return ppsdrv.Load(0, (byte[])arg.addtionalData);
            }
            else
            {
                return ppsdrv.Write(0, arg.port, arg.address, arg.data);
            }
        }


        //static int aold = -1;
        //static int dold = -1;

        private static void PPSDRVpsg(int a,int d)
        {
            switch (device)
            {
                case 0:
                    mds.WriteYM2608(0, 0, (byte)a, (byte)d);
                    break;
                case 1:
                case 2:
                    //if (aold != a || dold != d)
                    {
                        rsc.setRegister(0 * 0x100 + a, d);
                        if (ppsdrvWait > -1) rsc.setRegister(-1, ppsdrvWait);

                        //aold = a;
                        //dold = d;
                    }
                    break;
            }
        }

        private static int P86Write(ChipDatum arg)
        {
            if (arg == null) return 0;

            if (arg.port == 0x00)
            {
                return p86em.LoadPcm(0, (byte)arg.address, (byte)arg.data, (byte[])arg.addtionalData);
            }
            else
            {
                return p86em.Write(0, arg.port, arg.address, arg.data);
            }
        }

        public class SineWaveProvider16 : WaveProvider16
        {

            public SineWaveProvider16()
            {
            }

            public override int Read(short[] buffer, int offset, int sampleCount)
            {

                return callBack(buffer, offset, sampleCount);

            }

        }
    }
}