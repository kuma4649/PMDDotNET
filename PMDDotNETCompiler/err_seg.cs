using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Compiler
{
    public static class err_seg
    {
        public static string errmes_1 = " Error ";
        public static string errmes_2 = ": Part ";
        public static string errmes_3 = ": Command ";
        public static string errmes_4 = "---------- ";
        public static string errmes_5 = " (Macro)";
        public static string incfile_mes = "Include file :";
        public static string crlf_mes = "" + mc.cr + mc.lf;

        public static string[] err_table = new string[]
        {
             "オプション指定が間違っています。"
            ,"MML中に理解不能な文字があります。"
            ,"指定された数値が異常です。"
            ,"MMLファイルが読み込めません。"
            ,"MMLファイルが書き込めません。"
            ,"FFファイルが書き込めません。"
            ,"パラメータの指定が足りません。"
            ,"使用出来ない文字を指定しています。"
            ,"指定された音長が長すぎます。"
            ,"ポルタメント終了記号 } がありません。"
            ,"Lコマンド後に音長指定がありません。"
            ,"効果音パートでハードＬＦＯは使用出来ません。"
            ,"効果音パートでテンポ命令は使用出来ません。"
            ,"ポルタメント開始記号 { がありません。"
            ,"ポルタメントコマンド中の指定が間違っています。"
            ,"ポルタメントコマンド中に休符があります。"
            ,"音程コマンドの直後に指定して下さい。"
            ,"ここではこのコマンドは使用できません。"
            ,"MMLのサイズが大き過ぎます。"
            ,"コンパイル後のサイズが大き過ぎます。"
            ,"W/Sコマンド使用中に255stepを越える音長は指定出来ません。"
            ,"使用不可能な音長を指定しています。"
            ,"タイが音程命令直後に指定されていません。"
            ,"ループ開始記号 [ がありません。"
            ,"無限ループ中に音長を持つ命令がありません。"
            ,"１ループ中に脱出記号が２ヶ所以上あります。"
            ,"音程が限界を越えています。"
            ,"MML変数が定義されていません。"
            ,"音色ファイルか/Vオプションを指定してください。"
            ,"Ｒパートが必要分定義されていません。"
            ,"音色が定義されていません。"
            ,"直前の音符長が圧縮または加工されています。"
            ,"Rパートでタイ・スラーは使用出来ません。"
            ,"可変長MML変数の定義数が256を越えました。"
        };
    }
}
