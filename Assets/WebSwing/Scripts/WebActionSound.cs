using UnityEngine;
namespace WebSwing
{
    // Cached short effects, with no streaming or per-frame synthesis.
    public static class WebActionSound
    {
        static AudioClip shot,broken,chandelier;
        public static AudioClip ShotClip=>shot?shot:shot=Generate(0);
        public static AudioClip BreakClip=>broken?broken:broken=Generate(1);
        static AudioClip Generate(int kind)
        {
            const int rate=22050;
            float seconds=kind==0?.19f:kind==1?.72f:.36f;
            var data=new float[Mathf.CeilToInt(rate*seconds)];var random=new System.Random(731+kind);
            float previous=0,low=0,phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,u=t/seconds,noise=(float)random.NextDouble()*2-1;
                low=Mathf.Lerp(low,noise,.36f);float high=noise-low;
                float value;
                if(kind==0)
                {
                    float envelope=Mathf.Min(1,t/.008f)*Mathf.Exp(-u*6);
                    phase+=2*Mathf.PI*Mathf.Lerp(1700,300,u)/rate;
                    value=(high*.8f+Mathf.Sin(phase)*.16f+low*.16f)*envelope;
                }
                else
                {
                    value=high*Mathf.Exp(-t*38)*.38f;
                    for(int shard=0;shard<8;shard++)
                    {
                        float age=t-shard*.023f;
                        if(age<0)continue;
                        float frequency=(kind==1?1700:700)+shard*377;
                        value+=Mathf.Sin(2*Mathf.PI*frequency*age)*Mathf.Exp(-age*(13+shard*2))*.075f;
                    }
                    value+=(noise-previous)*Mathf.Exp(-t*9)*.08f;
                }
                previous=noise;data[i]=Mathf.Clamp(value,-.85f,.85f);
            }
            var clip=AudioClip.Create(kind==0?"Web shoot / shh-thwip":kind==1?"Ceramic break / bright shards":"Brass and crystal impact",data.Length,1,rate,false);
            clip.SetData(data,0);return clip;
        }
        static void At(AudioClip clip,Vector3 position,float volume)
        {
            var go=new GameObject("Web action sound");go.transform.position=position;
            var source=go.AddComponent<AudioSource>();source.clip=clip;source.spatialBlend=.85f;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=.8f;source.maxDistance=18;
            source.volume=volume;source.dopplerLevel=0;source.Play();Object.Destroy(go,clip.length+.1f);
        }
        public static void Shoot(Vector3 position)=>At(ShotClip,position,.55f);
        public static void BreakCeramic(Vector3 position)=>At(BreakClip,position,.7f);
        public static void ChandelierHit(Vector3 position)
        {
            if(!chandelier)chandelier=Generate(2);
            At(chandelier,position,.5f);
        }
    }
}
