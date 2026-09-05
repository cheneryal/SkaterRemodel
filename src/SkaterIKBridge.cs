using UnityEngine;

namespace SkaterMod
{
    [RequireComponent(typeof(Animator))]
    public class SkaterIKBridge : MonoBehaviour
    {
        private Animator animator;
        private Player player;
        private PlayerBody playerBody;
        private Stick stickComponent;
        private Transform headBone;
        private Transform spineBone;
        private Quaternion baseHeadRotation = Quaternion.identity;
        private Quaternion baseSpineRotation = Quaternion.identity;
        private Quaternion lastHeadRotation = Quaternion.identity;
        private Quaternion lastSpineRotation = Quaternion.identity;
        private float headTrackingSmoothing = 12f;
        
        // Only allow yaw (turning), no pitch (up/down tilt)
        private const float MaxYaw = 90f;          // Max head turn left/right
        private const float SpineYawRatio = 0.6f;  // 60% of yaw goes to spine, 40% to head

        public void Initialize(Player player, PlayerBody body = null)
        {
            this.animator = GetComponent<Animator>();
            this.player = player;
            this.playerBody = body;
            
            // Find the head and spine bones for tracking
            if (animator != null)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
                
                // Cache initial rotations
                if (headBone != null)
                {
                    baseHeadRotation = headBone.localRotation;
                    lastHeadRotation = baseHeadRotation;
                }
                if (spineBone != null)
                {
                    baseSpineRotation = spineBone.localRotation;
                    lastSpineRotation = baseSpineRotation;
                }
            }
            
            ModLogger.Log($"SkaterIKBridge Initialized for {player?.Username.Value}.");
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || player == null)
            {
                if(animator != null) SetHandIKWeights(0);
                return;
            }

            try
            {
                // Apply hand IK for stick handling
                if (player.Stick != null)
                {
                    ApplyHandIK();
                }
            }
            catch (System.Exception e)
            {
                ModLogger.Log($"CRITICAL ERROR in OnAnimatorIK, disabling IK. Error: {e.Message}");
                SetHandIKWeights(0); 
                enabled = false; 
            }
        }

        void LateUpdate()
        {
            // Apply head rotation after animations have been evaluated
            if (animator != null && headBone != null)
            {
                ApplyHeadTracking();
            }
        }

        private void ApplyHandIK()
        {
            if (stickComponent == null)
            {
                stickComponent = player.Stick.GetComponent<Stick>();
                if (stickComponent == null)
                {
                    SetHandIKWeights(0);
                    return;
                }
            }

            Vector3 topHandOffset;
            float topHandTwist;
            float topHandAngle;
            Vector3 handClaspOffset;
            Vector3 claspRotationOffset;

            if (player.Handedness.Value == PlayerHandedness.Right)
            {
                topHandOffset = new Vector3(-0.1f, 0.05f, -0.22f);
                topHandTwist = 90f;
                topHandAngle = 10f;
                handClaspOffset = new Vector3(0.15f, 0.12f, 0.2f);
                claspRotationOffset = new Vector3(-20f, -5f, -160f);
            }
            else 
            {
                topHandOffset = new Vector3(0.1f, -0.05f, -0.22f);
                topHandTwist = -95f;
                topHandAngle = -20f;
                handClaspOffset = new Vector3(0.15f, 0.12f, 0.2f);
                claspRotationOffset = new Vector3(20f, 5f, 160f);
            }

            Vector3 stickTop = stickComponent.ShaftHandlePosition;
            Vector3 stickBottom = stickComponent.BladeHandlePosition;
            Vector3 stickForward = (stickBottom - stickTop).normalized;
            Vector3 stickUp = stickComponent.transform.up;
            Quaternion stickRotation = Quaternion.LookRotation(stickForward, stickUp);

            Vector3 finalTopHandPosition = stickTop + stickComponent.transform.TransformDirection(topHandOffset);
            Quaternion finalTopHandRotation = stickRotation * Quaternion.Euler(topHandAngle, 0, topHandTwist);

            Vector3 finalBottomHandPosition = finalTopHandPosition + (finalTopHandRotation * handClaspOffset);
            Quaternion finalBottomHandRotation = finalTopHandRotation * Quaternion.Euler(claspRotationOffset);

            SetHandIKWeights(1);
            if (player.Handedness.Value == PlayerHandedness.Right)
            {
                SetIK(AvatarIKGoal.LeftHand, finalTopHandPosition, finalTopHandRotation);
                SetIK(AvatarIKGoal.RightHand, finalBottomHandPosition, finalBottomHandRotation);
            }
            else 
            {
                SetIK(AvatarIKGoal.RightHand, finalTopHandPosition, finalTopHandRotation);
                SetIK(AvatarIKGoal.LeftHand, finalBottomHandPosition, finalBottomHandRotation);
            }
        }

        private void ApplyHeadTracking()
        {
            if (headBone == null || spineBone == null || player == null || player.PlayerInput == null)
                return;

            // Get the look angle from PlayerInput
            Vector2 lookAngle = player.IsLocalPlayer 
                ? player.PlayerInput.LookAngleInput.ClientValue 
                : player.PlayerInput.LookAngleInput.ServerValue;
            
            // Only use yaw (turning left/right), ignore pitch (up/down tilt)
            float yaw = Mathf.Clamp(lookAngle.y, -MaxYaw, MaxYaw);
            
            // Distribute yaw rotation between spine and head for natural body movement
            // Spine rotates more (60%), head rotates less (40%), so body follows look direction
            float spineYaw = yaw * SpineYawRatio;
            float headYaw = yaw * (1f - SpineYawRatio);
            
            // Apply spine rotation (body turns)
            Quaternion targetSpineRotation = Quaternion.Euler(0f, spineYaw, 0f);
            lastSpineRotation = Quaternion.Lerp(lastSpineRotation, targetSpineRotation, Time.deltaTime * headTrackingSmoothing);
            spineBone.localRotation = lastSpineRotation;
            
            // Apply head rotation (head turns on top of spine)
            Quaternion targetHeadRotation = Quaternion.Euler(0f, headYaw, 0f);
            lastHeadRotation = Quaternion.Lerp(lastHeadRotation, targetHeadRotation, Time.deltaTime * headTrackingSmoothing);
            headBone.localRotation = lastHeadRotation;
        }

        private void SetIK(AvatarIKGoal goal, Vector3 position, Quaternion rotation)
        {
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);
        }

        private void SetIKWeights(float weight)
        {
            SetHandIKWeights(weight);
        }

        private void SetHandIKWeights(float weight)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
        }
    }
}