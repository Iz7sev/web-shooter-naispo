using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace WebSwing
{
    public sealed class WebInput : MonoBehaviour
    {
        public Transform head,aim;
        public SwingMotor motor;
        public WebShooter shooter,leftShooter;
        public WorldModes modes;
        public float poseHoldTime=.08f,poseReleaseTime=.16f,trackingGrace=.25f;
        public string TrackingStatus { get; private set; }="DESKTOP";
        public bool WalkEnabled=true;
        public bool TapToShoot=true;
        public RoomRopeGrab RopeGrab { get; private set; }
        readonly bool[] externalDown=new bool[2],externalPrevious=new bool[2],externalOwned=new bool[2],externalReady=new bool[2];
        readonly float[] externalReceived=new float[2];
        public bool IsHandTracked(int hand) => tracked[hand];
        public void ReceiveExternalTrigger(int hand,bool down)
        {
            if(hand<0||hand>1) return;
            externalReceived[hand]=Time.unscaledTime;
            if(modes && modes.MenuVisible) { externalDown[hand]=externalReady[hand]=false; return; }
            if(!down) externalReady[hand]=true;
            externalDown[hand]=down&&externalReady[hand];
        }
        public void ToggleShot(int hand)
        {
            var web=ForHand(hand);
            if(web.Active) web.Release(); else Shoot(hand);
        }
        public bool Shoot(int hand)
        {
            var web=ForHand(hand);
            if(!modes||!modes.IsMR)return web.Fire(hand);
            bool fired=web.FireDetached(hand);
            if(fired&&RopeGrab)RopeGrab.RememberShot(hand,web.LastDetachedWeb);
            return fired;
        }
        readonly List<XRHandSubsystem> systems=new List<XRHandSubsystem>();
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();
        readonly float[] held=new float[2],absent=new float[2],openHeld=new float[2];
        readonly Vector3[] lastLocalHand=new Vector3[2],throwVelocity=new Vector3[2];
        readonly float[] handSampleTime=new float[2];
        readonly bool[] motionSample=new bool[2];
        readonly float[] readyHeld=new float[2];
        readonly Vector3[] frozenAim=new Vector3[2];
        readonly ThrowMotionMemory[] throwMemory={new ThrowMotionMemory(),new ThrowMotionMemory()};
        readonly bool[] menuPose=new bool[2];
        readonly MenuPoseHold menuGestureHold=new MenuPoseHold();
        readonly bool[] latched=new bool[2],blocked=new bool[2],pinched=new bool[2],tracked=new bool[2],open=new bool[2];
        readonly Vector3[] wrists=new Vector3[2],previousWrists=new Vector3[2];
        readonly TrackedHandVisual[] visuals=new TrackedHandVisual[2];
        XRHandSubsystem hands;
        bool wasXR,floorSet,walkSample;
        float yaw,pitch,walkFiltered;
        WebShooter ForHand(int index) => index==0?leftShooter:shooter;
        public bool AnyWeb => shooter.Active || (leftShooter && leftShooter.Active);
        void Start()
        {
            RopeGrab=gameObject.AddComponent<RoomRopeGrab>();RopeGrab.Initialize(leftShooter,shooter,motor);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Left tracked hand":"Right tracked hand");
                go.transform.SetParent(transform,false); visuals[i]=go.AddComponent<TrackedHandVisual>();
            }
        }
        public void ReleaseAll()
        {
            menuGestureHold.ResetProgress();if(RopeGrab)RopeGrab.ResetGrabs();
            if(shooter) shooter.ReleaseTrackingLost(); if(leftShooter) leftShooter.ReleaseTrackingLost(); motor.SetArmWalkSpeed(0);
            for(int i=0;i<2;i++) { blocked[i]=true; latched[i]=false; held[i]=0; externalDown[i]=externalPrevious[i]=externalOwned[i]=externalReady[i]=false; openHeld[i]=0; motionSample[i]=false; throwVelocity[i]=Vector3.zero; throwMemory[i].Clear();readyHeld[i]=0;frozenAim[i]=Vector3.zero; }
        }
        void Update()
        {
            bool xr=XRSettings.isDeviceActive;
            if(xr!=wasXR) { ReleaseAll(); wasXR=xr; walkSample=false; Cursor.lockState=CursorLockMode.None; }
            if(xr) UpdateXR(); else UpdateDesktop();
        }
        void UpdateXR()
        {
            if(!floorSet)
            {
                inputs.Clear(); SubsystemManager.GetSubsystems(inputs);
                foreach(var subsystem in inputs) if(subsystem.running) floorSet|=subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
            motor.SetDesktopMove(Vector2.zero,false);
            var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 position)) head.localPosition=position;
            if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out Quaternion rotation)) head.localRotation=rotation;
            if(hands==null || !hands.running) { systems.Clear(); SubsystemManager.GetSubsystems(systems); hands=systems.Find(s=>s.running); }
            if(hands==null) { menuGestureHold.ResetProgress(); TrackingStatus="XR: WAITING FOR HANDS"; LoseHand(0); LoseHand(1); StopWalk(); return; }
            UpdateHand(hands.leftHand,0); UpdateHand(hands.rightHand,1);
            if(RopeGrab)RopeGrab.Step(Time.unscaledDeltaTime);
            if(menuGestureHold.Step(tracked[0]&&menuPose[0],tracked[1]&&menuPose[1],Time.unscaledDeltaTime) && modes && !modes.MenuVisible)
                modes.ShowMenu();
            UpdateWalk();
            TrackingStatus="HANDS: "+(tracked[0]?"L ":"")+(tracked[1]?"R ":"")+" | WALK "+walkFiltered.ToString("F1")+" m/s";
        }
        void UpdateHand(XRHand hand,int index)
        {
            var web=ForHand(index);
            if(!web) return;
            if(!hand.isTracked || !Point(hand,XRHandJointID.Wrist,out var wrist) ||
                !Point(hand,XRHandJointID.IndexProximal,out var knuckleA) || !Point(hand,XRHandJointID.LittleProximal,out var knuckleB))
            { LoseHand(index); return; }
            visuals[index].Draw(hand); tracked[index]=true; wrists[index]=wrist;
            if(NaispoSession.Active&&!NaispoSession.Active.AcceptHands)
            {web.Release();web.AimTracked=false;motor.SetGrip(index,false);motionSample[index]=false;return;}
            bool valid=Finger(hand,XRHandJointID.IndexProximal,XRHandJointID.IndexIntermediate,XRHandJointID.IndexDistal,XRHandJointID.IndexTip,out float a);
            valid&=Finger(hand,XRHandJointID.MiddleProximal,XRHandJointID.MiddleIntermediate,XRHandJointID.MiddleDistal,XRHandJointID.MiddleTip,out float b);
            valid&=Finger(hand,XRHandJointID.RingProximal,XRHandJointID.RingIntermediate,XRHandJointID.RingDistal,XRHandJointID.RingTip,out float c);
            valid&=Finger(hand,XRHandJointID.LittleProximal,XRHandJointID.LittleIntermediate,XRHandJointID.LittleDistal,XRHandJointID.LittleTip,out float d);
            if(!valid) { LoseHand(index); return; }
            bool pose=valid&&InteractionMotion.WebPose(a,b,c,d);
            open[index]=valid&&a>.72f&&b>.72f&&c>.72f&&d>.68f;
            if(open[index]) blocked[index]=false;
            bool aimValid=HandAim.TryForwardRay(wrist,knuckleA,knuckleB,transform.position,transform.rotation,out var forwardRay);
            if(aimValid)
                web.hand.SetPositionAndRotation(forwardRay.origin,HandAim.RayRotation(forwardRay.direction));
            else web.hand.position=transform.TransformPoint(wrist)+web.hand.forward*.012f;
            web.AimTracked=aimValid;
            bool fist=!pose&&a<.72f&&d<.70f&&b<.78f&&c<.78f&&(a+b+c+d)<2.72f;
            motor.SetGrip(index,fist);
            if(RopeGrab)RopeGrab.SetHand(index,!modes||!modes.MenuVisible,fist,transform.TransformPoint((wrist+(knuckleA+knuckleB)*.5f)*.5f));
            bool fingerTips=Point(hand,XRHandJointID.IndexTip,out var tip)&Point(hand,XRHandJointID.ThumbTip,out var thumb);
            menuPose[index]=InteractionMotion.MenuPeace(a,b,c,d);
            bool pinch=fingerTips&&Vector3.Distance(tip,thumb)<.028f;
            if(modes && modes.MenuVisible)
            {
                web.Release();web.AimTracked=false; held[index]=0; latched[index]=false; blocked[index]=true;
                Vector3 rayDirection=transform.TransformDirection(tip-knuckleA).normalized;
                if(fingerTips && a>.70f) modes.Point(index,new Ray(transform.TransformPoint(knuckleA),rayDirection),false);
                else modes.ClearPointer(index);
                motionSample[index]=false; openHeld[index]=0; throwMemory[index].Clear();
                pinched[index]=pinch; return;
            }
            pinched[index]=pinch;
            // Use tracking-space displacement; walking and recentering must not add throw speed.
            float sampleDt=Time.unscaledTime-handSampleTime[index];
            if(motionSample[index] && sampleDt>.001f && sampleDt<.12f && (wrist-lastLocalHand[index]).magnitude<.22f)
                throwVelocity[index]=Vector3.Lerp(throwVelocity[index],(wrist-lastLocalHand[index])/sampleDt,1-Mathf.Exp(-sampleDt*22));
            else throwVelocity[index]=Vector3.zero;
            motionSample[index]=true; lastLocalHand[index]=wrist; handSampleTime[index]=Time.unscaledTime;
            bool hardware=externalDown[index]&&Time.unscaledTime-externalReceived[index]<.3f;
            if(hardware&&!externalPrevious[index]) { ToggleShot(index); externalOwned[index]=web.Active; }
            if(externalOwned[index]&&Time.unscaledTime-externalReceived[index]>=.3f) { web.Release(); externalOwned[index]=false; }
            externalPrevious[index]=hardware;
            StepTrackedGesture(index,pose&&aimValid,open[index],throwVelocity[index],Time.unscaledDeltaTime);
        }
        public void StepTrackedGesture(int index,bool pose,bool isOpen,Vector3 localVelocity,float dt)
        {
            var web=ForHand(index);
            if(!motor.MovementLocked && motor.IsAttached(index))
            {
                openHeld[index]=isOpen?openHeld[index]+dt:0;
                if(openHeld[index]>=.12f){web.Release();motor.SetGrip(index,false);openHeld[index]=0;blocked[index]=true;latched[index]=true;}
                return;
            }
            if(web.EasyThrow && web.Carrying)
            {
                web.UpdateThrowAim(dt);
                if(!isOpen){readyHeld[index]+=dt;if(readyHeld[index]>=.12f)web.ThrowArmed=true;openHeld[index]=0;frozenAim[index]=Vector3.zero;}
                else if(web.ThrowArmed)
                {
                    if(openHeld[index]<=0)frozenAim[index]=web.PlannedThrow;
                    openHeld[index]+=dt;
                    if(openHeld[index]>=.12f){web.ThrowPlanned(frozenAim[index]);openHeld[index]=readyHeld[index]=0;blocked[index]=true;latched[index]=true;}
                }
                return;
            }
            readyHeld[index]=0;
            throwMemory[index].Sample(localVelocity,head.localRotation*Vector3.forward,web.Carrying,dt);
            Vector3 remembered=throwMemory[index].Velocity;
            web.ThrowPreviewVelocity=remembered==Vector3.zero?Vector3.zero:InteractionMotion.ThrowVelocity(transform.TransformDirection(remembered));
            if(isOpen)blocked[index]=false;
            pose &= !isOpen;
            if(web.Active && TapToShoot && isOpen)
            {
                openHeld[index]+=dt;
                // A quick forward push needs only a short, clearly tracked open palm.
                bool throwing=web.Carrying && remembered!=Vector3.zero;
                if(openHeld[index]>=(throwing?.05f:.7f))
                {
                    if(throwing)web.Throw(transform.TransformDirection(remembered)); else web.Release();
                    throwMemory[index].Clear();
                    blocked[index]=true; latched[index]=true; openHeld[index]=0;
                }
            }
            else openHeld[index]=0;
            if(pose&&!blocked[index])
            {
                absent[index]=0; held[index]+=dt;
                if(!latched[index]&&held[index]>=poseHoldTime) { latched[index]=true; if(TapToShoot) { if(!web.Active||web.RoomSurfaceWeb)Shoot(index); } else Shoot(index); }
            }
            else
            {
                held[index]=0; absent[index]+=dt;
                if(absent[index]>poseReleaseTime) { if(!TapToShoot)web.Release(); latched[index]=false; }
            }
        }

        void UpdateWalk()
        {
            float dt=Mathf.Max(Time.deltaTime,.001f);
            bool allowed=(!NaispoSession.Active||(NaispoSession.Active.AcceptHands&&!NaispoSession.Active.Guided))&&WalkEnabled&&!menuPose[0]&&!menuPose[1]&&tracked[0]&&tracked[1]&&!AnyWeb&&(!modes||(!modes.IsMR&&!modes.MenuVisible))&&
                (motor.WallAttached||motor.Mantling||
                (wrists[0].y<head.localPosition.y-.3f&&wrists[1].y<head.localPosition.y-.3f));
            if(!walkSample || !allowed)
            {
                previousWrists[0]=wrists[0]; previousWrists[1]=wrists[1]; walkSample=allowed; StopWalk(); return;
            }
            Vector3 left=(wrists[0]-previousWrists[0])/dt,right=(wrists[1]-previousWrists[1])/dt;
            previousWrists[0]=wrists[0]; previousWrists[1]=wrists[1];
            Vector3 forward=Vector3.ProjectOnPlane(head.localRotation*Vector3.forward,Vector3.up).normalized;
            float target=motor.WallAttached?InteractionMotion.ClimbTarget(left,right,forward):InteractionMotion.WalkTarget(left,right,forward);
            walkFiltered=Mathf.MoveTowards(walkFiltered,target,Time.deltaTime*(target>walkFiltered?3.5f:5f));
            motor.SetArmWalkSpeed(walkFiltered);
        }
        void StopWalk() { walkFiltered=0; motor.SetArmWalkSpeed(0); }
        void LoseHand(int index)
        {
            externalDown[index]=externalReady[index]=false;
            motor.SetGrip(index,false);if(ForHand(index))ForHand(index).AimTracked=false;
            motionSample[index]=false; throwVelocity[index]=Vector3.zero; openHeld[index]=readyHeld[index]=0; throwMemory[index].Clear();if(ForHand(index))ForHand(index).ThrowArmed=false;
            if(modes)modes.ClearPointer(index);
            if(RopeGrab)RopeGrab.SetHand(index,false,false,Vector3.zero);
            menuPose[index]=false; tracked[index]=false; open[index]=false; pinched[index]=false;
            if(visuals[index]) visuals[index].SetVisible(false);
            held[index]=0; absent[index]+=Time.deltaTime;
            if(absent[index]>(motor.IsAttached(index)?.65f:trackingGrace)) { if(ForHand(index)) ForHand(index).ReleaseTrackingLost(); latched[index]=false; blocked[index]=true; }
        }
        static bool Point(XRHand hand,XRHandJointID id,out Vector3 p)
        { bool valid=hand.GetJoint(id).TryGetPose(out Pose pose); p=pose.position; return valid; }
        static bool Finger(XRHand hand,XRHandJointID a,XRHandJointID b,XRHandJointID c,XRHandJointID d,out float ratio)
        {
            ratio=0;
            if(!Point(hand,a,out var p)||!Point(hand,b,out var q)||!Point(hand,c,out var r)||!Point(hand,d,out var s)) return false;
            float length=Vector3.Distance(p,q)+Vector3.Distance(q,r)+Vector3.Distance(r,s);
            if(length<.001f) return false; ratio=Vector3.Distance(p,s)/length; return true;
        }
        void UpdateDesktop()
        {
            foreach(var visual in visuals) if(visual) visual.SetVisible(false);
            var mouse=Mouse.current; var keyboard=Keyboard.current;
            if(mouse==null||keyboard==null) return;
            TrackingStatus="LMB: RIGHT / RMB: LEFT / F: PULL / M: MENU";
            if(keyboard.mKey.wasPressedThisFrame && modes) { if(modes.MenuVisible)modes.HideMenu();else modes.ShowMenu(); }
            if(keyboard.digit1Key.wasPressedThisFrame && modes) modes.SelectMode(false);
            if(keyboard.digit2Key.wasPressedThisFrame && modes) modes.SelectMode(true);
            if(keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState=CursorLockMode.None; ReleaseAll(); }
            if(modes && modes.MenuVisible)
            {
                Cursor.lockState=CursorLockMode.None;
                modes.Point(1,head.GetComponent<Camera>().ScreenPointToRay(mouse.position.ReadValue()),mouse.leftButton.wasPressedThisFrame);
                motor.SetDesktopMove(Vector2.zero,false); return;
            }
            if(mouse.leftButton.wasPressedThisFrame && Cursor.lockState!=CursorLockMode.Locked) Cursor.lockState=CursorLockMode.Locked;
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                Vector2 delta=mouse.delta.ReadValue()*.1f; yaw+=delta.x; pitch=Mathf.Clamp(pitch-delta.y,-85,85);
                head.localRotation=Quaternion.Euler(pitch,yaw,0);
            }
            motor.SetGrip(1,mouse.leftButton.isPressed);motor.SetGrip(0,mouse.rightButton.isPressed);
            shooter.AimTracked=true;if(leftShooter)leftShooter.AimTracked=true;
            head.localPosition=new Vector3(0,1.65f,0);
            float pullOffset=keyboard.fKey.isPressed?.65f:0;
            aim.SetPositionAndRotation(head.position+head.right*.18f-head.up*.2f-head.forward*pullOffset,head.rotation);
            if(leftShooter) leftShooter.hand.SetPositionAndRotation(head.position-head.right*.18f-head.up*.2f-head.forward*pullOffset,head.rotation);
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                if(mouse.leftButton.wasPressedThisFrame) { if(TapToShoot)ToggleShot(1);else shooter.Fire(2); }
                if(mouse.rightButton.wasPressedThisFrame && leftShooter) { if(TapToShoot)ToggleShot(0);else leftShooter.Fire(3); }
            }
            if(keyboard.tKey.wasPressedThisFrame && shooter.Carrying) shooter.Throw(head.forward*2f+Vector3.up*.5f);
            if(!TapToShoot&&mouse.leftButton.wasReleasedThisFrame) shooter.Release(2);
            if(!TapToShoot&&mouse.rightButton.wasReleasedThisFrame && leftShooter) leftShooter.Release(3);
            Vector2 move=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            motor.SetDesktopMove(Vector2.ClampMagnitude(move,1),keyboard.spaceKey.wasPressedThisFrame);
            if(keyboard.rKey.wasPressedThisFrame) { ReleaseAll(); motor.ResetPlayer(); }
        }
        void OnApplicationFocus(bool focus) { if(!focus) ReleaseAll(); }
        void OnDisable() { if(motor) ReleaseAll(); Cursor.lockState=CursorLockMode.None; }
    }
}

