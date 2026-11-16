using UnityEngine;

namespace WebSwing
{
    // Each hand owns its own short history. Untracked motion never enters it.
    public sealed class ThrowMotionMemory
    {
        public Vector3 Velocity { get; private set; }
        float age=1;
        public void Clear() { Velocity=Vector3.zero;age=1; }
        public void Sample(Vector3 velocity,Vector3 forward,bool carrying,float dt)
        {
            if(!carrying) { Clear();return; }
            age+=dt;if(age>.25f)Velocity=Vector3.zero;
            // Keep a recent strong push, rather than the slowdown when the fingers open.
            if(InteractionMotion.ThrowGesture(velocity,forward) && (Velocity==Vector3.zero || velocity.sqrMagnitude>=Velocity.sqrMagnitude))
            { Velocity=velocity;age=0; }
        }
    }
    // Each hand must hold the menu pose continuously. Switching hands never adds time.
    public sealed class MenuPoseHold
    {
        float leftHeld,rightHeld;
        bool consumed;
        public void ResetProgress(){leftHeld=rightHeld=0;}
        public bool Step(bool left,bool right,float dt)
        {
            if(!left&&!right){consumed=false;ResetProgress();return false;}
            if(consumed)return false;
            dt=Mathf.Clamp(dt,0,.1f);
            leftHeld=left?leftHeld+dt:0;rightHeld=right?rightHeld+dt:0;
            if(Mathf.Max(leftHeld,rightHeld)<1.5f)return false;
            consumed=true;ResetProgress();return true;
        }
    }
    public static class InteractionMotion
    {
        public const float MaxWalkSpeed=6.2f;
        public static bool CanCarry(Vector3 handToCargo,float ropeLength)
        {
            return handToCargo.magnitude<1.05f ||
                (Mathf.Abs(handToCargo.y)<2f && Vector3.ProjectOnPlane(handToCargo,Vector3.up).magnitude<.85f && ropeLength<1.4f);
        }
        public static bool ThrowGesture(Vector3 velocity,Vector3 forward)
        {
            return velocity.magnitude>.65f && Vector3.Dot(velocity,forward.normalized)>.25f;
        }
        public static Vector3 ThrowVelocity(Vector3 velocity)
        {
            // Modest amplification permits a chest-height push instead of an overhead throw.
            return Vector3.ClampMagnitude(velocity*1.8f,7f);
        }
        public static Vector3 WallDirection(Vector3 look,Vector3 normal)
        {
            Vector3 tangent=Vector3.ProjectOnPlane(look,normal);
            return tangent.sqrMagnitude<.015f?Vector3.up:tangent.normalized;
        }
        public static float ClimbTarget(Vector3 left,Vector3 right,Vector3 forward)
        {
            if(left.magnitude>4||right.magnitude>4)return 0;
            return Mathf.Min(3,Mathf.Max(WalkTarget(left,right,forward),WalkTarget(left,right,Vector3.up))*.65f);
        }
        public static bool WebPose(float index,float middle,float ring,float little)
        {
            // Relative curl keeps a relaxed open palm distinct from the web gesture.
            return index>.70f && little>.65f && middle<.83f && ring<.83f &&
                (index+little-middle-ring)>.16f;
        }
        public static bool MenuPeace(float index,float middle,float ring,float little)
        {
            return index>.88f && middle>.88f && ring<.65f && little<.65f;
        }
        public static float PullStep(Vector3 previous,Vector3 current,Vector3 towardObject,float dt)
        {
            float displacement=Vector3.Dot(previous-current,towardObject.normalized);
            return displacement>.0012f?Mathf.Min(displacement,3f*dt):0f;
        }
        public static float WalkTarget(Vector3 leftVelocity,Vector3 rightVelocity,Vector3 forward)
        {
            float left=Vector3.Dot(leftVelocity,forward),right=Vector3.Dot(rightVelocity,forward);
            if(left*right>=0 || Mathf.Min(Mathf.Abs(left),Mathf.Abs(right))<.08f) return 0;
            return Mathf.Clamp((Mathf.Abs(left)+Mathf.Abs(right)-.25f)*4.14f,0,MaxWalkSpeed);
        }
    }
}
