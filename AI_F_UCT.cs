using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;


namespace SimpleWars
{
    // 自作AIのメインクラス
    class AI_F_UCT : Player
    {
        // パラメータ
        private const int MAX_SIM = 2000;       // 1行動あたりのシミュレーション回数
        private const int SIM_SIKI = 10;        // 木探索での子ノードを展開する閾値
        private const double UCB_CONST = 0.15;  // UCB値の特性を定める定数

        // ストップウォッチ関連
        private Stopwatch stopwatch = new Stopwatch();
        private static long timeLeft;           // 残り時間
        private const long LIMIT_TIME = 100000;   // 1ターンにかける時間(ミリ秒)　(GPW杯やGATのレギュレーションに則るなら10000未満の値)

        // その他
        private static int max_depth;           // 探索木の深さの最大値
        private static int totalsim;            // 制限時間内のプレイアウト回数およびUCB値計算に用いる変数
        private static int lastid;              // 木探索の再帰で直前のノードidを記憶するための変数
        private static int movablenum;          // 未行動ユニットの数


        #region 表示名、パラメータ情報
        // AIの表示名を返す（必須）
        // 改行文字 \r, \n，半角大カッコ [, ]，システム文字は使えない
        public string getName()
        {
            return "F-UCT";
        }

        // パラメータ等の情報を返す（必須だが，空でも良い）
        // 改行は含んでも構わない．半角大カッコ [, ], システム文字は使えない
        public string showParameters()
        {
            return "";

            // 例えば PARAM1, PARAM2 というパラメータがあって，棋譜等に残したい場合
            // return "PARAM1 = " + PARAM1 + "\r\n" + "PARAM2 = " + PARAM2;
        }
        #endregion

        // 1ユニットの行動を決定する（必須）
        // なお，ここでもらった map オブジェクトはコピーされたものなので，どのように利用・変更しても問題ない．
        public Action makeAction(Map map, int teamColor, bool turnStart, bool gameStart)
        {
            // ゲーム開始時にファジィ表作成
            if (gameStart)
            {
                Fuzzy_Table.makeTables();
            }

            // タイマースタート
            stopwatch.Start();

            // ルートノード作成
            Game_Tree root = makeroot(map, teamColor);

            // ターン開始時に制限時間を設定
            if (turnStart)
            {
                timeLeft = LIMIT_TIME;
            }

            // 未行動ユニット数を計算
            movablenum = map.getUnitsList(teamColor, false, true, false).Count;

            // プレイアウト回数を0に初期化
            totalsim = 0;

            for (int i = 0; i < MAX_SIM; i++)
            {
                // 1行動あたりに与えられた時間を超えても指定回数探索が行われていない場合、
                // その時点で探索を中断する
                if (stopwatch.ElapsedMilliseconds > (timeLeft / movablenum) )
                {
                    Logger.addLogMessage("timeover.\r\n", teamColor);
                    break;
                }

                // 主探索部
                search(root, teamColor);

                totalsim++;
            }

            // タイマーストップ
            stopwatch.Stop();

            // デバッグ時はここで探索木の様子等をログ出力
            Logger.addLogMessage("経過時間(ミリ秒): " + stopwatch.ElapsedMilliseconds + "\r\n", teamColor);
            Logger.log("探索木の最大深さ: " + max_depth + "\r\n", teamColor);
            Logger.log("プレイアウト回数: " + totalsim + "\r\n", teamColor);

            //残り時間を計算
            timeLeft -= stopwatch.ElapsedMilliseconds;
            stopwatch.Reset();

            return maxRateAction(root);
        }

        // ルートノード作成
        public static Game_Tree makeroot(Map map, int teamcolor)
        {
            Game_Tree root = new Game_Tree(); // 戻り値
            root.board = map.createDeepClone();

            List<Action> allActions = new List<Action>(); // 全合法手
            List<Unit> allUnits = map.getUnitsList(teamcolor, false, true, false);  // 未行動自ユニット

            // 全合法手を取得
            foreach (Unit u in allUnits)
            {
                List<Action> the_acts = AiTools2.getUnitActions(u, map); // 選択したユニットの全行動
                allActions.AddRange(the_acts);
            }

            // 全合法手に対し子ノードとしての初期設定を定める
            foreach (Action act in allActions)
            {
                Game_Tree child = new Game_Tree();
                child.board = map.createDeepClone();
                child.act = act;
                child.depth = 1;
                child.board.executeAction(act);
                root.next.Add(child);

                // 訪問回数(プレイアウト回数)をファジィ評価から定める
                // (プレイアウト結果は必ず勝利とする)
                child.simnum = (int)(Fuzzy_Table.returnValue(act, child.board) * 10);
                child.totalscore = child.simnum;
                child.housyuu = 1;                
            }
            return root;
        }

        // 主探索部
        public static void search(Game_Tree n, int teamcolor)
        {
            // 探索木の最大深さを更新
            if (totalsim == 0)
            {
                max_depth = 0;
            }
            if (n.depth > max_depth)
            {
                max_depth = n.depth;
            }

            int enemycolor = getenemycolor(teamcolor); // 相手チームの色
            int maxID = 0; // UCB値最大子ノードのID
            double maxUCB = -1; // 子ノードの最大UCB値(初期値は0未満)
            double tmpUCB; // UCB値比較用の一時変数

            // UCB値最大の子ノードを探す
            for (int j = 0; j < n.next.Count; j++)
            {
                Game_Tree child = n.next[j];
                tmpUCB = evaluateUCT(child);

                if (tmpUCB == 100) // 未訪問ノード
                {
                    maxUCB = tmpUCB;
                    maxID = j;
                    break;
                }
                if (tmpUCB > maxUCB)
                {
                    maxUCB = tmpUCB;
                    maxID = j;
                }
            }

            // そのノードが展開閾値を超えている場合
            if (n.next[maxID].simnum > SIM_SIKI)
            {
                // 展開されていない場合
                if (n.next[maxID].next.Count == 0)
                {
                    // 未行動自ユニットがいる場合
                    if (n.next[maxID].board.getUnitsList(teamcolor, false, true, false).Count > 0)
                    {
                        development(n.next[maxID], teamcolor);
                    }
                    // 未行動相手ユニットならいる場合
                    else if (n.next[maxID].board.getUnitsList(enemycolor, false, true, false).Count > 0)
                    {
                        development(n.next[maxID], enemycolor);
                    }
                    // 未行動ユニットが存在しない場合
                    else
                    {
                        // 生存ユニットを全て行動可能にする
                        n.next[maxID].board.enableUnitsAction(teamcolor);
                        n.next[maxID].board.enableUnitsAction(enemycolor);

                        if (n.next[maxID].board.getUnitsList(teamcolor, false, true, false).Count > 0)
                            development(n.next[maxID], teamcolor);
                        else
                            development(n.next[maxID], enemycolor);
                    }
                }

                // 再帰
                search(n.next[maxID], teamcolor);

                // 子ノードから返ってきた結果を反映させる
                n.next[maxID].simnum++;
                n.next[maxID].lastscore = n.next[maxID].next[lastid].lastscore;
                n.next[maxID].totalscore += n.next[maxID].lastscore;
                n.next[maxID].housyuu = n.next[maxID].totalscore / n.next[maxID].simnum;
                
                lastid = maxID;
            }
            // 末端の葉ノード
            else
            {
                // ゲーム終局までランダムシミュレーション
                Map result = randomsimulation(n.next[maxID].board, teamcolor);

                // シミュレーション結果を記録
                n.next[maxID].simnum++;
                n.next[maxID].lastscore = evaluateStateValue(result, teamcolor);
                n.next[maxID].totalscore += n.next[maxID].lastscore;
                n.next[maxID].housyuu = n.next[maxID].totalscore / n.next[maxID].simnum;

                lastid = maxID;
            }
        }

        // 勝率が最も高いノードの行動を返す
        public static Action maxRateAction(Game_Tree root)
        {
            int rtnID = 0; // 最大勝率ノードのID
            double maxrate = 0; // 最大勝率

            for (int i = 0; i < root.next.Count; i++)
            {
                Game_Tree rtnnode = root.next[i];
                double tmprate = root.next[i].housyuu;
                if (tmprate > maxrate)
                {
                    maxrate = tmprate;
                    rtnID = i;
                }
            }

            return root.next[rtnID].act;
        }

        //ランダムシミュレーション
        public static Map randomsimulation(Map map, int teamcolor)
        {
            int enemycolor = getenemycolor(teamcolor);  // 相手チームの色
            Random fRand = new Random(); // 乱数
            Map simmap = map.createDeepClone(); // シミュレーション用マップ

            // 上限ターンまで繰り返す
            while (simmap.getTurnCount() < simmap.getTurnLimit())
            {
                List<Unit> simUnits = simmap.getUnitsList(teamcolor, false, true, false); // 未行動ユニット

                // 未行動ユニットがいなくなるまで繰り返す
                while (simUnits.Count > 0)
                {
                    Action simact;

                    // 80%の確率で攻撃行動の中からランダムに行動を選択
                    // 20%の確率で全行動の中からランダムに行動を選択
                    if (fRand.NextDouble() >= 0.2 && AiTools2.getAllAttackActions(teamcolor, simmap).Count > 0)
                    {
                        List<Action> simacts = AiTools2.getAllAttackActions(teamcolor, simmap); // 全攻撃行動を取得
                        simact = simacts[fRand.Next(simacts.Count)]; // ランダムに１つを選択

                    }
                    else
                    {
                        Unit simUnit = simUnits[fRand.Next(simUnits.Count)]; // 未行動ユニットをランダムに１つを選択
                        List<Action> simacts = AiTools2.getUnitActions(simUnit, simmap); // 選択したユニットの全行動を取得
                        simact = simacts[fRand.Next(simacts.Count)]; // ランダムに１つを選択
                    }

                    // 行動ユニットをリストから削除
                    simUnits.Remove(simmap.getUnit(simact.operationUnitId));

                    // マップに選択行動を適用
                    simmap.executeAction(simact);
                }

                // ターン終了時に次の自分ターンに備えてユニットを行動可能にする
                simmap.enableUnitsAction(teamcolor);

                // ターンインクリメント
                simmap.incTurnCount();

                // ターン上限なら終了
                if (simmap.getTurnCount() >= simmap.getTurnLimit())
                {
                    break;
                }


                /* ------------------------------ ターン変更 -----------------------------------*/


                List<Unit> enemies = simmap.getUnitsList(enemycolor, false, true, false); // 未行動相手ユニット

                while (enemies.Count > 0)
                {
                    Action enact;
                    if (fRand.NextDouble() >= 0.2 && AiTools2.getAllAttackActions(enemycolor, simmap).Count > 0)
                    {
                        List<Action> enacts = AiTools2.getAllAttackActions(enemycolor, simmap);
                        enact = enacts[fRand.Next(enacts.Count)];

                    }
                    else
                    {
                        Unit enUnit = enemies[fRand.Next(enemies.Count)];
                        List<Action> enacts = AiTools2.getUnitActions(enUnit, simmap);
                        enact = enacts[fRand.Next(enacts.Count)];
                    }

                    enemies.Remove(simmap.getUnit(enact.operationUnitId));

                    simmap.executeAction(enact);
                }

                simmap.enableUnitsAction(enemycolor);

                simmap.incTurnCount();

                if (simmap.getTurnCount() >= simmap.getTurnLimit())
                {
                    break;
                }

                // 自分または相手チームにユニットが存在しない場合終了
                if (simmap.getUnitsList(teamcolor, true, true, false).Count == 0 || 
                    simmap.getUnitsList(teamcolor, false, false, true).Count == 0)
                    break;

            }

            return simmap;
        }

        // ノードの展開
        public static void development(Game_Tree n, int teamcolor)
        {
            List<Action> allActions = new List<Action>(); // 全合法手
            List<Unit> allUnits = n.board.getUnitsList(teamcolor, false, true, false);  // 未行動自ユニット

            // 全合法手を取得
            foreach (Unit u in allUnits)
            {
                List<Action> the_acts = AiTools2.getUnitActions(u, n.board);//選択したユニットの全行動
                allActions.AddRange(the_acts);
            }

            // 全合法手に対し子ノードとしての初期設定を定める
            foreach (Action act in allActions)
            {
                Game_Tree child = new Game_Tree();
                child.board = n.board.createDeepClone();

                // 行動済みユニットが存在しない場合(ターン変更直後)
                if (n.board.getUnitsList(teamcolor, true, false, false).Count == 0)
                {
                    child.board.incTurnCount();
                }

                child.act = act;
                child.depth = n.depth + 1;
                child.board.executeAction(act);
                n.next.Add(child);

                // 訪問回数(プレイアウト回数)をファジィ評価から定める
                // (プレイアウト結果は必ず勝利とする)
                child.simnum = (int)(Fuzzy_Table.returnValue(act, child.board) * 10);
                child.totalscore = child.simnum;
                child.housyuu = 1;                
            }
        }

        //UCB値計算
        public static double evaluateUCT(Game_Tree n)
        {
            if (n.simnum == 0)
                return 100;
            else
                return n.housyuu + UCB_CONST * Math.Sqrt(Math.Log(totalsim, Math.E) / n.simnum);
        }

        //状態評価関数(シミュレーション後) 戻り値は 1, 0.5, 0
        public static double evaluateStateValue(Map map, int teamcolor)
        {
            int enemycolor = getenemycolor(teamcolor); // 相手チームの色

            List<Unit> myTeamUnits = map.getUnitsList(teamcolor, true, true, false);//自分チームの全ユニット
            List<Unit> enemyTeamUnits = map.getUnitsList(enemycolor, true, true, false);//相手チームの全ユニット

            // 自分生存ユニットが0(負け)
            if (myTeamUnits.Count == 0)
            {
                return 0;
            }
            // 相手生存ユニットが0(勝ち)
            if (enemyTeamUnits.Count == 0)
            {
                return 1;
            }

            double mytotalHP = 0; // 自分HP合計値
            double enemytotalHP = 0; // 相手HP合計値

            // 自分HP合計値計算
            for (int i = 0; i < myTeamUnits.Count; i++)
            {
                mytotalHP += myTeamUnits[i].getHP();
            }
            // 相手HP合計値計算
            for (int i = 0; i < enemyTeamUnits.Count; i++)
            {
                enemytotalHP += enemyTeamUnits[i].getHP();
            }

            // 勝敗判定
            if (mytotalHP - enemytotalHP >= map.getDrawHPThreshold())
                return 1;
            else if (Math.Abs(mytotalHP - enemytotalHP) < map.getDrawHPThreshold())
                return 0.5;
            else
                return 0;
        }

        // enemycolor定義
        public static int getenemycolor(int teamcolor)
        {
            if (teamcolor == 1)
                return 0;
            else
                return 1;
        }

        // その時点で引数ユニットが次の相手ターンに受けうる最高ダメージ
        public static int possible_damaged(Map map, Unit unit, int teamcolor)
        {
            Map clone = map.createDeepClone(); // シミュレーション用マップ

            int enemycolor = getenemycolor(teamcolor); // 相手チームの色

            List<Unit> enemys = clone.getUnitsList(enemycolor, false, true, false);　// 相手ユニット

            int damage_sum = 0; //合計被ダメージ

            // 全相手ユニットが攻撃
            while (enemys.Count > 0)
            {
                int maxdamage = 0; // 最大ダメージ
                int unit_id = 0; // ユニットID
                Action tmpact = new Action(); // 行動の一時変数

                for (int j = 0; j < enemys.Count; j++)
                {
                    List<Action> attacks = RangeController.getAttackActionList(enemys[j], clone);

                    for (int i = 0; i < attacks.Count; i++)
                    {
                        if (attacks[i].targetUnitId != unit.getID()) continue;                      

                        int[] tmpdamage = DamageCalculator.calculateDamages(clone, attacks[i]);

                        if (tmpdamage[0] > maxdamage)
                        {
                            maxdamage = tmpdamage[0];
                            unit_id = j;
                            tmpact = attacks[i].createDeepClone();
                        }
                    }
                }

                if (maxdamage == 0)
                    break;
                else
                    damage_sum += maxdamage;

                clone.executeAction(tmpact);
                enemys.RemoveAt(unit_id);
            }

            if (damage_sum >= unit.getHP())
                damage_sum = unit.getHP();

            return damage_sum;
        }
    }
}
