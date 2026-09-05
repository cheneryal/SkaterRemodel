using UnityEngine;
using Unity.Netcode;

namespace SkaterMod
{
    public class SkaterAnimationBridge : MonoBehaviour
    {
        private Animator customAnimator;
        private Player player;
        private PlayerBody playerBody;
        private Movement playerMovement;
        private Rigidbody rb;
        private Vector3 lastPosition;
        private float smoothedSpeed = 0f;

        // We now accept PlayerBody directly to guarantee it is never null!
        public void Initialize(Player player, PlayerBody body, Movement playerMovement)
        {
            this.customAnimator = GetComponent<Animator>();
            this.player = player;
            this.playerBody = body; 
            this.playerMovement = playerMovement;
            this.rb = body.GetComponent<Rigidbody>();
            this.lastPosition = body.transform.position;

            if (customAnimator == null) ModLogger.Log("ERROR: [AnimBridge] Missing its Animator component.");
            if (player == null || playerBody == null || playerMovement == null)
            {
                ModLogger.Log($"ERROR: [AnimBridge] Initialize received null components. P: {player != null}, PB: {playerBody != null}, PM: {playerMovement != null}");
            }
            else ModLogger.Log($"SkaterAnimationBridge Initialized for {player.Username.Value}.");
        }

        void Update()
        {
            if (customAnimator == null || player == null || playerBody == null) return;

            float targetSpeed = 0f;
            if (player.IsLocalPlayer)
            {
                targetSpeed = playerBody.Speed.Value;
            }
            else
            {
                // Workaround for remote players where Speed.Value is not available.
                // Calculate speed based on position change.
                if (Time.deltaTime > 0f)
                {
                    Vector3 currentPosHorizontal = new Vector3(playerBody.transform.position.x, 0, playerBody.transform.position.z);
                    Vector3 lastPosHorizontal = new Vector3(lastPosition.x, 0, lastPosition.z);
                    float distance = Vector3.Distance(currentPosHorizontal, lastPosHorizontal);
                    targetSpeed = distance / Time.deltaTime;
                }
                lastPosition = playerBody.transform.position;
            }

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, targetSpeed, Time.deltaTime * 10f);

            customAnimator.SetFloat("fSpeed", smoothedSpeed);
            
            bool isSprinting = playerBody.IsSprinting.Value;
            bool isSliding = playerBody.IsSliding.Value; 
            bool isStopping = playerBody.IsStopping.Value;

            bool isJumping = playerBody.IsJumping;
            bool isSlipping = playerBody.IsSlipping;
            bool hasFallen = playerBody.HasFallen.Value;
            bool isGrounded = playerBody.IsGrounded;

            float currentTurn = 0f;
            if (player.IsLocalPlayer)
            {
                currentTurn = playerMovement.TurnSpeed * (playerMovement.IsTurningLeft ? -1f : 1f);
            }

            bool isGoalie = player.Role == PlayerRole.Goalie;
            bool useEffectiveButterfly = false;

            if (isGoalie && isSliding)
            {
                useEffectiveButterfly = true;
                isSliding = false; 
            }

            customAnimator.SetFloat("fTurn", currentTurn);
            customAnimator.SetBool("bIsSprinting", isSprinting);
            customAnimator.SetBool("bIsSliding", isSliding);
            customAnimator.SetBool("bIsStopping", isStopping);
            customAnimator.SetBool("bIsButterfly", useEffectiveButterfly);
            customAnimator.SetBool("bIsJumping", isJumping);
            customAnimator.SetBool("bIsSlipping", isSlipping);
            customAnimator.SetBool("bHasFallen", hasFallen);
            customAnimator.SetBool("bIsGrounded", isGrounded);
        }
    }
}