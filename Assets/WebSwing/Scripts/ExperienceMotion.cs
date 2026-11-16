using System.Collections.Generic;
using UnityEngine;

namespace WebSwing
{
    public static class ExperienceMotion
    {
        public static bool BallisticVelocity(Vector3 origin,Vector3 target,float speed,out Vector3 velocity)
        {
            Vector3 delta=target-origin,flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            float x=flat.magnitude,g=Physics.gravity.magnitude,s2=speed*speed;
            float discriminant=s2*s2-g*(g*x*x+2*delta.y*s2);
            if(x<.05f||discriminant<0||g<.01f){velocity=Vector3.zero;return false;}
            float angle=Mathf.Atan((s2-Mathf.Sqrt(discriminant))/(g*x));
            velocity=flat.normalized*(Mathf.Cos(angle)*speed)+Vector3.up*(Mathf.Sin(angle)*speed);return true;
        }
    }
    public static class ExperienceCue
    {
        static readonly Dictionary<int,AudioClip> clips=new Dictionary<int,AudioClip>();
        public static void Play(Vector3 position,int pitch,float duration)
        {
            if(!clips.TryGetValue(pitch,out var clip)||!clip)
            {
                int count=Mathf.CeilToInt(22050*duration);var samples=new float[count];
                for(int i=0;i<count;i++)samples[i]=Mathf.Sin(i*2*Mathf.PI*pitch/22050f)*Mathf.Sin(i*Mathf.PI/count)*.25f;
                clip=AudioClip.Create("Web feedback "+pitch,count,1,22050,false);clip.SetData(samples,0);clips[pitch]=clip;
            }
            AudioSource.PlayClipAtPoint(clip,position,.25f);
        }
    }
}
