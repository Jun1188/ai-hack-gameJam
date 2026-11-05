using UnityEngine;

namespace GrandpaRobot.Presentation
{
    public sealed class GrandfatherLocomotion : MonoBehaviour
    {
        public Animator animator;
        public RuntimeAnimatorController controller;
        bool walking;
        float lastMoved=-10;
        public float referenceWalkSpeed=1.35f;
        void Awake()
        {
            if(!animator)animator=GetComponentInChildren<Animator>();
            if(!animator)return;
            if(controller)animator.runtimeAnimatorController=controller;
            animator.enabled=true;animator.speed=1;animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())renderer.updateWhenOffscreen=true;
            if(!animator.runtimeAnimatorController||!animator.avatar||!animator.avatar.isValid)
            {Debug.LogError("[Grandfather] Animator의 컨트롤러 또는 아바타 연결을 확인하세요.",this);return;}
            animator.Rebind();animator.SetLayerWeight(0,1);
            animator.Play("Base Layer.Idle",0,0);animator.Update(0);
            walking=false;
        }
        public void SetSpeed(float metresPerSecond)
        {
            if(!animator)return;
            if(metresPerSecond>.08f)lastMoved=Time.time;
            SetMoving(metresPerSecond>.08f||Time.time-lastMoved<.12f);
            animator.SetFloat("WalkRate",Mathf.Clamp(metresPerSecond/referenceWalkSpeed,.65f,1.4f),.12f,Time.deltaTime);
        }
        public void SetMoving(bool moving)
        {
            if(!animator||!animator.runtimeAnimatorController||walking==moving)return;
            animator.ResetTrigger(moving?"Idle":"Walk");
            animator.SetTrigger(moving?"Walk":"Idle");walking=moving;
        }
    }
}
