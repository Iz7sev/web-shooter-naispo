using UnityEngine;
namespace WebSwing
{
    public sealed class RoomRopeGrab : MonoBehaviour
    {
        readonly WebShooter[] sources=new WebShooter[2],held=new WebShooter[2];
        readonly bool[] picked=new bool[2];
        readonly RoomLooseWeb[] recent=new RoomLooseWeb[2];
        readonly float[] recentUntil=new float[2];
        public void RememberShot(int hand,RoomLooseWeb patch){recent[hand]=patch;recentUntil[hand]=Time.unscaledTime+4f;}
        readonly bool[] tracked=new bool[2],closed=new bool[2],wasClosed=new bool[2];
        readonly Vector3[] palms=new Vector3[2],previous=new Vector3[2];
        readonly GameObject[] markers=new GameObject[2];
        Material material;
        SwingMotor motor;
        public bool IsHolding(int hand)=>held[hand]!=null||picked[hand];
        public void Initialize(WebShooter left,WebShooter right,SwingMotor body)
        {
            sources[0]=left;sources[1]=right;motor=body;
            material=new Material(Shader.Find("Sprites/Default")){color=new Color(.25f,1,.65f)};
            for(int i=0;i<2;i++)
            {
                markers[i]=GameObject.CreatePrimitive(PrimitiveType.Sphere);markers[i].name="Rope held by hand";
                markers[i].transform.SetParent(transform,false);markers[i].transform.localScale=Vector3.one*.055f;
                markers[i].GetComponent<Collider>().enabled=false;Destroy(markers[i].GetComponent<Collider>());
                markers[i].GetComponent<Renderer>().sharedMaterial=material;markers[i].SetActive(false);
            }
        }
        public static bool NearSegment(Vector3 palm,Vector3 start,Vector3 end)
        {
            Vector3 d=end-start;if(d.sqrMagnitude<.09f)return false;
            float t=Vector3.Dot(palm-start,d)/d.sqrMagnitude;
            return t>.08f&&t<.95f&&Vector3.Distance(palm,start+d*t)<.13f&&Vector3.Distance(palm,start)>.12f;
        }
        public void SetHand(int index,bool valid,bool fist,Vector3 palm)
        {
            tracked[index]=valid;closed[index]=fist;palms[index]=palm;
            if(!valid){recent[index]=null;Drop(index);DropPickup(index);}
        }
        public void Step(float dt)
        {
            for(int i=0;i<2;i++)
            {
                var own=sources[i];
                if(picked[i])
                {
                    if(!own||!own.Active||own.Carrying)picked[i]=false;
                    else if(!tracked[i]||!closed[i]||!motor.MovementLocked)DropPickup(i);
                }
                var source=sources[1-i];bool available=motor&&motor.MovementLocked&&tracked[i]&&closed[i]&&sources[i]&&!sources[i].Active;
                if(held[i]&&(!available||!held[i].CanGrabRope))Drop(i);
                if(!held[i]&&!picked[i]&&available&&!wasClosed[i])
                {
                    // The first fist after firing keeps that shot, even if curling shifts aim.
                    var loose=Time.unscaledTime<=recentUntil[i]&&recent[i]&&recent[i].isActiveAndEnabled?recent[i]:null;
                    recent[i]=null;
                    if(!loose)loose=RoomLooseWeb.Closest(palms[i]);
                    if(!loose&&own.AimTracked)loose=RoomLooseWeb.Aimed(own.AimRay);
                    if(loose&&loose.PickUp(own,i)){picked[i]=true;available=false;ExperienceCue.Play(palms[i],1000,.045f);}
                }
                if(!held[i]&&!picked[i]&&available&&!wasClosed[i]&&source&&source.CanGrabRope&&NearSegment(palms[i],source.hand.position,source.RopeEnd))
                {held[i]=source;previous[i]=palms[i];ExperienceCue.Play(palms[i],1000,.045f);}
                if(held[i])
                {
                    Vector3 step=palms[i]-previous[i];
                    if(step.magnitude>.22f){Drop(i);}
                    else
                    {
                        float pull=Mathf.Max(0,Vector3.Dot(-step,(held[i].RopeEnd-palms[i]).normalized));
                        held[i].HoldRope(palms[i],Mathf.Min(pull,Mathf.Max(dt,0)*2));previous[i]=palms[i];
                    }
                }
                wasClosed[i]=tracked[i]&&closed[i];
                if(markers[i]){markers[i].SetActive(held[i]||picked[i]);markers[i].transform.position=palms[i];}
            }
        }
        void Drop(int i){if(held[i])held[i].ClearRopeHold();held[i]=null;if(markers[i])markers[i].SetActive(false);}
        void DropPickup(int i)
        {
            if(picked[i]&&sources[i]&&sources[i].Active&&!sources[i].Carrying)sources[i].LeaveLooseWeb();
            picked[i]=false;
        }
        public void ResetGrabs(){for(int i=0;i<2;i++){Drop(i);DropPickup(i);recent[i]=null;tracked[i]=closed[i]=wasClosed[i]=false;}}
        void OnDisable()=>ResetGrabs();
        void OnDestroy(){if(material)Destroy(material);}
    }
}
