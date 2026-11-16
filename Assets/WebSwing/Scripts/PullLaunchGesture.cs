using UnityEngine;
namespace WebSwing
{
    public sealed class PullLaunchGesture
    {
        Vector3 previous;bool sampled;float distance,window;
        public void Reset(){sampled=false;distance=window=0;}
        public bool Step(Vector3 hand,Vector3 anchor,bool gripping,float dt,out float strength)
        {
            strength=0;
            if(!gripping||dt<=0||dt>.12f){Reset();return false;}
            if(!sampled){previous=hand;sampled=true;return false;}
            Vector3 movement=previous-hand;previous=hand;
            if(movement.magnitude>.12f){distance=window=0;return false;}
            float pull=Vector3.Dot(movement,(anchor-hand).normalized),speed=pull/dt;
            window+=dt;if(window>.4f){distance=0;window=dt;}
            if(speed>.65f)distance+=pull;else distance=Mathf.Max(0,distance-dt*.6f);
            if(distance<.18f)return false;
            strength=Mathf.InverseLerp(.65f,2.2f,speed);Reset();return true;
        }
    }
}
