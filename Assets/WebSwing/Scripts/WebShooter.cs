using UnityEngine;

namespace WebSwing
{
    public sealed class WebShooter : MonoBehaviour
    {
        public SwingMotor motor;
        public Transform hand;
        public int handIndex=1;
        public float range = 100f;
        public LayerMask targetLayers = 1 << 8;
        public float pullSpring = 85f, pullDamping = 20f, maxPullAcceleration = 28f;
        public float maxPullForce = 150f, pullGain = 2.2f;
        public float maxPullMass = 30f, holdDistance = .65f, breakDistance = 125f;
        public bool LiftAssist=true,EasyThrow=true;
        public bool ThrowArmed { get; set; }
        Vector3 easyDirection;
        TextMesh carryHint;
        public Vector3 PlannedThrow { get; private set; }
        public bool AimAssisted { get; private set; }
        RoomToy assistedTarget;
        float nextTargetSearch;
        readonly RaycastHit[] aimHits=new RaycastHit[32];
        public bool AimTracked=true;
        public Ray LastShotRay { get; private set; }
        public Ray AimRay=>new Ray(hand.position,hand.forward);
        public Color HandColor=>handIndex==0?new Color(.15f,.8f,1f):new Color(1f,.48f,.12f);
        LineRenderer aimGuide,aimRing;
        Material aimMaterial;
        public int ShotSequence { get; private set; }
        public bool Carrying => carrying;
        bool staticWeb,carrying;
        float easyPullDistance;bool easyPullStarted;
        bool ExhibitionGrab=>NaispoSession.Active&&NaispoSession.Active.MixedReality&&NaispoSession.Active.AcceptHands;
        Vector3 staticPoint;
        public bool Active => staticWeb || motor.IsAttached(handIndex) || pulled != null;
        public bool PullingObject => pulled!=null;
        public bool RoomSurfaceWeb=>staticWeb&&motor.MovementLocked;
        public bool CanGrabRope=>motor.MovementLocked&&Active&&!carrying;
        public Vector3 RopeEnd=>EndPoint;
        public bool RopeHeld { get; private set; }
        Vector3 ropeHoldPoint;
        float ropeHoldSeen;
        public void HoldRope(Vector3 point,float pull)
        {
            if(!CanGrabRope)return;
            RopeHeld=true;ropeHoldPoint=point;ropeHoldSeen=Time.unscaledTime;
            if(pulled)objectRopeLength=Mathf.Max(holdDistance,objectRopeLength-pull*pullGain);
        }
        public void ClearRopeHold(){RopeHeld=false;}
        Collider[] pulledColliders;
        bool articulated;
        public float ObjectRopeLength => objectRopeLength;
        public string State => pulled != null ? (carrying?"LIFT / CARRY: ":"PULL HAND: ") + pulled.mass.ToString("F0") + " kg" : motor.IsAttached(handIndex) ? "REEL / SWING" : "READY";
        public float Tension01 => pulled != null ? pullTension : motor.GetTension(handIndex);
        public Vector3 ForceDirection => Active ? (EndPoint - hand.position).normalized : Vector3.zero;
        public int Owner { get; private set; } = -1;
        Rigidbody pulled;
        Vector3 localHit, localNormal, staticNormal, previousHand, previousLocalHand;
        float objectRopeLength, pullTension;
        WebRopeVisual web;
        Material markerMaterial;
        GameObject marker;
        float firedAt;
        readonly PullLaunchGesture pullLaunch=new PullLaunchGesture();
        public Vector3 ThrowPreviewVelocity { get; set; }
        LineRenderer trajectory;
        LineRenderer carryOutline;
        Vector3 carryTarget,carryTargetSpeed;
        static readonly int[] outlineOrder={0,1,2,3,0,4,5,1,5,6,2,6,7,3,7,4};
        static readonly Vector3[] outlineCorners={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
        Material trajectoryMaterial;
        Vector3 EndPoint => pulled != null ? pulled.transform.TransformPoint(localHit) : staticWeb?staticPoint:motor.GetAnchor(handIndex);
        Vector3 HitNormal => pulled != null ? pulled.transform.TransformDirection(localNormal) : staticNormal;

        void Awake()
        {
            web = gameObject.AddComponent<WebRopeVisual>();
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Web target"; Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = Vector3.one * .075f;
            markerMaterial = new Material(Shader.Find("Sprites/Default"));
            marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            marker.SetActive(false);
            var hint=new GameObject("Carry state hint");hint.transform.SetParent(transform,false);
            carryHint=hint.AddComponent<TextMesh>();carryHint.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            carryHint.fontSize=48;carryHint.characterSize=.008f;carryHint.anchor=TextAnchor.MiddleCenter;
            hint.GetComponent<Renderer>().sharedMaterial=CityEnvironment.FontMaterial(carryHint.font);hint.SetActive(false);
            var preview=new GameObject("Throw trajectory preview");preview.transform.SetParent(transform,false);
            trajectory=preview.AddComponent<LineRenderer>();
            trajectoryMaterial=new Material(Shader.Find("Sprites/Default")){color=new Color(.2f,1f,.8f,.85f)};
            trajectory.sharedMaterial=trajectoryMaterial;trajectory.startWidth=.009f;trajectory.endWidth=.004f;trajectory.positionCount=0;trajectory.enabled=false;
            var outline=new GameObject("Held cargo outline");outline.transform.SetParent(transform,false);carryOutline=outline.AddComponent<LineRenderer>();
            aimMaterial=new Material(Shader.Find("Sprites/Default")){color=HandColor};
            aimGuide=NewAimLine("Wrist aim direction",2);aimRing=NewAimLine("Large hand reticle",33);
            carryOutline.sharedMaterial=trajectoryMaterial;carryOutline.startWidth=carryOutline.endWidth=.004f;carryOutline.positionCount=16;carryOutline.enabled=false;
        }
        LineRenderer NewAimLine(string name,int count)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=aimMaterial;line.positionCount=count;line.startWidth=line.endWidth=.003f;line.enabled=false;return line;
        }
        public bool Aim(out RaycastHit hit)
        {
            if (!Physics.Raycast(AimRay, out hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            bool ground = hit.rigidbody == null && hit.normal.y > .5f;
            return (ground || (motor && motor.MovementLocked && hit.collider.gameObject.layer==9) || ((1 << hit.collider.gameObject.layer) & targetLayers.value) != 0) &&
                (hit.rigidbody == null || hit.rigidbody.isKinematic || hit.rigidbody.mass <= maxPullMass);
        }
        public bool Fire(int owner,bool leaveMark=true)
        {
            if(!AimTracked)return false;
            if (!Aim(out RaycastHit hit)) return false;
            LastShotRay=AimRay;
            Rigidbody target = hit.rigidbody;
            if (target != null && (!target.isKinematic && target.mass > maxPullMass)) return false;
            Release(); Owner = owner; firedAt = Time.time; ShotSequence++;
            previousLocalHand=motor.transform.InverseTransformPoint(hand.position);
            WebActionSound.Shoot(hand.position);
            PracticeCourse.Attached(hit.point,hit.collider);
            if(motor.MovementLocked&&leaveMark)RoomWebMarks.Add(hit.point,hit.normal,target?target.transform:null);
            if (target != null && !target.isKinematic)
            {
                pulled = target; pulledColliders=target.GetComponentsInChildren<Collider>();articulated=target.GetComponent<Joint>()!=null; var reset=target.GetComponent<CargoReset>();if(reset)reset.CancelRecall(); localHit = target.transform.InverseTransformPoint(hit.point);
                localNormal = target.transform.InverseTransformDirection(hit.normal);
                objectRopeLength = Vector3.Distance(hand.position, hit.point);
                previousHand = hand.position; previousLocalHand=motor.transform.InverseTransformPoint(hand.position); target.WakeUp();
            }
            else
            {
                staticNormal = hit.normal;
                if(motor.MovementLocked || !motor.StaticWebMovementEnabled)
                { staticWeb=true; staticPoint=hit.point; }
                else motor.Attach(hit.point,handIndex);
            }
            return true;
        }
        public RoomLooseWeb LastDetachedWeb { get; private set; }
        public bool FireDetached(int owner)
        {
            if(!Fire(owner,false))return false;
            if(motor.MovementLocked)LeaveLooseWeb();return true;
        }
        public void LeaveLooseWeb()
        {
            if(CanGrabRope)LastDetachedWeb=RoomLooseWeb.Create(hand.position,pulled,EndPoint,HitNormal,HandColor);
            Release();
        }
        public void AdoptLooseWeb(Rigidbody body,Vector3 point,Vector3 normal,int owner)
        {
            Release();Owner=owner;firedAt=Time.time-.5f;
            previousHand=hand.position;previousLocalHand=motor.transform.InverseTransformPoint(hand.position);
            staticNormal=normal;
            if(body&&!body.isKinematic)
            {
                pulled=body;pulledColliders=body.GetComponentsInChildren<Collider>();articulated=body.GetComponent<Joint>()!=null;
                localHit=body.transform.InverseTransformPoint(point);localNormal=body.transform.InverseTransformDirection(normal);
                objectRopeLength=Vector3.Distance(hand.position,point);
                var reset=body.GetComponent<CargoReset>();if(reset)reset.CancelRecall();body.WakeUp();
            }
            else{staticWeb=true;staticPoint=point;}
        }
        public void Release(int owner = -1)
        {
            if (owner != -1 && Owner != owner) return;
            pullLaunch.Reset();
            easyPullDistance=0;easyPullStarted=false;
            ThrowArmed=false;PlannedThrow=Vector3.zero;ClearRopeHold();
            pulled = null; staticWeb=carrying=false; motor.ReleaseTether(handIndex); Owner = -1; pullTension = 0;
            if (web) web.Hide();
            ThrowPreviewVelocity=Vector3.zero;if(trajectory)trajectory.enabled=false;if(carryOutline)carryOutline.enabled=false;
        }
        public void ReleaseTrackingLost()
        {
            // Tracking loss is not a throw gesture. Drop carried cargo without adding stale velocity.
            if(carrying && pulled) { pulled.linearVelocity=Vector3.zero; pulled.angularVelocity=Vector3.zero; }
            Release();
        }
        public bool Throw(Vector3 trackedHandVelocity)
        {
            if(!carrying || !pulled) return false;
            var body=pulled;
            Vector3 velocity=InteractionMotion.ThrowVelocity(trackedHandVelocity);
            Launch(body,velocity); return true;
        }
        public bool ThrowPlanned(Vector3 velocity)
        {
            if(!carrying||!pulled)return false;
            Launch(pulled,Vector3.ClampMagnitude(velocity,7));return true;
        }
        void Launch(Rigidbody body,Vector3 velocity)
        {
            Release();body.linearVelocity=velocity;body.WakeUp();
            if(NaispoSession.Active&&NaispoSession.Active.MRGame)NaispoSession.Active.MRGame.MarkThrown(body);
            var reset=body.GetComponent<CargoReset>();if(reset)reset.ScheduleRecall(7);
            ExperienceCue.Play(body.position,550,.06f);
        }
        public void UpdateThrowAim(float dt)
        {
            if(!carrying||!pulled||!EasyThrow)return;
            Vector3 look=motor.head.forward;
            if(easyDirection.sqrMagnitude<.1f)easyDirection=look;
            easyDirection=Vector3.Slerp(easyDirection,look,1-Mathf.Exp(-dt*8));
            Vector3 origin=pulled.worldCenterOfMass;
            Vector3 target=origin+easyDirection*6;bool found=false;float nearest=12;
            int count=Physics.RaycastNonAlloc(motor.head.position,easyDirection,aimHits,12,~(1<<10),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(aimHits[i].rigidbody!=pulled && aimHits[i].distance<nearest)
            {nearest=aimHits[i].distance;target=aimHits[i].point;found=true;}
            AimAssisted=false;
            // Assist only a visible nearby virtual target within a narrow cone.
            if(!assistedTarget&&Time.time>=nextTargetSearch){assistedTarget=Object.FindFirstObjectByType<RoomToy>();nextTargetSearch=Time.time+.5f;}
            var toy=assistedTarget;
            if(toy && Vector3.Distance(origin,toy.transform.position)<8 &&
                Vector3.Angle(easyDirection,toy.transform.position-motor.head.position)<7)
            {
                Vector3 to=toy.transform.position-origin;bool blocked=false;
                int hits=Physics.RaycastNonAlloc(origin,to.normalized,aimHits,to.magnitude,~(1<<10),QueryTriggerInteraction.Ignore);
                for(int i=0;i<hits;i++)if(aimHits[i].rigidbody!=pulled && aimHits[i].rigidbody!=toy.GetComponent<Rigidbody>())blocked=true;
                if(!blocked){target=toy.transform.position;found=true;AimAssisted=true;}
            }
            PlannedThrow=found&&ExperienceMotion.BallisticVelocity(origin,target,5.5f,out var velocity)?velocity:easyDirection*5.5f;
            ThrowPreviewVelocity=PlannedThrow;
        }
        void FixedUpdate()
        {
            if(RopeHeld&&Time.unscaledTime-ropeHoldSeen>.15f)ClearRopeHold();
            if(staticWeb) { if(Vector3.Distance(hand.position,staticPoint)>breakDistance)Release(); return; }
            if (Owner >= 0 && pulled == null && !motor.IsAttached(handIndex)) { Release(); return; }
            if (!Active) return;
            if (Vector3.Distance(hand.position, EndPoint) > breakDistance) { Release(); return; }
            if (pulled == null)
            {
                if(motor.IsAttached(handIndex))
                {
                    Vector3 local=motor.transform.InverseTransformPoint(hand.position);
                    if(pullLaunch.Step(local,motor.transform.InverseTransformPoint(EndPoint),motor.IsGripping(handIndex)&&(!motor.ComfortEnabled||(NaispoSession.Active&&NaispoSession.Active.Guided&&NaispoSession.Active.AcceptHands)),Time.fixedDeltaTime,out float launchStrength)&&motor.LaunchFromPull(handIndex,launchStrength))
                    {Release();return;}
                    float step=InteractionMotion.PullStep(previousLocalHand,local,motor.transform.InverseTransformDirection(EndPoint-hand.position),Time.fixedDeltaTime);
                    previousLocalHand=local;motor.RequestPull(handIndex,step*1.6f);
                }
                return;
            }
            float dt = Time.fixedDeltaTime;
            Vector3 localHand=motor.transform.InverseTransformPoint(hand.position);
            Vector3 direction=motor.transform.InverseTransformDirection(EndPoint-hand.position);
            float pull=InteractionMotion.PullStep(previousLocalHand,localHand,direction,dt);
            if(LiftAssist) pull=Mathf.Max(pull,Mathf.Max(0,localHand.y-previousLocalHand.y)*.7f);
            previousLocalHand=localHand;
            objectRopeLength=Mathf.Max(holdDistance,objectRopeLength-pull*pullGain);
            if(ExhibitionGrab&&!articulated)
            {
                easyPullDistance+=pull;
                if(easyPullDistance>=.07f)easyPullStarted=true;
                if(easyPullStarted)objectRopeLength=Mathf.MoveTowards(objectRopeLength,holdDistance,dt*2.2f);
            }
            Vector3 handVelocity = Vector3.ClampMagnitude((hand.position - previousHand) / dt, 12f);
            previousHand = hand.position;
            float forceLimit = Mathf.Min(maxPullForce, pulled.mass * maxPullAcceleration);
            Vector3 point = EndPoint;
            Vector3 pullOrigin=RopeHeld?ropeHoldPoint:hand.position;
            float remaining=RopeHeld?Mathf.Max(holdDistance,objectRopeLength-Vector3.Distance(hand.position,ropeHoldPoint)):objectRopeLength;
            Vector3 force = WebPhysics.TetherForce(point, pulled.GetPointVelocity(point), pullOrigin,
                RopeHeld?Vector3.zero:handVelocity, remaining, pullSpring, pullDamping, forceLimit);
            pullTension = Mathf.Clamp01(force.magnitude / forceLimit);
            if(LiftAssist && !articulated && (carrying || InteractionMotion.CanCarry(pulled.worldCenterOfMass-hand.position,objectRopeLength)))
            {
                if(!carrying){easyDirection=motor.head.forward;carryTarget=pulled.worldCenterOfMass;carryTargetSpeed=Vector3.zero;ExperienceCue.Play(hand.position,1100,.055f);}
                carrying=true;
                Vector3 hold=hand.position+motor.head.forward*.55f-Vector3.up*.08f;
                carryTarget=Vector3.SmoothDamp(carryTarget,hold,ref carryTargetSpeed,.09f,3f,dt);
                Vector3 stable=Vector3.ClampMagnitude(carryTargetSpeed,1.5f);
                Vector3 acceleration=(carryTarget-pulled.worldCenterOfMass)*45f-(pulled.linearVelocity-stable)*13.5f-Physics.gravity;
                pulled.angularVelocity*=Mathf.Exp(-8*dt);
                force=Vector3.ClampMagnitude(acceleration*pulled.mass,forceLimit);
                pullTension=Mathf.Clamp01(force.magnitude/forceLimit);
                pulled.AddForce(force,ForceMode.Force);
            }
            else pulled.AddForceAtPosition(force, point, ForceMode.Force);
        }
        void LateUpdate()=>RefreshVisuals();
        public void RefreshVisuals()
        {
            UpdateThrowAim(Time.unscaledDeltaTime);
            DrawThrowPreview();
            carryHint.gameObject.SetActive(carrying);
            carryOutline.enabled=carrying&&pulled&&pulled.GetComponent<CargoReset>();
            if(carryOutline.enabled)
            {
                Bounds bounds=new Bounds(pulled.worldCenterOfMass,Vector3.zero);
                foreach(var collider in pulledColliders)if(collider&&collider.enabled)bounds.Encapsulate(collider.bounds);
                for(int i=0;i<16;i++)carryOutline.SetPosition(i,bounds.center+Vector3.Scale(outlineCorners[outlineOrder[i]],bounds.extents+Vector3.one*.015f));
            }
            if(carrying)
            {
                carryHint.transform.position=pulled.worldCenterOfMass+Vector3.up*.4f;
                carryHint.transform.rotation=Quaternion.LookRotation(carryHint.transform.position-motor.head.position);
                carryHint.text=EasyThrow?(ThrowArmed?"LOOK TO AIM / OPEN PALM":"CLOSE FINGERS TO READY"):"PUSH + OPEN PALM";
                carryHint.color=ThrowArmed?new Color(.25f,1,.7f):new Color(1,.75f,.2f);
            }
            if (Active)
            {
                aimGuide.enabled=aimRing.enabled=false;
                web.Draw(hand.position, EndPoint, HitNormal, Tension01, Time.time - firedAt,RopeHeld?(Vector3?)ropeHoldPoint:null);
                marker.SetActive(carrying&&ThrowPreviewVelocity.sqrMagnitude>.01f);
            }
            else
            {
                web.Hide();
                aimGuide.enabled=AimTracked;aimRing.enabled=false;
                if(!AimTracked){marker.SetActive(false);return;}
                bool valid = Aim(out RaycastHit hit); marker.SetActive(true);
                if(!valid){marker.transform.position=hand.position+hand.forward*1.4f;markerMaterial.color=new Color(1,.25f,.2f);}
                aimGuide.SetPosition(0,AimRay.origin);aimGuide.SetPosition(1,AimRay.GetPoint(valid?Mathf.Min(hit.distance,2f):1.4f));
                aimMaterial.color=valid?HandColor:Color.Lerp(HandColor,Color.gray,.65f);
                markerMaterial.color=aimMaterial.color;
                marker.transform.localScale=Vector3.one*.045f;
                if (valid)
                {
                    marker.transform.position = hit.point + hit.normal * .03f;
                    float size=HandAim.ReticleSize(hit.distance);marker.transform.localScale=Vector3.one*size*.3f;
                    aimRing.enabled=true;aimRing.startWidth=aimRing.endWidth=Mathf.Clamp(size*.045f,.006f,.025f);
                    Vector3 n=hit.normal.normalized,side=Vector3.Cross(n,Vector3.up);
                    if(side.sqrMagnitude<.01f)side=Vector3.Cross(n,Vector3.right);side.Normalize();Vector3 up=Vector3.Cross(side,n);
                    for(int i=0;i<33;i++){float a=i*Mathf.PI/16;aimRing.SetPosition(i,marker.transform.position+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*size*.5f);}
                }
            }
        }
        void DrawThrowPreview()
        {
            trajectory.enabled=carrying && pulled && ThrowPreviewVelocity.sqrMagnitude>.01f;
            if(!trajectory.enabled)return;
            const int count=21;trajectory.positionCount=count;
            Vector3 origin=pulled.worldCenterOfMass,previous=origin;
            for(int i=0;i<count;i++)
            {
                float t=i*.065f;
                Vector3 point=origin+ThrowPreviewVelocity*t+Physics.gravity*(.5f*t*t);
                if(i>0 && Physics.Linecast(previous,point,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.rigidbody!=pulled)
                {trajectory.SetPosition(i,hit.point);trajectory.positionCount=i+1;marker.transform.position=hit.point;break;}
                trajectory.SetPosition(i,point);previous=point;
                marker.transform.position=point;marker.transform.localScale=Vector3.one*.11f;
                markerMaterial.color=AimAssisted?new Color(1,.8f,.2f):new Color(.2f,1,.7f);
            }
        }
        void OnDisable() { if (motor) Release(); if (marker) marker.SetActive(false); }
        void OnDestroy() { if (marker) Destroy(marker); if (markerMaterial) Destroy(markerMaterial); if(trajectoryMaterial)Destroy(trajectoryMaterial);if(aimMaterial)Destroy(aimMaterial); }
    }
}
