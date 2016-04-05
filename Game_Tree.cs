using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleWars
{
    // UCT探索で用いるノードクラス
    class Game_Tree
    {
        public Map board;                 // 盤面
        public int depth;                 // 探索木内での深さ
        public int simnum;                // 訪問回数(シミュレーション回数)
        public double lastscore;          // 最後のシミュレーション結果
        public double totalscore;         // これまでのシミュレーション結果の合計
        public double housyuu;            // totalscore と simnum から求める報酬
        public Action act;                // 選択された行動
        public List<Game_Tree> next;      // 子ノードのリスト

        public Game_Tree()
        {
            board = new Map();
            depth = 0;
            simnum = 0;
            lastscore = 0;
            totalscore = 0;
            housyuu = 0;
            act = new Action();
            next = new List<Game_Tree>();
        }
    }
}
