using Fusion;
using Fusion.Addons.SimpleKCC;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] modelParts;
    [SerializeField] private SimpleKCC kcc;
    [SerializeField] private Transform camTarget;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpImpulse = 10f;

    public double Score => Math.Round(transform.position.y, 1);
    public bool IsReady; //Server is the only one who cares about this

    [Networked] public string Name {  get; private set; }

    [Networked] private NetworkButtons PreviosButtons {  get; set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;
    public override void Spawned()
    {
        kcc.SetGravity(Physics.gravity.y * 2f);

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
            kcc.Settings.ForcePredictedLookRotation = true;
        }     
    }

    public override void FixedUpdateNetwork() //Execute any logic that affects gameplay, such as player movement and interactions
    {
        if (GetInput(out NetInput input)) // predict movement
        {
            kcc.AddLookRotation(input.LookDelta * lookSensitivity);
            UpdateCamTarget();

            Vector3 worldDirection = kcc.TransformRotation * new Vector3(input.Direction.x, 0f, input.Direction.y);
            float jump = 0f;
            if (input.Buttons.WasPressed(PreviosButtons, InputButton.Jump) && kcc.IsGrounded)
                jump = jumpImpulse;
            kcc.Move(worldDirection.normalized * speed, jump); //Normalized to prevent cheating
            PreviosButtons = input.Buttons;
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerName(string name)
    {
        Name = name;
    }
}
