using UnityEngine;
using System.Collections;

namespace SkaterMod
{
    public class LocalPlayerVisibility : MonoBehaviour
    {
        public void InitializeAndApply()
        {
            StartCoroutine(DelayedApplyVisibility());
        }

        private IEnumerator DelayedApplyVisibility()
        {
            yield return new WaitForSeconds(0.1f);

            ModLogger.Log("[LocalPlayerVisibility] Enforcing visibility settings after delay.");

            Renderer leftArmRenderer = FindChildRecursive(transform, "Left_Arm")?.GetComponent<Renderer>();
            Renderer rightArmRenderer = FindChildRecursive(transform, "Right_Arm")?.GetComponent<Renderer>();
            Renderer leftGloveRenderer = FindChildRecursive(transform, "Left_Glove")?.GetComponent<Renderer>();
            Renderer rightGloveRenderer = FindChildRecursive(transform, "Right_Glove")?.GetComponent<Renderer>();
            Renderer torsoRenderer = FindChildRecursive(transform, "Torso")?.GetComponent<Renderer>();
            Renderer leftLegRenderer = FindChildRecursive(transform, "Left_Leg")?.GetComponent<Renderer>();
            Renderer rightLegRenderer = FindChildRecursive(transform, "Right_Leg")?.GetComponent<Renderer>();
            Renderer groinRenderer = FindChildRecursive(transform, "Groin")?.GetComponent<Renderer>();
            Renderer leftSkateRenderer = FindChildRecursive(transform, "Left_Skate")?.GetComponent<Renderer>();
            Renderer rightSkateRenderer = FindChildRecursive(transform, "Right_Skate")?.GetComponent<Renderer>();
            Renderer leftBladeRenderer = FindChildRecursive(transform, "Left_Skate_Blade")?.GetComponent<Renderer>();
            Renderer rightBladeRenderer = FindChildRecursive(transform, "Right_Skate_Blade")?.GetComponent<Renderer>();

            // --- VISIBILITY TOGGLES ---

            if (!SkaterModPlugin.Config.ShowFirstPersonArms)
            {
                if (leftArmRenderer != null) leftArmRenderer.enabled = false;
                if (rightArmRenderer != null) rightArmRenderer.enabled = false;
            }
            else
            {
                // FIX: Enable updateWhenOffscreen to prevent flickering
                PreventCulling(leftArmRenderer);
                PreventCulling(rightArmRenderer);
            }

            if (!SkaterModPlugin.Config.ShowFirstPersonGloves)
            {
                if (leftGloveRenderer != null) leftGloveRenderer.enabled = false;
                if (rightGloveRenderer != null) rightGloveRenderer.enabled = false;
            }
            else
            {
                PreventCulling(leftGloveRenderer);
                PreventCulling(rightGloveRenderer);
            }

            if (!SkaterModPlugin.Config.ShowFirstPersonTorso)
            {
                if (torsoRenderer != null) torsoRenderer.enabled = false;
            }
            else
            {
                PreventCulling(torsoRenderer);
            }

            if (!SkaterModPlugin.Config.ShowFirstPersonLegs)
            {
                if (leftLegRenderer != null) leftLegRenderer.enabled = false;
                if (rightLegRenderer != null) rightLegRenderer.enabled = false;
                if (leftSkateRenderer != null) leftSkateRenderer.enabled = false;
                if (groinRenderer != null) groinRenderer.enabled = false;
                if (rightSkateRenderer != null) rightSkateRenderer.enabled = false;
                if (leftBladeRenderer != null) leftBladeRenderer.enabled = false;
                if (rightBladeRenderer != null) rightBladeRenderer.enabled = false;
            }
            else
            {
                PreventCulling(leftLegRenderer);
                PreventCulling(rightLegRenderer);
                PreventCulling(leftSkateRenderer);
                PreventCulling(groinRenderer);
                PreventCulling(rightSkateRenderer);
                PreventCulling(leftBladeRenderer);
                PreventCulling(rightBladeRenderer);
            }
        }

        // Helper method to fix the flickering
        private void PreventCulling(Renderer r)
        {
            if (r != null && r is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = true;
            }
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}