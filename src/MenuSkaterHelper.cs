using UnityEngine;

namespace SkaterMod
{
    public class MenuSkaterHelper : MonoBehaviour
    {
        [SerializeField]
        private Vector3 positionOffset = new Vector3(0f, 0f, 0.15f); 
        [SerializeField]
        private Vector3 rotationOffset = new Vector3(0f, 20f, 0f); 

        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;

        void Awake()
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
        }

        void Start()
        {
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetFloat("fSpeed", 0f);
                animator.SetFloat("fTurn", 0f);
                animator.SetBool("bIsSprinting", false);
                animator.SetBool("bIsGrounded", true);
                animator.SetBool("bIsSliding", false);
                animator.SetBool("bIsStopping", false);
                
                ModLogger.Log("Menu skater animator set to idle state.");
            }
            else
            {
                ModLogger.Log("MenuSkaterHelper could not find an Animator component.");
            }
        }

        void Update()
        {
            transform.localPosition = initialLocalPosition + positionOffset;
            
            transform.localRotation = initialLocalRotation * Quaternion.Euler(rotationOffset);
        }
    }
}