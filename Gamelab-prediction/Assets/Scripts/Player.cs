using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Player : NetworkBehaviour
{
    [SyncVar(hook = "OnServerStateChanged")]
    public PlayerState state;

    private PlayerState predictedState;
    private List<PlayerInput> pendingMoves;

    void Awake()
    {
        InitState();
    }

    void Start()
    {
        if (isLocalPlayer)
        {
            pendingMoves = new List<PlayerInput>();
        }
    }

    private void InitState()
    {
        state = new PlayerState
        {
            timestamp = 0,
            position = Vector3.zero,
            rotation = Quaternion.Euler(0, 0, 0),
        };
    }

    void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            PlayerInput playerInput = GetPlayerInput();

            if (playerInput != null)
            {
                pendingMoves.Add(playerInput);
                UpdatePredictedState();
                CmdMoveOnServer(playerInput);
            }
        }
        SyncState();
    }

    void SyncState()
    {
        if (isServer)
        {
            transform.position = state.position;
            transform.rotation = state.rotation;
            return;
        }

        PlayerState stateToRender = isLocalPlayer ? predictedState : state;

        transform.position = Vector3.Lerp(transform.position,
            stateToRender.position * Settings.PlayerLerpSpacing,
            Settings.PlayerLerpEasing);
        transform.rotation = Quaternion.Lerp(transform.rotation,
            stateToRender.rotation,
            Settings.PlayerLerpEasing);
    }

    private PlayerInput GetPlayerInput()
    {
        PlayerInput playerInput = new PlayerInput();
        playerInput.forward += (sbyte)(Input.GetKey(KeyCode.W) ? 1 : 0);
        playerInput.forward += (sbyte)(Input.GetKey(KeyCode.S) ? -1 : 0);
        playerInput.rotate += (sbyte)(Input.GetKey(KeyCode.D) ? 1 : 0);
        playerInput.rotate += (sbyte)(Input.GetKey(KeyCode.A) ? -1 : 0);

        if (playerInput.forward == 0 && playerInput.rotate == 0)
            return null;
        return playerInput;
    }

    public PlayerState ProcessPlayerInput(PlayerState previous, PlayerInput playerInput)
    {
        Vector3 newPosition = previous.position;
        Quaternion newRotation = previous.rotation;

        if (playerInput.rotate != 0)
        {
            newRotation = previous.rotation
                * Quaternion.Euler(Vector3.up
                    * Settings.PlayerFixedUpdateInterval
                    * Settings.PlayerRotateSpeed
                    * playerInput.rotate);
        }

        if (playerInput.forward != 0)
        {
            newPosition = previous.position
                + newRotation
                    * Vector3.forward
                    * playerInput.forward
                    * Settings.PlayerFixedUpdateInterval
                    * Settings.PlayerMoveSpeed;
        }

        return new PlayerState
        {
            timestamp = previous.timestamp + 1,
            position = newPosition,
            rotation = newRotation
        };
    }

    [Command]
    void CmdMoveOnServer(PlayerInput playerInput)
    {
        state = ProcessPlayerInput(state, playerInput);
    }

    public void OnServerStateChanged(PlayerState newState)
    {
        state = newState;
        if (pendingMoves != null)
        {
            while (pendingMoves.Count >
                  (predictedState.timestamp - state.timestamp))
            {
                pendingMoves.RemoveAt(0);
            }
            UpdatePredictedState();
        }
    }

    public void UpdatePredictedState()
    {
        predictedState = state;
        foreach (PlayerInput playerInput in pendingMoves)
        {
            predictedState = ProcessPlayerInput(predictedState, playerInput);
        }
    }
}
