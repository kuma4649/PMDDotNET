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

        private static bool isNRM = true;
        private static bool isVA = false;
        private static bool usePPS = false;
        private static bool usePPZ = false;
        private static int[] VolumeV = null;
        private static int[] VolumeR = null;
        private static bool isGimicOPNA = false;

        static int Main(string[] args)
        {
            Log.writeLine += WriteLine;
#if DEBUG
            //Log.writeLine += WriteLineF;
            Log.level = LogLevel.INFO;//.TRACE;
#else
            Log.level = LogLevel.INFO;
#endif
            int fnIndex = AnalyzeOption(args);
            int mIndex = -1;

            if (args != null)
            {
                for (int i = fnIndex; i < args.Length; i++)
                {
                    if (Path.GetExtension(args[i]).ToUpper().IndexOf(".M") < 0) continue;
                    mIndex = i;
                    break;
                }
            }

            if (mIndex<0)
            {
                Log.WriteLine(LogLevel.INFO, "引数(.Mファイル)１個欲しいよぉ...");
                return -1;
            }

            if (!File.Exists(args[mIndex]))
            {
                Log.WriteLine(LogLevel.ERROR, "ファイルが見つかりません");
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

                mds = new MDSound.MDSound(SamplingRate, samplingBuffer, new MDSound.MDSound.Chip[] { chip });

                string[] pmdVol = SetVolume();

                //OPNAWrite(new ChipDatum(0, 0x29, 0x82));//6chMode


#if NETCOREAPP
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
                drv = new Driver.Driver();
                Driver.PMDDotNETOption dop = new Driver.PMDDotNETOption();
                dop.isNRM = isNRM;
                dop.isVA = isVA;
                dop.usePPS = usePPS;
                dop.usePPZ = usePPZ;
                dop.isLoadADPCM = false;
                dop.loadADPCMOnly = false;
                List<string> pop = new List<string>();
                bool pmdvolFound = false;
                for(int i = fnIndex; i < args.Length; i++)
                {
                    if (i == mIndex) continue;
                    string op = args[i].ToUpper().Trim();
                    pop.Add(op);
                    if(op.IndexOf("-D") >= 0 || op.IndexOf("/D") >= 0)
                        pmdvolFound = true;
                }

                //Dオプションを指定していない場合はpmdVolを適用させる
                if (!pmdvolFound)
                {
                    foreach (string ao in pmdVol) pop.Add(ao);
                }

                Log.WriteLine(LogLevel.INFO, "");

                ((Driver.Driver)drv).Init(
                    args[mIndex]
                    , OPNAWrite
                    , OPNAWaitSend
                    , dop
                    , pop.ToArray()
                    );

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

                //if (loadADPCMOnly) return 0;

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
                if (VolumeV == null)
                {
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
                        VolumeV[2] = -10;//Rhythm //未調査
                        VolumeV[3] = 0;//Adpcm //未調査
                    }
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
                //pmdのオプションで調整
                ret.Add("/DF1");
                ret.Add("/DS0");
            }


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
                VolumeR = new int[] { Math.Min(Math.Max(n, 0), 255) };
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
            if (v == "NRM" || v == "OPN" || v == "2203" || v == "26")
            {
                isNRM = true;
                isVA = false;
            }
            else if (v == "86" || v == "SPB" || v == "86B" || v == "OPNA" || v == "2608")
            {
                isNRM = false;
                isVA = false;
            }
            else if (v == "VA_NRM")
            {
                isNRM = true;
                isVA = true;
            }
            else if (v == "VA_86")
            {
                isNRM = false;
                isVA = true;
            }
        }

        private static void OptionDispHelp()
        {
            Log.writeLine(LogLevel.INFO, @"
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
         G.I.M.I.C.のOPNAモジュールによる再生を行います。モジュールが見つからない場合はEMUと同じ動作になります。
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
     ボリューム値については後述の-V=にて変更可能です。
       -B=NRM|OPN|2203|26
         デフォルト値です。
         ノーマル音源(OPN)を指定します。
         以下のオプションが暗黙で指定されます。
           -VV=12,-5,-191,-191
         GIMIC GMC-OPNAの場合
           -VR=31
         GIMIC GMC-OPNA以外のモジュール又はSCCIの場合(PMDのオプション)(TBD)
           -DFn -DSn -DRn -DPn -DZn
       -B=86|SPB|86B|OPNA|2608
         拡張音源(OPNA)を指定します。
         以下のオプションが暗黙で指定されます。
           -VV=0,-5,-18,0
         GIMIC GMC-OPNAの場合
           -VR=66
         GIMIC GMC-OPNA以外のモジュール又はSCCIの場合(PMDのオプション)(TBD)
           -DFn -DSn -DRn -DPn -DZn
       -B=VA_NRM
         PC-88VAノーマル音源を指定します。
         以下のオプションが暗黙で指定されます。
           -VV=0,0,0,0
         GIMIC GMC-OPNAの場合
           -VR=31
         GIMIC GMC-OPNA以外のモジュール又はSCCIの場合(PMDのオプション)(TBD)
           -DFn -DSn -DRn -DPn -DZn
       -B=VA_86
         PC-88VA拡張音源を指定します。
         以下のオプションが暗黙で指定されます。
           -VV=0,0,0,0
         GIMIC GMC-OPNAの場合
           -VR=31
         GIMIC GMC-OPNA以外のモジュール又はSCCIの場合(PMDのオプション)(TBD)
           -DFn -DSn -DRn -DPn -DZn

   -VV=n,n,n,n
     エミュレーションに設定するボリューム値を設定します。
     カンマ区切りでFM,SSG,Rhythm,Adpcmの順に音量を指定します。
     nの指定可能範囲は-191～20です。

   -VR=n
     実チップに設定するボリューム値を設定します。
     実質、GIMICのOPNAモジュール専用オプションで、SSGの音量を0～255で指定します。

   -PPS=n  (TBD)
     PPSDRVを使用するときは1を指定します。0を指定すると使用しません。
     デフォルト値は0です。
     nの指定可能範囲は0～1です。

   -PPZ=n  (TBD)
     PPZ8を使用するときは1を指定します。0を指定すると使用しません。
     デフォルト値は0です。
     nの指定可能範囲は0～1です。

   [PMD options]
     オリジナルのPMDへ送るオプションを指定します。
     実際には上記以外のオプションやファイル名を指定すると全てオリジナルのPMDへ指定したものと解釈されます。

   [file.m]
     .mファイルを指定します。.m2ファイルなどは今のところ未対応です。
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
                        Log.WriteLine(LogLevel.ERROR, "Not found G.I.M.I.C.");
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
                        Log.WriteLine(LogLevel.ERROR, "Not found G.I.M.I.C.(OPNA module)");
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
            double step = 1 / (double)SamplingRate;

            trdStopped = false;
            try
            {
                while (!trdClosed)
                {
                    Thread.Sleep(0);

                    double el1 = sw.ElapsedTicks / swFreq;
                    if (el1 - o < step) continue;
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