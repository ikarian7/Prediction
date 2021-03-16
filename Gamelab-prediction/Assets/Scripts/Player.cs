using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Player : NetworkBehaviour
{
    [SyncVar]
    PlayerState state;

    void Awake()
    {
        InitState();
    }

    private void InitState()
    {
        state = new PlayerState
        {
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
                CmdMoveOnServer(playerInput);
            }
        }
        SyncState();
    }

    private void SyncState()
    {
        transform.position = state.position;
        transform.rotation = state.rotation;
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
            position = newPosition,
            rotation = newRotation
        };
    }

    [Command]
    void CmdMoveOnServer(PlayerInput playerInput)
    {
        state = ProcessPlayerInput(state, playerInput);
    }

}
