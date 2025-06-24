using UnityEngine;
using static FTK_MultiMax_Rework_v2.Main;

namespace FTK_MultiMax_Rework_v2;

public static class TargetPositions
{
    public static void Fix(Transform root)
    {
        int maxPlayers = GameFlowMC.gMaxPlayers;
        
        // Player 2_target is leftmost, don't ask me why, I would like to know just as well 
        GameObject leftMost = root.Find("Player 2_target").gameObject;
        GameObject rightMost = root.Find("Player 3_target").gameObject;

        Vector3 leftMostPosition = leftMost.transform.localPosition;
        Vector3 rightMostPosition = rightMost.transform.localPosition;

        Vector3 oldDelta = rightMostPosition - leftMostPosition;
        Vector3 delta = oldDelta * maxPlayers / 3f; // Adjust for player amount

        Vector3 recenterOffset = (delta - oldDelta) * -0.5f;

        int startIndex = leftMost.transform.GetSiblingIndex();
                
        for (int i = 0; i < maxPlayers; i++)
        {
            Transform targetTransform = root.Find($"Player {i + 1}_target");
                    
            if (!targetTransform)
            {
                GameObject newTarget = Object.Instantiate(rightMost, root);
                newTarget.name = $"Player {i + 1}_target";
                targetTransform = newTarget.transform;
                targetTransform.SetSiblingIndex(startIndex + i);
            }
            else if (i == 0)
            {
                // For some unholy reason, the game swaps Player 1 and Player 2's positions
                targetTransform.SetSiblingIndex(startIndex + 1);
            }

            float alpha = (float) i / (maxPlayers - 1); // 0-1 scale from left to right, from player 1 to last player
            targetTransform.localPosition = leftMostPosition + delta * alpha + recenterOffset;
        }
    }
}