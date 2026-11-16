using UnityEngine;
namespace WebSwing
{
    public static class HandAim
    {
        // Finger curl is a trigger. Only the wrist and knuckles define the launch axis.
        public static bool TryForwardRay(Vector3 wrist,Vector3 indexBase,Vector3 littleBase,
            Vector3 origin,Quaternion originRotation,out Ray ray)
        {
            ray=default;Vector3 direction=(indexBase+littleBase)*.5f-wrist;
            if(direction.sqrMagnitude<.0009f)return false;
            direction=originRotation*direction.normalized;
            ray=new Ray(origin+originRotation*wrist+direction*.012f,direction);return true;
        }
        public static Quaternion RayRotation(Vector3 direction)
        {
            Vector3 up=Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.95f?Vector3.forward:Vector3.up;
            return Quaternion.LookRotation(direction,up);
        }
        public static float ReticleSize(float distance)=>Mathf.Clamp(distance*.024f,.18f,1.25f);
        public static Color ColorForHand(int hand)=>hand==0?new Color(.15f,.8f,1f):new Color(1f,.48f,.12f);
    }
}
