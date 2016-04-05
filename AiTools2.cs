using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleWars {
    /// <summary>
    /// システムは使わない，思考ルーチンを作るのにあるとうれしいような関数たちです．
	/// 拡充予定です
    /// </summary>
    class AiTools2 {

        // 攻撃行動可能な自ユニット・敵ユニット・攻撃位置のリストを ActionのListとして取得する
        public static List<Action> getAllAttackActions(int teamcolor, Map map) {
            List<Action> atkActions = new List<Action>();

            // 行動可能な自ユニットリスト
            List<Unit> myMovableUnits = map.getUnitsList(teamcolor, false, true, false);

            foreach (Unit myUnit in myMovableUnits) {
                // movableUnitsの攻撃行動リスト
                List<Action> atkActionsOfOne = RangeController.getAttackActionList(myUnit, map);

                // 全体に含める
                atkActions.AddRange(atkActionsOfOne);
            }

            return atkActions;
        }

        public static List<Action> getAllMoveActions(int teamcolor, Map map) {
            List<Action> moveActions = new List<Action>();

            // 行動可能な自ユニットリスト
            List<Unit> myMovableUnits = map.getUnitsList(teamcolor, false, true, false);

            //行動可能なユニットの移動可能箇所をリストにまとめる
            foreach (Unit myUnit in myMovableUnits) {
                bool[,] reachable = RangeController.getReachableCellsMatrix(myUnit, map);
                for (int i = 0; i < reachable.GetLength(0); i++) {
                    for (int j = 0; j < reachable.GetLength(1); j++) {
                        if (reachable[i, j]) {
                            moveActions.Add(Action.createMoveOnlyAction(myUnit, i, j));
                        }
                    }
                }
            }

            return moveActions;
        }

        // operationUnitがtargetUnitに攻撃可能かどうか．ダメージ効果0の場合にはfalseが返る
        public static bool isEffective(Unit operationUnit, Unit targetUnit) {
            if (operationUnit.getSpec().getUnitAtkPower(targetUnit.getTypeOfUnit()) != 0) { return true; }
            return false;
        }

        /// <summary>
        /// 現在の状態における全合法手を生成する
        /// </summary>
        /// <param name="teamColor"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static List<Action> getAllActions(int teamColor, Map map)
        {
            List<Action> allActions = new List<Action>();

            allActions.AddRange(getAllAttackActions(teamColor, map));
            allActions.AddRange(getAllMoveActions(teamColor, map));

            return allActions;
        }





        /* ------------------------------------ 以下追加関数 ------------------------------------------*/





        //ユニットの全ての行動のリストを取得する
        public static List<Action> getUnitActions(Unit unit, Map map)
        {
            List<Action> unitactions = new List<Action>();

            List<Action> atkActions = RangeController.getAttackActionList(unit, map);

            foreach (Action atk in atkActions)
            {
                int[] attackDamages = DamageCalculator.calculateDamages(map, atk); // 攻撃，反撃ダメージを計算

                if (attackDamages[0] != 0)
                {
                    unitactions.Add(atk);
                }
            }

            List<Action> moveactions = new List<Action>();

            bool[,] movable = RangeController.getReachableCellsMatrix(unit, map);

            for (int x = 1; x < map.getXsize() - 1; x++)
            {
                for (int y = 1; y < map.getYsize() - 1; y++)
                {
                    if (movable[x, y] == true)
                    {
                        moveactions.Add(Action.createMoveOnlyAction(unit, x, y));
                    }
                }
            }

            unitactions.AddRange(moveactions);

            return unitactions;
        }

        // 攻撃行動可能な自ユニットのリストをUnitのListとして取得する
        public static List<Unit> getAllAttackUnits(int teamcolor, Map map)
        {
            List<Unit> atkUnits = new List<Unit>();

            // 行動可能な自ユニットリスト
            List<Unit> myMovableUnits = map.getUnitsList(teamcolor, false, true, false);

            foreach (Unit myUnit in myMovableUnits)
            {
                // movableUnitsの攻撃行動リスト
                List<Action> atkActionsOfOne = RangeController.getAttackActionList(myUnit, map);

                if (atkActionsOfOne != null)
                {
                    atkUnits.Add(myUnit);
                }
            }

            return atkUnits;
        }

        // opUnitの移動範囲にあるユニットを Unit の matrix で返す関数
        // ユニットが存在しないマス、そもそも移動範囲外のマスはNULL
        public static Unit[,] getNearUnitsMatrix(Unit opUnit, Map map)
        {
            int[] DX = new int[4] { +1, 0, -1, 0 };
            int[] DY = new int[4] { 0, -1, 0, +1 };

            Spec unitSpec = opUnit.getSpec();  // 動かすユニットのSpec
            int unitStep = unitSpec.getUnitStep(); // ステップ数
            int unitColor = opUnit.getTeamColor(); // 動かすユニットのチーム

            bool[,] reachable = new bool[map.getXsize(), map.getYsize()];
            Unit[,] nearUnits = new Unit[map.getXsize(), map.getYsize()];  // UnitのMatrix．これを返す

            int[] posCounts = new int[unitStep + 1];  // 残り移動力 index になる移動可能箇所数．0で初期化される
            int[,] posX = new int[unitStep + 1, 100]; // 残り移動力 index になる移動可能箇所． 100は適当な数，本来 Const化すべき
            int[,] posY = new int[unitStep + 1, 100];

            // 今いる場所は残り移動力 unitStep の移動可能箇所であり，最終的にも移動可能
            posCounts[unitStep] = 1;
            posX[unitStep, 0] = opUnit.getXpos();
            posY[unitStep, 0] = opUnit.getYpos();
            reachable[opUnit.getXpos(), opUnit.getYpos()] = true;

            // 残り移動力 restStep になる移動可能箇所から，その上下左右を見て，移動できるならリストに追加していく
            for (int restStep = unitStep; restStep > 0; restStep--)
            { // 現在の残り移動コスト
                for (int i = 0; i < posCounts[restStep]; i++)
                {
                    int x = posX[restStep, i];  // 注目する場所
                    int y = posY[restStep, i];

                    // この (x,y) の上下左右を見る
                    for (int r = 0; r < 4; r++)
                    {
                        int newx = x + DX[r]; // 上下左右
                        int newy = y + DY[r];

                        int newrest = restStep - unitSpec.getMoveCost(map.getFieldType(newx, newy)); // 移動後の残り移動コスト
                        if (newrest < 0) continue;  // 周囲にあたったか，移動力が足りない →進入不可

                        Unit u = map.getUnit(newx, newy);
                        if (u != null)
                        {
                            if (nearUnits[newx, newy] == null)
                                nearUnits[newx, newy] = u;

                            if (u.getTeamColor() != unitColor) continue;  // 敵ユニットにあたった →進入不可
                        }

                        // すでに移動可能マークがついた場所じゃなければ，移動可能箇所に追加する
                        if (reachable[newx, newy] == false)
                        {
                            posX[newrest, posCounts[newrest]] = newx;
                            posY[newrest, posCounts[newrest]] = newy;
                            posCounts[newrest]++;
                            reachable[newx, newy] = true;
                        }
                    }
                }
            }

            // 味方ユニットがいる場所は，最終的には移動可能な箇所ではない
            foreach (Unit u in map.getUnitsList(unitColor, true, true, true))
            {
                reachable[u.getXpos(), u.getYpos()] = false;
            }

            // 自分の場所をどう考えるかは自由だが，移動可能とするなら
            reachable[opUnit.getXpos(), opUnit.getYpos()] = true;

            // 今いる場所は残り移動力 unitStep の移動可能箇所であるが，あるのは自分ユニットなのでNULL
            nearUnits[opUnit.getXpos(), opUnit.getYpos()] = null;

            return nearUnits;
        }
    }
}
