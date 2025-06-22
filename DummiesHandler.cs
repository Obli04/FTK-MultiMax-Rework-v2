using System.Collections.Generic;
using UnityEngine;
using static FTK_MultiMax_Rework_v2.Main;

namespace FTK_MultiMax_Rework_v2 {
    public class DummiesHandler {
        
        // [Polars Bear] TODO: Cleanup
        public static void CreateDummies() {
            Log("Making Dummies");
            List<GameObject> dummies = new List<GameObject>();
            
            for (int j = 0; j < Mathf.Max(3, GameFlowMC.gMaxPlayers); j++) {
                if (j < 3) {
                    dummies.Add(FTKHub.Instance.m_Dummies[j]);
                    continue;
                }
                GameObject copy2 = Object.Instantiate(FTKHub.Instance.m_Dummies[2], FTKHub.Instance.m_Dummies[2].transform.parent);
                copy2.name = "Player " + (j + 1) + " Dummy";
                copy2.GetComponent<PhotonView>().viewID = 3245 + j;
                dummies.Add(copy2);
            }
            
            for (int i = 0; i < Mathf.Max(3, GameFlowMC.gMaxEnemies); i++) {
                if (i < 3) {
                    dummies.Add(FTKHub.Instance.m_Dummies[i + 3]);
                    continue;
                }
                GameObject copy = UnityEngine.Object.Instantiate(FTKHub.Instance.m_Dummies[5], FTKHub.Instance.m_Dummies[5].transform.parent);
                copy.name = "Enemy " + (i + 1) + " Dummy";
                copy.GetComponent<PhotonView>().viewID = 3045 + i;
                dummies.Add(copy);
            }
            
            FTKHub.Instance.m_Dummies = dummies.ToArray();
            GameObject[] dummies2 = FTKHub.Instance.m_Dummies;
            Log("Dummies created");
        }

        public static GameObject CreateDummy(GameObject[] source, int index, string prefix) {
            GameObject dummy;
            if (index < 3) {
                dummy = source[index];
            } else {
                dummy = Object.Instantiate(source[2], source[2].transform.parent);
                dummy.name = $"{prefix} {index + 1} Dummy";
                dummy.GetComponent<PhotonView>().viewID = 3245 + index;
            }
            return dummy;
        }
    }
}
