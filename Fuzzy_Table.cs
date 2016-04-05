using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SimpleWars
{
    class Fuzzy_Table
    {
        private int hpval;   // hp 10
        private int optypeval;    // 攻撃対象の種類 7
        private int damageval;   // 攻撃ダメージ 11
        private int dangerval;   // 受けうるダメージ 11
        private int clogval;   // 塞がり度 5
        private int nearMyunitval;   // 周辺味方ユニット数 15
        private int nearOpunitval;   // 周辺敵ユニット数 15
        private int isLastAttackval;  // 最後に行動したかどうか 2
        private int difTotalhpval;   // 総HP和の差 31
        private int leftTurnval; // 残りターン数 40
        private int myFighterval;    // 味方戦闘機数 10
        private int myAttackerval;   // 味方攻撃機 10
        private int myPanzerval; // 味方戦車数 10
        private int myCannonval; // 味方自走砲数 10
        private int myAntiairval;    // 味方対空戦車数 10
        private int myInfantryval;   // 味方歩兵数 10
        private int opFighterval;    // 敵戦闘機数 10
        private int opAttackerval;   // 敵攻撃機 10
        private int opPanzerval; // 敵戦車数 10
        private int opCannonval; 　// 敵自走砲 10
        private int opAntiairval;    // 敵対空戦車数 10
        private int opInfantryval;   // 敵歩兵数 10

        private float value;

        private float membership;

        private static List<Fuzzy_Table> fighterTable = new List<Fuzzy_Table>();
        private static List<Fuzzy_Table> attackerTable = new List<Fuzzy_Table>();
        private static List<Fuzzy_Table> panzerTable = new List<Fuzzy_Table>();
        private static List<Fuzzy_Table> cannonTable = new List<Fuzzy_Table>();
        private static List<Fuzzy_Table> antiairTable = new List<Fuzzy_Table>();
        private static List<Fuzzy_Table> infantryTable = new List<Fuzzy_Table>();

        public Fuzzy_Table()
        {

        }

        public static Fuzzy_Table changeVector(Action act, Map map)
        {
            Fuzzy_Table vec = new Fuzzy_Table();
            Unit u = map.getUnit(act.operationUnitId);

            vec.hpval = u.getHP();
            vec.optypeval = act.X_targetType;
            vec.damageval = act.X_attackDamage;
            vec.dangerval = AI_F_UCT.possible_damaged(map, u, u.getTeamColor());

            vec.clogval = 0;
            vec.nearMyunitval = 0;
            vec.nearOpunitval = 0;

            bool[,] reachable_cells = RangeController.getReachableCellsMatrix(u, map);
            Unit[,] near_units = AiTools2.getNearUnitsMatrix(u, map);
            int[] DX = new int[4] { +1, 0, -1, 0 };
            int[] DY = new int[4] { 0, -1, 0, +1 };
            for (int r = 0; r < 4; r++)
            {
                int chx = u.getXpos() + DX[r]; // 上下左右
                int chy = u.getYpos() + DY[r];

                if (reachable_cells[chx, chy] == false)
                    vec.clogval += 1;
            }

            for (int y = 0; y < map.getYsize(); y++)
            {
                for (int x = 0; x < map.getXsize(); x++)
                {
                    if (near_units[x, y] == null) continue;

                    else if (near_units[x, y].getTeamColor() == u.getTeamColor()) vec.nearMyunitval += 1;

                    else vec.nearOpunitval += 1;
                }
            }

            vec.isLastAttackval = 0;
            if (map.getUnitsList(u.getTeamColor(), false, true, false).Count == 0)
                vec.isLastAttackval = 1;

            vec.difTotalhpval = 0;
            Unit[] allUnits = map.getUnits();

            foreach (Unit tmpu in allUnits)
            {
                if (tmpu == null) continue;

                if (tmpu.getTeamColor() == u.getTeamColor())
                    vec.difTotalhpval += tmpu.getHP();
                else
                    vec.difTotalhpval -= tmpu.getHP();
            }

            vec.leftTurnval = map.getTurnLimit() - map.getTurnCount();

            int[] myUnitNum = new int[6];
            int[] opUnitNum = new int[6];
            foreach (Unit tmpu in allUnits)
            {
                if (tmpu == null) continue;

                if (tmpu.getTeamColor() == u.getTeamColor())
                    myUnitNum[tmpu.getSpec().getUnitType()] += 1;
                else
                    opUnitNum[tmpu.getSpec().getUnitType()] += 1;
            }

            vec.myFighterval = myUnitNum[0];
            vec.myAttackerval = myUnitNum[1];
            vec.myPanzerval = myUnitNum[2];
            vec.myCannonval = myUnitNum[3];
            vec.myAntiairval = myUnitNum[4];
            vec.myInfantryval = myUnitNum[5];

            vec.opFighterval = opUnitNum[0];
            vec.opAttackerval = opUnitNum[1];
            vec.opPanzerval = opUnitNum[2];
            vec.opCannonval = opUnitNum[3];
            vec.opAntiairval = opUnitNum[4];
            vec.opInfantryval = opUnitNum[5];

            return vec;
        }

        public static void makeTables()
        {
            if (fighterTable.Count != 0 || attackerTable.Count != 0 || panzerTable.Count != 0 || cannonTable.Count != 0 || antiairTable.Count != 0 || infantryTable.Count != 0)
                return;

            using (StreamReader r = new StreamReader("fuzzy_table_fighter_member.csv", true))
            {
                if (r != null)
                {
                    while (!r.EndOfStream)
                    {
                        // 1行読み込む
                        String line = r.ReadLine();

                        // カンマ毎に分割して配列に格納
                        String[] strValues = line.Split(',');
                        int[] values = new int[strValues.Length - 2];
                        for (int i = 0; i < strValues.Length - 2; i++)
                        {
                            values[i] = int.Parse(strValues[i]);
                        }
                        float evalValue = float.Parse(strValues[strValues.Length - 2]);
                        float member = float.Parse(strValues[strValues.Length - 1]);

                        // main
                        Fuzzy_Table data = new Fuzzy_Table();

                        data.hpval = values[0];
                        data.optypeval = values[1];
                        data.damageval = values[2];
                        data.dangerval = values[3];
                        data.clogval = values[4];
                        data.nearMyunitval = values[5];
                        data.nearOpunitval = values[6];
                        data.isLastAttackval = values[7];
                        data.difTotalhpval = values[8];
                        data.leftTurnval = values[9];
                        data.myFighterval = values[10];
                        data.myAttackerval = values[11];
                        data.myPanzerval = values[12];
                        data.myCannonval = values[13];
                        data.myAntiairval = values[14];
                        data.myInfantryval = values[15];
                        data.opFighterval = values[16];
                        data.opAttackerval = values[17];
                        data.opPanzerval = values[18];
                        data.opCannonval = values[19];
                        data.opAntiairval = values[20];
                        data.opInfantryval = values[21];
                        data.value = evalValue;
                        data.membership = member;

                        fighterTable.Add(data);
                    }
                }
            }

            using (StreamReader r = new StreamReader("fuzzy_table_attacker_member.csv", true))
            {
                if (r != null)
                {
                    while (!r.EndOfStream)
                    {
                        // 1行読み込む
                        String line = r.ReadLine();

                        // カンマ毎に分割して配列に格納
                        String[] strValues = line.Split(',');
                        int[] values = new int[strValues.Length - 2];
                        for (int i = 0; i < strValues.Length - 2; i++)
                        {
                            values[i] = int.Parse(strValues[i]);
                        }
                        float evalValue = float.Parse(strValues[strValues.Length - 2]);
                        float member = float.Parse(strValues[strValues.Length - 1]);

                        // main
                        Fuzzy_Table data = new Fuzzy_Table();

                        data.hpval = values[0];
                        data.optypeval = values[1];
                        data.damageval = values[2];
                        data.dangerval = values[3];
                        data.clogval = values[4];
                        data.nearMyunitval = values[5];
                        data.nearOpunitval = values[6];
                        data.isLastAttackval = values[7];
                        data.difTotalhpval = values[8];
                        data.leftTurnval = values[9];
                        data.myFighterval = values[10];
                        data.myAttackerval = values[11];
                        data.myPanzerval = values[12];
                        data.myCannonval = values[13];
                        data.myAntiairval = values[14];
                        data.myInfantryval = values[15];
                        data.opFighterval = values[16];
                        data.opAttackerval = values[17];
                        data.opPanzerval = values[18];
                        data.opCannonval = values[19];
                        data.opAntiairval = values[20];
                        data.opInfantryval = values[21];
                        data.value = evalValue;
                        data.membership = member;

                        attackerTable.Add(data);
                    }
                }
            }

            using (StreamReader r = new StreamReader("fuzzy_table_panzer_member.csv", true))
            {
                if (r != null)
                {
                    while (!r.EndOfStream)
                    {
                        // 1行読み込む
                        String line = r.ReadLine();

                        // カンマ毎に分割して配列に格納
                        String[] strValues = line.Split(',');
                        int[] values = new int[strValues.Length - 2];
                        for (int i = 0; i < strValues.Length - 2; i++)
                        {
                            values[i] = int.Parse(strValues[i]);
                        }
                        float evalValue = float.Parse(strValues[strValues.Length - 2]);
                        float member = float.Parse(strValues[strValues.Length - 1]);

                        // main
                        Fuzzy_Table data = new Fuzzy_Table();

                        data.hpval = values[0];
                        data.optypeval = values[1];
                        data.damageval = values[2];
                        data.dangerval = values[3];
                        data.clogval = values[4];
                        data.nearMyunitval = values[5];
                        data.nearOpunitval = values[6];
                        data.isLastAttackval = values[7];
                        data.difTotalhpval = values[8];
                        data.leftTurnval = values[9];
                        data.myFighterval = values[10];
                        data.myAttackerval = values[11];
                        data.myPanzerval = values[12];
                        data.myCannonval = values[13];
                        data.myAntiairval = values[14];
                        data.myInfantryval = values[15];
                        data.opFighterval = values[16];
                        data.opAttackerval = values[17];
                        data.opPanzerval = values[18];
                        data.opCannonval = values[19];
                        data.opAntiairval = values[20];
                        data.opInfantryval = values[21];
                        data.value = evalValue;
                        data.membership = member;

                        panzerTable.Add(data);
                    }
                }
            }

            using (StreamReader r = new StreamReader("fuzzy_table_cannon_member.csv", true))
            {
                if (r != null)
                {
                    while (!r.EndOfStream)
                    {
                        // 1行読み込む
                        String line = r.ReadLine();

                        // カンマ毎に分割して配列に格納
                        String[] strValues = line.Split(',');
                        int[] values = new int[strValues.Length - 2];
                        for (int i = 0; i < strValues.Length - 2; i++)
                        {
                            values[i] = int.Parse(strValues[i]);
                        }
                        float evalValue = float.Parse(strValues[strValues.Length - 2]);
                        float member = float.Parse(strValues[strValues.Length - 1]);

                        // main
                        Fuzzy_Table data = new Fuzzy_Table();

                        data.hpval = values[0];
                        data.optypeval = values[1];
                        data.damageval = values[2];
                        data.dangerval = values[3];
                        data.clogval = values[4];
                        data.nearMyunitval = values[5];
                        data.nearOpunitval = values[6];
                        data.isLastAttackval = values[7];
                        data.difTotalhpval = values[8];
                        data.leftTurnval = values[9];
                        data.myFighterval = values[10];
                        data.myAttackerval = values[11];
                        data.myPanzerval = values[12];
                        data.myCannonval = values[13];
                        data.myAntiairval = values[14];
                        data.myInfantryval = values[15];
                        data.opFighterval = values[16];
                        data.opAttackerval = values[17];
                        data.opPanzerval = values[18];
                        data.opCannonval = values[19];
                        data.opAntiairval = values[20];
                        data.opInfantryval = values[21];
                        data.value = evalValue;
                        data.membership = member;

                        cannonTable.Add(data);
                    }
                }
            }

            using (StreamReader r = new StreamReader("fuzzy_table_antiair_member.csv", true))
            {
                if (r != null)
                {
                    while (!r.EndOfStream)
                    {
                        // 1行読み込む
                        String line = r.ReadLine();

                        // カンマ毎に分割して配列に格納
                        String[] strValues = line.Split(',');
                        int[] values = new int[strValues.Length - 2];
                        for (int i = 0; i < strValues.Length - 2; i++)
                        {
                            values[i] = int.Parse(strValues[i]);
                        }
                        float evalValue = float.Parse(strValues[strValues.Length - 2]);
                        float member = float.Parse(strValues[strValues.Length - 1]);

                        // main
                        Fuzzy_Table data = new Fuzzy_Table();

                        data.hpval = values[0];
                        data.optypeval = values[1];
                        data.damageval = values[2];
                        data.dangerval = values[3];
                        data.clogval = values[4];
                        data.nearMyunitval = values[5];
                        data.nearOpunitval = values[6];
                        data.isLastAttackval = values[7];
                        data.difTotalhpval = values[8];
                        data.leftTurnval = values[9];
                        data.myFighterval = values[10];
                        data.myAttackerval = values[11];
                        data.myPanzerval = values[12];
                        data.myCannonval = values[13];
                        data.myAntiairval = values[14];
                        data.myInfantryval = values[15];
                        data.opFighterval = values[16];
                        data.opAttackerval = values[17];
                        data.opPanzerval = values[18];
                        data.opCannonval = values[19];
                        data.opAntiairval = values[20];
                        data.opInfantryval = values[21];
                        data.value = evalValue;
                        data.membership = member;

                        antiairTable.Add(data);
                    }
                }
            }

            using (StreamReader r = new StreamReader("fuzzy_table_infantry_member.csv", true))
            {
                if (r != null)
                {
                    while (!r.EndOfStream)
                    {
                        // 1行読み込む
                        String line = r.ReadLine();

                        // カンマ毎に分割して配列に格納
                        String[] strValues = line.Split(',');
                        int[] values = new int[strValues.Length - 2];
                        for (int i = 0; i < strValues.Length - 2; i++)
                        {
                            values[i] = int.Parse(strValues[i]);
                        }
                        float evalValue = float.Parse(strValues[strValues.Length - 2]);
                        float member = float.Parse(strValues[strValues.Length - 1]);

                        // main
                        Fuzzy_Table data = new Fuzzy_Table();

                        data.hpval = values[0];
                        data.optypeval = values[1];
                        data.damageval = values[2];
                        data.dangerval = values[3];
                        data.clogval = values[4];
                        data.nearMyunitval = values[5];
                        data.nearOpunitval = values[6];
                        data.isLastAttackval = values[7];
                        data.difTotalhpval = values[8];
                        data.leftTurnval = values[9];
                        data.myFighterval = values[10];
                        data.myAttackerval = values[11];
                        data.myPanzerval = values[12];
                        data.myCannonval = values[13];
                        data.myAntiairval = values[14];
                        data.myInfantryval = values[15];
                        data.opFighterval = values[16];
                        data.opAttackerval = values[17];
                        data.opPanzerval = values[18];
                        data.opCannonval = values[19];
                        data.opAntiairval = values[20];
                        data.opInfantryval = values[21];
                        data.value = evalValue;
                        data.membership = member;

                        infantryTable.Add(data);
                    }
                }
            }
        }

        public static void showTables(int teamcolor)
        {
            Logger.log("Fighter Table.\r\n", teamcolor);
            foreach (Fuzzy_Table fighterData in fighterTable)
            {
                Logger.log(fighterData.hpval + ",", teamcolor);
                Logger.log(fighterData.optypeval + ",", teamcolor);
                Logger.log(fighterData.damageval + ",", teamcolor);
                Logger.log(fighterData.dangerval + ",", teamcolor);
                Logger.log(fighterData.clogval + ",", teamcolor);
                Logger.log(fighterData.nearMyunitval + ",", teamcolor);
                Logger.log(fighterData.nearOpunitval + ",", teamcolor);
                Logger.log(fighterData.isLastAttackval + ",", teamcolor);
                Logger.log(fighterData.difTotalhpval + ",", teamcolor);
                Logger.log(fighterData.leftTurnval + ",", teamcolor);
                Logger.log(fighterData.myFighterval + ",", teamcolor);
                Logger.log(fighterData.myAttackerval + ",", teamcolor);
                Logger.log(fighterData.myPanzerval + ",", teamcolor);
                Logger.log(fighterData.myCannonval + ",", teamcolor);
                Logger.log(fighterData.myAntiairval + ",", teamcolor);
                Logger.log(fighterData.myInfantryval + ",", teamcolor);
                Logger.log(fighterData.opFighterval + ",", teamcolor);
                Logger.log(fighterData.opAttackerval + ",", teamcolor);
                Logger.log(fighterData.opPanzerval + ",", teamcolor);
                Logger.log(fighterData.opCannonval + ",", teamcolor);
                Logger.log(fighterData.opAntiairval + ",", teamcolor);
                Logger.log(fighterData.opInfantryval + ",", teamcolor);
                Logger.log(fighterData.value + ",", teamcolor);
                Logger.log(fighterData.membership + "\r\n", teamcolor); 
            }

            Logger.log("Attacker Table.\r\n", teamcolor);
            foreach (Fuzzy_Table attackerData in attackerTable)
            {
                Logger.log(attackerData.hpval + ",", teamcolor);
                Logger.log(attackerData.optypeval + ",", teamcolor);
                Logger.log(attackerData.damageval + ",", teamcolor);
                Logger.log(attackerData.dangerval + ",", teamcolor);
                Logger.log(attackerData.clogval + ",", teamcolor);
                Logger.log(attackerData.nearMyunitval + ",", teamcolor);
                Logger.log(attackerData.nearOpunitval + ",", teamcolor);
                Logger.log(attackerData.isLastAttackval + ",", teamcolor);
                Logger.log(attackerData.difTotalhpval + ",", teamcolor);
                Logger.log(attackerData.leftTurnval + ",", teamcolor);
                Logger.log(attackerData.myFighterval + ",", teamcolor);
                Logger.log(attackerData.myAttackerval + ",", teamcolor);
                Logger.log(attackerData.myPanzerval + ",", teamcolor);
                Logger.log(attackerData.myCannonval + ",", teamcolor);
                Logger.log(attackerData.myAntiairval + ",", teamcolor);
                Logger.log(attackerData.myInfantryval + ",", teamcolor);
                Logger.log(attackerData.opFighterval + ",", teamcolor);
                Logger.log(attackerData.opAttackerval + ",", teamcolor);
                Logger.log(attackerData.opPanzerval + ",", teamcolor);
                Logger.log(attackerData.opCannonval + ",", teamcolor);
                Logger.log(attackerData.opAntiairval + ",", teamcolor);
                Logger.log(attackerData.opInfantryval + ",", teamcolor);
                Logger.log(attackerData.value + ",", teamcolor);
                Logger.log(attackerData.membership + "\r\n", teamcolor); 
            }

            Logger.log("Panzer Table.\r\n", teamcolor);
            foreach (Fuzzy_Table panzerData in panzerTable)
            {
                Logger.log(panzerData.hpval + ",", teamcolor);
                Logger.log(panzerData.optypeval + ",", teamcolor);
                Logger.log(panzerData.damageval + ",", teamcolor);
                Logger.log(panzerData.dangerval + ",", teamcolor);
                Logger.log(panzerData.clogval + ",", teamcolor);
                Logger.log(panzerData.nearMyunitval + ",", teamcolor);
                Logger.log(panzerData.nearOpunitval + ",", teamcolor);
                Logger.log(panzerData.isLastAttackval + ",", teamcolor);
                Logger.log(panzerData.difTotalhpval + ",", teamcolor);
                Logger.log(panzerData.leftTurnval + ",", teamcolor);
                Logger.log(panzerData.myFighterval + ",", teamcolor);
                Logger.log(panzerData.myAttackerval + ",", teamcolor);
                Logger.log(panzerData.myPanzerval + ",", teamcolor);
                Logger.log(panzerData.myCannonval + ",", teamcolor);
                Logger.log(panzerData.myAntiairval + ",", teamcolor);
                Logger.log(panzerData.myInfantryval + ",", teamcolor);
                Logger.log(panzerData.opFighterval + ",", teamcolor);
                Logger.log(panzerData.opAttackerval + ",", teamcolor);
                Logger.log(panzerData.opPanzerval + ",", teamcolor);
                Logger.log(panzerData.opCannonval + ",", teamcolor);
                Logger.log(panzerData.opAntiairval + ",", teamcolor);
                Logger.log(panzerData.opInfantryval + ",", teamcolor);
                Logger.log(panzerData.value + ",", teamcolor);
                Logger.log(panzerData.membership + "\r\n", teamcolor); 
            }

            Logger.log("Cannon Table.\r\n", teamcolor);
            foreach (Fuzzy_Table cannonData in cannonTable)
            {
                Logger.log(cannonData.hpval + ",", teamcolor);
                Logger.log(cannonData.optypeval + ",", teamcolor);
                Logger.log(cannonData.damageval + ",", teamcolor);
                Logger.log(cannonData.dangerval + ",", teamcolor);
                Logger.log(cannonData.clogval + ",", teamcolor);
                Logger.log(cannonData.nearMyunitval + ",", teamcolor);
                Logger.log(cannonData.nearOpunitval + ",", teamcolor);
                Logger.log(cannonData.isLastAttackval + ",", teamcolor);
                Logger.log(cannonData.difTotalhpval + ",", teamcolor);
                Logger.log(cannonData.leftTurnval + ",", teamcolor);
                Logger.log(cannonData.myFighterval + ",", teamcolor);
                Logger.log(cannonData.myAttackerval + ",", teamcolor);
                Logger.log(cannonData.myPanzerval + ",", teamcolor);
                Logger.log(cannonData.myCannonval + ",", teamcolor);
                Logger.log(cannonData.myAntiairval + ",", teamcolor);
                Logger.log(cannonData.myInfantryval + ",", teamcolor);
                Logger.log(cannonData.opFighterval + ",", teamcolor);
                Logger.log(cannonData.opAttackerval + ",", teamcolor);
                Logger.log(cannonData.opPanzerval + ",", teamcolor);
                Logger.log(cannonData.opCannonval + ",", teamcolor);
                Logger.log(cannonData.opAntiairval + ",", teamcolor);
                Logger.log(cannonData.opInfantryval + ",", teamcolor);
                Logger.log(cannonData.value + ",", teamcolor);
                Logger.log(cannonData.membership + "\r\n", teamcolor); 
            }

            Logger.log("Antiair Table.\r\n", teamcolor);
            foreach (Fuzzy_Table antiairData in antiairTable)
            {
                Logger.log(antiairData.hpval + ",", teamcolor);
                Logger.log(antiairData.optypeval + ",", teamcolor);
                Logger.log(antiairData.damageval + ",", teamcolor);
                Logger.log(antiairData.dangerval + ",", teamcolor);
                Logger.log(antiairData.clogval + ",", teamcolor);
                Logger.log(antiairData.nearMyunitval + ",", teamcolor);
                Logger.log(antiairData.nearOpunitval + ",", teamcolor);
                Logger.log(antiairData.isLastAttackval + ",", teamcolor);
                Logger.log(antiairData.difTotalhpval + ",", teamcolor);
                Logger.log(antiairData.leftTurnval + ",", teamcolor);
                Logger.log(antiairData.myFighterval + ",", teamcolor);
                Logger.log(antiairData.myAttackerval + ",", teamcolor);
                Logger.log(antiairData.myPanzerval + ",", teamcolor);
                Logger.log(antiairData.myCannonval + ",", teamcolor);
                Logger.log(antiairData.myAntiairval + ",", teamcolor);
                Logger.log(antiairData.myInfantryval + ",", teamcolor);
                Logger.log(antiairData.opFighterval + ",", teamcolor);
                Logger.log(antiairData.opAttackerval + ",", teamcolor);
                Logger.log(antiairData.opPanzerval + ",", teamcolor);
                Logger.log(antiairData.opCannonval + ",", teamcolor);
                Logger.log(antiairData.opAntiairval + ",", teamcolor);
                Logger.log(antiairData.opInfantryval + ",", teamcolor);
                Logger.log(antiairData.value + ",", teamcolor);
                Logger.log(antiairData.membership + "\r\n", teamcolor); 
            }

            Logger.log("Infantry Table.\r\n", teamcolor);
            foreach (Fuzzy_Table infantryData in infantryTable)
            {
                Logger.log(infantryData.hpval + ",", teamcolor);
                Logger.log(infantryData.optypeval + ",", teamcolor);
                Logger.log(infantryData.damageval + ",", teamcolor);
                Logger.log(infantryData.dangerval + ",", teamcolor);
                Logger.log(infantryData.clogval + ",", teamcolor);
                Logger.log(infantryData.nearMyunitval + ",", teamcolor);
                Logger.log(infantryData.nearOpunitval + ",", teamcolor);
                Logger.log(infantryData.isLastAttackval + ",", teamcolor);
                Logger.log(infantryData.difTotalhpval + ",", teamcolor);
                Logger.log(infantryData.leftTurnval + ",", teamcolor);
                Logger.log(infantryData.myFighterval + ",", teamcolor);
                Logger.log(infantryData.myAttackerval + ",", teamcolor);
                Logger.log(infantryData.myPanzerval + ",", teamcolor);
                Logger.log(infantryData.myCannonval + ",", teamcolor);
                Logger.log(infantryData.myAntiairval + ",", teamcolor);
                Logger.log(infantryData.myInfantryval + ",", teamcolor);
                Logger.log(infantryData.opFighterval + ",", teamcolor);
                Logger.log(infantryData.opAttackerval + ",", teamcolor);
                Logger.log(infantryData.opPanzerval + ",", teamcolor);
                Logger.log(infantryData.opCannonval + ",", teamcolor);
                Logger.log(infantryData.opAntiairval + ",", teamcolor);
                Logger.log(infantryData.opInfantryval + ",", teamcolor);
                Logger.log(infantryData.value + ",", teamcolor);
                Logger.log(infantryData.membership + "\r\n", teamcolor); 
            }
        }

        public static float returnValue(Action act, Map map)
        {
            Unit u = map.getUnit(act.operationUnitId);
            if (u == null) return 0.0f;

            if (act.destinationXpos == act.fromXpos && act.destinationYpos == act.fromYpos && act.actionType == 1)
                return 1.0f;

            Fuzzy_Table vec = changeVector(act, map);

            List<Fuzzy_Table> table_list = new List<Fuzzy_Table>();
            // fighter action
            if (u.getSpec().getUnitType() == 0)
            {
                table_list = fighterTable;
            }
            // attacker action
            if (u.getSpec().getUnitType() == 1)
            {
                table_list = attackerTable;
            }
            // panzer action
            if (u.getSpec().getUnitType() == 2)
            {
                table_list = panzerTable;
            }
            // cannon action
            if (u.getSpec().getUnitType() == 3)
            {
                table_list = cannonTable;
            }
            // antiari action
            if (u.getSpec().getUnitType() == 4)
            {
                table_list = antiairTable;
            }
            // infantry action
            if (u.getSpec().getUnitType() == 5)
            {
                table_list = infantryTable;
            }


            float mindist = float.MaxValue;
            int counter = 1;
            float nearvalue = 0.0f;


            foreach (Fuzzy_Table tmp in table_list)
            {
                if (vec.hpval == tmp.hpval && vec.optypeval == tmp.optypeval && vec.damageval == tmp.damageval && vec.dangerval == tmp.dangerval &&
                    vec.clogval == tmp.clogval && vec.nearMyunitval == tmp.nearMyunitval && vec.nearOpunitval == tmp.nearOpunitval &&
                    vec.isLastAttackval == tmp.isLastAttackval && vec.difTotalhpval == tmp.difTotalhpval && vec.leftTurnval == tmp.leftTurnval &&
                    vec.myFighterval == tmp.myFighterval && vec.myAttackerval == tmp.myAttackerval && vec.myPanzerval == tmp.myPanzerval &&
                    vec.myCannonval == tmp.myCannonval && vec.myAntiairval == tmp.myAntiairval && vec.myInfantryval == tmp.myInfantryval &&
                    vec.opFighterval == tmp.opFighterval && vec.opAttackerval == tmp.opAttackerval && vec.opPanzerval == tmp.opPanzerval &&
                    vec.opCannonval == tmp.opCannonval && vec.opAntiairval == tmp.opAntiairval && vec.opInfantryval == tmp.opInfantryval)
                    return tmp.membership;
                else
                {
                    float tmpdist = (float)Math.Sqrt(Math.Pow((vec.hpval - tmp.hpval), 2) + Math.Pow((vec.optypeval - tmp.optypeval), 2) + 
                        Math.Pow((vec.damageval - tmp.damageval), 2) + Math.Pow((vec.dangerval - tmp.dangerval), 2) + 
                        Math.Pow((vec.clogval - tmp.clogval), 2) + Math.Pow((vec.nearMyunitval - tmp.nearMyunitval), 2) + 
                        Math.Pow((vec.nearOpunitval - tmp.nearOpunitval), 2) + Math.Pow((vec.isLastAttackval - tmp.isLastAttackval), 2) + 
                        Math.Pow((vec.difTotalhpval - tmp.difTotalhpval), 2) + Math.Pow((vec.leftTurnval - tmp.leftTurnval), 2) + 
                        Math.Pow((vec.myFighterval - tmp.myFighterval), 2) + Math.Pow((vec.myAttackerval - tmp.myAttackerval), 2) + 
                        Math.Pow((vec.myPanzerval - tmp.myPanzerval), 2) + Math.Pow((vec.myCannonval - tmp.myCannonval), 2) + 
                        Math.Pow((vec.myAntiairval - tmp.myAntiairval), 2) + Math.Pow((vec.myInfantryval - tmp.myInfantryval), 2) +
                        Math.Pow((vec.opFighterval - tmp.opFighterval), 2) + Math.Pow((vec.opAttackerval - tmp.opAttackerval), 2) +
                        Math.Pow((vec.opPanzerval - tmp.opPanzerval), 2) + Math.Pow((vec.opCannonval - tmp.opCannonval), 2) +
                        Math.Pow((vec.opAntiairval - tmp.opAntiairval), 2) + Math.Pow((vec.opInfantryval - tmp.opInfantryval), 2));

                    if (tmpdist < mindist)
                    {
                        nearvalue = tmp.membership;
                        mindist = tmpdist;
                        counter = 1;
                    }
                    else if (tmpdist == mindist)
                    {
                        nearvalue += tmp.membership;
                        counter += 1;
                    }
                }
            }

            return nearvalue / counter;
        }
    }
}
