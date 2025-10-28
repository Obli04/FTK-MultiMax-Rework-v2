using System.Collections.Generic;
using UnityEngine;
using static FTK_MultiMax_Rework.Main;
using System.Reflection;

namespace FTK_MultiMax_Rework
{
    public class DummiesHandler
    {
        public static void CreateDummies()
        {
            Log("Making Dummies");
            List<GameObject> dummies = new List<GameObject>();

            var originalDummies = FTKHub.Instance.m_Dummies;

            // --- Player Dummies ---
            for (int j = 0; j < Mathf.Max(3, GameFlowMC.gMaxPlayers); j++)
            {
                if (j < 3)
                {
                    dummies.Add(originalDummies[j]);
                    continue;
                }

                GameObject copy2 = Object.Instantiate(originalDummies[2], originalDummies[2].transform.parent);
                copy2.name = "Player " + (j + 1) + " Dummy";
                copy2.GetComponent<PhotonView>().viewID = 3245 + j;
                dummies.Add(copy2);
            }

            // --- Enemy Dummies ---
            for (int i = 0; i < Mathf.Max(3, GameFlowMC.gMaxEnemies); i++)
            {
                if (i < 3)
                {
                    dummies.Add(originalDummies[i + 3]);
                    continue;
                }

                GameObject copy = Object.Instantiate(originalDummies[5], originalDummies[5].transform.parent);
                copy.name = "Enemy " + (i + 1) + " Dummy";
                copy.GetComponent<PhotonView>().viewID = 3045 + i;

                // --- 🧠 Fix for cloned enemy AI crash ---
                var ai = copy.GetComponent<EnemyDummy>();
                if (ai != null && ai.m_AttackSchedule != null)
                {
                    var schedule = ai.m_AttackSchedule;
                    var type = schedule.GetType();

                    // Get attack list and current index
                    var fldList = type.GetField("m_AttackTypes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var fldIndex = type.GetField("m_CurrentAttackIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    if (fldList != null)
                    {
                        var list = fldList.GetValue(schedule) as System.Collections.IList;
                        if (list != null && list.Count > 0)
                        {
                            // Keep only the first safe attack
                            var newList = System.Activator.CreateInstance(list.GetType()) as System.Collections.IList;
                            newList.Add(list[0]);
                            fldList.SetValue(schedule, newList);

                            // Reset index to 0 if field exists
                            if (fldIndex != null)
                                fldIndex.SetValue(schedule, 0);

                            Log($"[MultiMax] Reset attack schedule for {copy.name} (1 safe entry)");
                        }
                    }
                }
                dummies.Add(copy);
            }

            FTKHub.Instance.m_Dummies = dummies.ToArray();
            Log("Dummies created");
        }

        public static GameObject CreateDummy(GameObject[] source, int index, string prefix)
        {
            GameObject dummy;
            if (index < 3)
            {
                dummy = source[index];
            }
            else
            {
                dummy = Object.Instantiate(source[2], source[2].transform.parent);
                dummy.name = $"{prefix} {index + 1} Dummy";
                dummy.GetComponent<PhotonView>().viewID = 3245 + index;
            }
            return dummy;
        }
    }
}
