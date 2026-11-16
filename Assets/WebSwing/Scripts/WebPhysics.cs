using UnityEngine;

namespace WebSwing
{
    // Shared, deterministic rope calculation used by the player and editor checks.
    public static class WebPhysics
    {
        public static Vector3 TetherForce(Vector3 attachment, Vector3 velocity, Vector3 hand,
            Vector3 handVelocity, float length, float spring, float damping, float maxForce)
        {
            Vector3 delta = attachment - hand;
            float distance = delta.magnitude;
            if (distance <= length || distance < .001f) return Vector3.zero;
            Vector3 outward = delta / distance;
            float separatingSpeed = Vector3.Dot(velocity - handVelocity, outward);
            float tension = Mathf.Clamp((distance - length) * spring + separatingSpeed * damping, 0, maxForce);
            return -outward * tension;
        }
        public static Vector3 Constrain(Vector3 position, Vector3 anchor, float length, ref Vector3 velocity, float reelSpeed = 0f)
        {
            Vector3 offset = position - anchor;
            float distance = offset.magnitude;
            if (distance < .0001f || distance < length) return position;
            Vector3 outward = offset / distance;
            float radialSpeed = Vector3.Dot(velocity, outward);
            // A shortening taut rope needs inward radial velocity as well as a position constraint.
            // Its tangential velocity survives so gravity produces a pendulum arc.
            if (radialSpeed > -reelSpeed) velocity -= outward * (radialSpeed + reelSpeed);
            return anchor + outward * length;
        }

        public static Vector3 BuildingLaunchVelocity(Vector3 delta,Vector3 current,float strength,float gravity)
        {
            float rise=Mathf.Clamp(delta.y+12f,22f,85f)*Mathf.Lerp(.95f,1.08f,Mathf.Clamp01(strength));
            float vertical=Mathf.Sqrt(2*Mathf.Max(1,gravity)*rise),apexTime=vertical/Mathf.Max(1,gravity);
            Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            Vector3 planar=flat.sqrMagnitude>.01f?flat.normalized*Mathf.Clamp(flat.magnitude/apexTime,4,28):Vector3.zero;
            planar+=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(current,Vector3.up),8)*.15f;
            return Vector3.ClampMagnitude(planar+Vector3.up*vertical,52);
        }
        public static float ReelDistanceFactor(float distance)
        {
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(12f,60f,distance));
        }
        public static Vector3 ConstrainPendulum(Vector3 position,Vector3 anchor,float length,ref Vector3 velocity,float reelSpeed=0)
        {
            Vector3 radial=position-anchor;
            if(radial.sqrMagnitude<.000001f)radial=Vector3.down;
            radial.Normalize();
            // A swing keeps a taut radius; remove inward as well as outward drift.
            velocity=Vector3.ProjectOnPlane(velocity,radial)-radial*reelSpeed;
            return anchor+radial*length;
        }
        public static Vector3 PullAcceleration(Vector3 position, Vector3 velocity, Vector3 target,
            Vector3 targetVelocity, float spring, float damping, float maximum)
        {
            return Vector3.ClampMagnitude((target - position) * spring +
                (targetVelocity - velocity) * damping, maximum);
        }
    }
}
