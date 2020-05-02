using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PW
    {
        public const string ver = "4.8q";
        public int vers = 0x48;
        public string verc = "q";
        public string date = "Aug.5th 1998";

        public int mdata_def = 16;
        public int voice_def = 8;
        public int effect_def = 4;
        public int key_def = 1;

        public string _myname = "PMD     COM";

        public int va = 0;//１の時ＶＡMSDOS用
        public int board2 = 0;//１の時ボード２/音美ちゃん有り
        public int adpcm = 0;//１の時ADPCM使用
        public int ademu = 0;//１の時ADPCM Emulate
        public int pcm = 0;//１の時PCM使用
        public int ppz = 0;//１の時PPZ8使用
        public int sync = 0;//１の時MIDISYNC使用
        public int vsync = 0;//１の時VSyncを止める
        public string resmes = "PMD ver." + ver;

    }
}
