using Fusion;
using Fusion.Addons.KCC;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Windows;

public class Player : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] modelParts;
    [SerializeField] private KCC kcc;
    [SerializeField] private KCCProcessor glideProccesor;
    [SerializeField] private Transform camTarget;
    [SerializeField] private AudioSource source;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;
    //[SerializeField] private float speed = 5f;
    [SerializeField] private Vector3 jumpImpulse = new (0f,10f, 0f);
    [SerializeField] private float doubleJumpMultiplier = 0.75f;
    [SerializeField] private float grappleCD = 2f;
    [SerializeField] private float glideCD = 2f;
    [SerializeField] private float doubleJumpCD = 5f;
    [SerializeField] private float grappleStrength = 12f;
    [SerializeField] private float maxGlideTime = 2f;
    [field: SerializeField] public float AbilityRange { get; private set; } = 25f;
    public float GlideCDFactor => (GlideCD.RemainingTime(Runner) ?? 0f) / glideCD; //Returns the remaining of cooldown in a range of 0 to 1
    public float GrappleCDFactor => (GrappleCD.RemainingTime(Runner) ?? 0f) / grappleCD; //Returns the remaining of cooldown in a range of 0 to 1

    public float DoubleJumpCDFactor => (DoubleJumpCD.RemainingTime(Runner) ?? 0f) / doubleJumpCD; //Returns the remaining of cooldown in a range of 0 to 1
    public double Score => Math.Round(transform.position.y, 1);
    public bool IsReady; //Server is the only one who cares about this


    private bool CanGlide => !kcc.Data.IsGrounded && GlideCharge > 0f;
    [Networked] public string Name {  get; private set; }

    [Networked] public float GlideCharge { get; private set; }
    [Networked] public bool IsGliding { get; private set; }
    [Networked] private TickTimer GlideCD { get; set; } //Handly struct to wait for a specified period of time
    [Networked] private TickTimer GrappleCD { get; set; } //Handly struct to wait for a specified period of time

    [Networked] private TickTimer DoubleJumpCD { get; set; } //Handly struct to wait for a specified period of time
    [Networked] private NetworkButtons PreviousButtons {  get; set; }

    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync {  get; set; } //Synchronize sound in all clients
    private InputManager inputManager;
    private Vector2 baseLookRotation;
    private float glideDrain;
    public override void Spawned()
    {
        glideDrain = 1f / (maxGlideTime * Runner.TickRate);
        GlideCharge = 1f;
        //kcc.SetGravity(Physics.gravity.y * 2f);

        //Hide 3d model of the character
        if (HasInputAuthority)
        {
            foreach (MeshRenderer renderer in modelParts)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
           
            inputManager = Runner.GetComponent<InputManager>();
            inputManager.LocalPlayer = this;
            //Runner.GetComponent<InputManager>().LocalPlayer = this;
            Name = PlayerPrefs.GetString("Photon.Menu.Username");
            RPC_PlayerName(Name);//Remote Procedure Calls, ask other network peers to execute a method with certain parameters
            CameraFollow.Singleton.SetTarget(camTarget);
            UIManager.Singleton.LocalPlayer = this;
            kcc.Settings.ForcePredictedLookRotation = true;
        }     
    }

    public override void FixedUpdateNetwork() //Execute any logic that affects gameplay, such as player movement and interactions
    {
        if (GetInput(out NetInput input)) // predict movement
        {
            CheckGlide(input);
            CheckJump(input);
            kcc.AddLookRotation(input.LookDelta * lookSensitivity, -maxPitch, maxPitch);
            UpdateCamTarget();

            if (input.Buttons.WasPressed(PreviousButtons, InputButton.Grapple))
                TryGrapple(camTarget.forward);

            if (IsGliding && !CanGlide)
                ToogleGlide(false);
            
            /*float jump = 0f;
            if (input.Buttons.WasPressed(PreviosButtons, InputButton.Jump) && kcc.IsGrounded)
                jump = jumpImpulse;*/
            //kcc.Move(worldDirection.normalized * speed, jump); //Normalized to prevent cheating
            SetInputDirection(input);
            PreviousButtons = input.Buttons;
            baseLookRotation = kcc.GetLookRotation();
        }
    }

    public override void Render()
    {
        if(kcc.Settings.ForcePredictedLookRotation)
        {
            Vector2 predictedLookRotation = baseLookRotation + inputManager.AccumulatedMouseDelta * lookSensitivity;
            kcc.SetLookRotation(predictedLookRotation);
        }

        UpdateCamTarget();
    }
    private void SetInputDirection(NetInput input)
    {
        Vector3 worldDirection;
        if (IsGliding)
        {
            GlideCharge = Mathf.Max(0f, GlideCharge - glideDrain);
            worldDirection = kcc.Data.TransformDirection;
        }
        worldDirection = kcc.FixedData.TransformRotation * input.Direction.X0Y();
        kcc.SetInputDirection(worldDirection);
    }

    private void CheckGlide(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Glide) && GlideCD.ExpiredOrNotRunning(Runner) && CanGlide)
            ToogleGlide(true);
        else if (input.Buttons.WasReleased(PreviousButtons, InputButton.Glide) && IsGliding)
            ToogleGlide(false);
    }
    private void CheckJump(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (kcc.FixedData.IsGrounded)
            {
                kcc.Jump(jumpImpulse);
                JumpSync++;
            }
            else if (DoubleJumpCD.ExpiredOrNotRunning(Runner))
            {
                kcc.Jump(jumpImpulse * doubleJumpMultiplier);
                DoubleJumpCD = TickTimer.CreateFromSeconds(Runner, doubleJumpCD);
                ToogleGlide(false);
                JumpSync++;
            }
        }
    }

    private void UpdateCamTarget()
    {
        camTarget.localRotation = Quaternion.Euler(kcc.GetLookRotation().x, 0f, 0f);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)] // The ui update is actually allowed to run locally when the player indicates their readiness
    public void RPC_SetReady()
    {
        IsReady = true;
        if (HasInputAuthority)
            UIManager.Singleton.DidSetReady();
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        kcc.SetPosition(position);
        kcc.SetLookRotation(rotation);
    }

    public void ResetCooldowns()
    {
        GlideCD = TickTimer.None;
        GrappleCD = TickTimer.None;
        DoubleJumpCD = TickTimer.None;
    }

    private void TryGrapple(Vector3 lookDirection)
    {
        if (GrappleCD.ExpiredOrNotRunning(Runner) && Physics.Raycast(camTarget.position, lookDirection, out RaycastHit hitInfo, AbilityRange))
        {
            if (hitInfo.collider.TryGetComponent(out Block _))
            {
                GrappleCD = TickTimer.CreateFromSeconds(Runner, grappleCD);
                Vector3 grappleVector = Vector3.Normalize(hitInfo.point - transform.position);
                if(grappleVector.y > 0f) //Force more vertical
                {
                    grappleVector = Vector3.Normalize(grappleVector + Vector3.up);
                }

                kcc.Jump(grappleVector * grappleStrength); // Apply the force of the grapple
                ToogleGlide(false);
            }
        }
    }

    private void ToogleGlide(bool isGliding)
    {
        if (IsGliding = isGliding)
            return;

        if (IsGliding)
        {
            kcc.AddModifier(glideProccesor);
            Vector3 velocity = kcc.Data.DynamicVelocity;
            velocity.y *= 0.25f;
            kcc.SetDynamicVelocity(velocity);
        }
        else
        {
            kcc.RemoveModifier(glideProccesor);
            GlideCharge = 1f;
            GlideCD = TickTimer.CreateFromSeconds(Runner, glideCD);
        }
        IsGliding = isGliding;
    }
    private void Jumped()
    {
        source.Play();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerName(string name)
    {
        Name = name;
    }
}
