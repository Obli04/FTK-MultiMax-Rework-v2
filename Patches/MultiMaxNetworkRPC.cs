using UnityEngine;
using Photon;
using System;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections;

public class MultiMaxNetworkRPC : Photon.MonoBehaviour
{
    protected override void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[MultiMax] RPC handler Awake, viewID={photonView?.viewID ?? 0}");
    }

    void OnDestroy()
    {
        Debug.LogWarning($"[MultiMax] RPC handler destroyed! viewID={photonView?.viewID ?? 0}");
    }
    [PunRPC]
    public void PingTest()
    {
        Debug.Log($"[MultiMax] ✅ PingTest received! Handler is working, viewID={photonView.viewID}");
    }

    [PunRPC]
    public void SyncTurnIndex(int index)
    {
        try
        {
            Debug.Log($"[MultiMax] ✅ RPC SyncTurnIndex received: {index}");

            var mc = EncounterSessionMC.Instance;
            if (mc == null)
            {
                Debug.LogError("[MultiMax] EncounterSessionMC is null");
                return;
            }

            var field = typeof(EncounterSessionMC).GetField("m_CurrentCombatantIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(mc, index);

            Debug.Log($"[MultiMax] ✅ Set m_CurrentCombatantIndex → {index}");

            var uiTimeline = GameObject.FindObjectOfType<uiActiveTime>();
            if (uiTimeline != null)
            {
                uiTimeline.SendMessage("UpdateTimeline", SendMessageOptions.DontRequireReceiver);
                Debug.Log("[MultiMax] ✅ Updated timeline UI");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiMax] SyncTurnIndex error: {e}");
        }
    }

    [PunRPC]
    public void SyncFightOrder(string json)
    {
        try
        {
            Debug.Log($"[MultiMax] ✅ RPC SyncFightOrder received");

            var mc = EncounterSessionMC.Instance;
            var enc = EncounterSession.Instance;
            if (mc == null || enc == null)
            {
                Debug.LogError("[MultiMax] MC or Enc is null");
                return;
            }

            var ids = JsonConvert.DeserializeObject<List<FTKPlayerID>>(json);
            if (ids == null || ids.Count == 0) return;

            var fightOrderField = typeof(EncounterSessionMC).GetField("m_FightOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            var listType = typeof(List<>).MakeGenericType(typeof(EncounterSessionMC.FightOrderEntry));
            var newList = (IList)Activator.CreateInstance(listType);
            var ctor = typeof(EncounterSessionMC.FightOrderEntry).GetConstructor(new[] { typeof(FTKPlayerID), typeof(int) });
            for (int i = 0; i < ids.Count; i++)
                newList.Add(ctor.Invoke(new object[] { ids[i], i }));
            fightOrderField?.SetValue(mc, newList);
            var currentCombatantField = typeof(EncounterSessionMC)
                .GetField("m_CurrentCombatantIndex", BindingFlags.Instance | BindingFlags.NonPublic);

            if (currentCombatantField != null)
            {
                int firstAliveIndex = 0;
                for (int i = 0; i < ids.Count; i++)
                {
                    var dummy = enc.GetDummyByFID(ids[i]);
                    if (dummy != null && dummy.m_IsAlive)
                    {
                        firstAliveIndex = i;
                        break;
                    }
                }

                currentCombatantField.SetValue(mc, firstAliveIndex);
                Debug.Log($"[MultiMax] ✅ Synced m_CurrentCombatantIndex = {firstAliveIndex}");
            }

            var uiTimeline = GameObject.FindObjectOfType<uiActiveTime>();
            uiTimeline?.SendMessage("UpdateTimeline", SendMessageOptions.DontRequireReceiver);

            Debug.Log($"[MultiMax] ✅ Synced fight order ({ids.Count})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiMax] SyncFightOrder error: {e}");
        }
    }

    [PunRPC]
    public void SyncVictim(FTKPlayerID attacker, FTKPlayerID victim)
    {
        try
        {
            var enc = EncounterSession.Instance;
            if (enc?.m_EnemyDummies == null) return;

            if (enc.m_EnemyDummies.TryGetValue(attacker, out var dummy))
            {
                dummy.m_CurrentVictimID = victim;
                Debug.Log($"[MultiMax] ✅ Applied victim sync: {dummy.name} → {victim.m_TurnIndex}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiMax] SyncVictim error: {e}");
        }
    }
}