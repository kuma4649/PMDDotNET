using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PMDDotNETOption
    {
        public bool isLoadADPCM;
        public bool loadADPCMOnly;
        public bool isAUTO;
        public bool isVA;
        public bool isNRM;
        public bool usePPS;
        public bool usePPZ;
        public bool isSPB;
        public string[] envPmd;
        public string[] envPmdOpt;
        public string srcFile;
        public string PPCHeader;
    }
}
