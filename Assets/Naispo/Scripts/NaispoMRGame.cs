using System.Collections.Generic;
using UnityEngine;
namespace WebSwing
{
    // A throw scores only once, and only while the one-minute MR round is running.
    public sealed class NaispoMRGame : MonoBehaviour
    {
        const string BestKey="Naispo.MR.Best";
        NaispoSession session;TextMesh hud;GameObject root;
        readonly Dictionary<Rigidbody,float> thrown=new Dictionary<Rigidbody,float>();
        public int Score { get; private set; }
        public int Best { get; private set; }
        public int Throws { get; private set; }
        public void Initialize(NaispoSession owner)
        {
            session=owner;Best=PlayerPrefs.GetInt(BestKey,0);
            root=new GameObject("MR round score and timer");root.transform.SetParent(owner.world.input.head,false);
            root.transform.localPosition=new Vector3(0,.38f,1.4f);
            hud=root.AddComponent<TextMesh>();hud.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.fontSize=56;hud.characterSize=.009f;hud.anchor=TextAnchor.MiddleCenter;hud.alignment=TextAlignment.Center;
            hud.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(hud.font);root.SetActive(false);
        }
        public void ResetRound(){Score=Throws=0;thrown.Clear();}
        public void MarkThrown(Rigidbody body)
        {
            if(!body||!session.MixedReality||!session.AcceptHands)return;
            thrown[body]=Time.time;Throws++;
        }
        public bool TargetHit(Rigidbody body,Vector3 velocity)
        {
            if(!body||!session.MixedReality||!session.AcceptHands||velocity.magnitude<1.2f||
                !thrown.TryGetValue(body,out float launched)||Time.time-launched>7)return false;
            thrown.Remove(body);Score++;Best=Mathf.Max(Best,Score);
            ExperienceCue.Play(body.position,1350,.08f);return true;
        }
        public void SaveBest(){PlayerPrefs.SetInt(BestKey,Best);PlayerPrefs.Save();}
        void LateUpdate()
        {
            bool show=session.Phase==NaispoPhase.Running||session.Phase==NaispoPhase.Paused;
            int seconds=Mathf.CeilToInt(session.Remaining);
            root.SetActive(show);if(show)hud.text=session.MixedReality?"SCORE "+Score+"   /   "+seconds+"s\nBEST "+Best:
                "TIME "+(seconds/60)+":"+(seconds%60).ToString("00");
        }
        void OnDestroy(){if(root)Destroy(root);}
    }
}
