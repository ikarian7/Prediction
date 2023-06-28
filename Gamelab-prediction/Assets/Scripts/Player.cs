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

    private float extrapolationDelay = 0.1f; // Vertraging voor het toepassen van dead reckoning
    private float lastNetworkTime = 0f;
    private float lastExtrapolationTime = 0f;

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
            velocity = Vector3.zero
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

    // Synchroniseert de staat van de speler met de server of past dead reckoning toe voor externe spelers
    void SyncState()
    {
        if (isServer)
        {
            // Update de positie en rotatie direct op de server
            transform.position = state.position;
            transform.rotation = state.rotation;
            return;
        }

        PlayerState stateToRender = isLocalPlayer ? predictedState : state;

        // Bereken de tijd die is verstreken sinds de laatste ontvangen staat
        float timeSinceLastState = Time.time - state.timestamp * Settings.PlayerFixedUpdateInterval;
        float extrapolationDuration = Mathf.Clamp(timeSinceLastState, 0f, Settings.PlayerFixedUpdateInterval);

        if (Time.time > lastNetworkTime + extrapolationDelay)
        {
            // Pas dead reckoning toe door de positie te extrapoleren op basis van de snelheid
            Vector3 extrapolatedPosition = state.position + state.velocity * extrapolationDuration;
            Quaternion extrapolatedRotation = state.rotation;

            transform.position = Vector3.Lerp(transform.position,
                extrapolatedPosition * Settings.PlayerLerpSpacing,
                Settings.PlayerLerpEasing);
            transform.rotation = Quaternion.Lerp(transform.rotation,
                extrapolatedRotation,
                Settings.PlayerLerpEasing);
        }
        else
        {
            // Gebruik interpolatie om de ontvangen staat vloeiend weer te geven
            transform.position = Vector3.Lerp(transform.position,
                stateToRender.position * Settings.PlayerLerpSpacing,
                Settings.PlayerLerpEasing);
            transform.rotation = Quaternion.Lerp(transform.rotation,
                stateToRender.rotation,
                Settings.PlayerLerpEasing);
        }
    }

    // Haalt de input van de speler op op basis van toetsenbordtoetsen
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

    // Verwerkt de input van de speler en berekent de nieuwe staat
    public PlayerState ProcessPlayerInput(PlayerState previous, PlayerInput playerInput)
    {
        Vector3 newPosition = previous.position;
        Quaternion newRotation = previous.rotation;

        if (playerInput.rotate != 0)
        {
            // Draai de speler op basis van de input
            newRotation = previous.rotation
                * Quaternion.Euler(Vector3.up
                    * Settings.PlayerFixedUpdateInterval
                    * Settings.PlayerRotateSpeed
                    * playerInput.rotate);
        }

        if (playerInput.forward != 0)
        {
            // Verplaats de speler naar voren op basis van de input
            newPosition = previous.position
                + previous.rotation
                    * Vector3.forward
                    * playerInput.forward
                    * Settings.PlayerFixedUpdateInterval
                    * Settings.PlayerMoveSpeed;
        }

        // Bereken de nieuwe snelheid op basis van de nieuwe positie
        Vector3 newVelocity = (newPosition - previous.position) / Settings.PlayerFixedUpdateInterval;

        return new PlayerState
        {
            timestamp = previous.timestamp + 1,
            position = newPosition,
            rotation = newRotation,
            velocity = newVelocity
        };
    }

    // Stuurt de input van de speler naar de server voor verwerking
    [Command]
    void CmdMoveOnServer(PlayerInput playerInput)
    {
        state = ProcessPlayerInput(state, playerInput);
        lastNetworkTime = Time.time; // Update de laatste netwerktijd
    }

    // Wordt aangeroepen op de client wanneer de staat van de server verandert
    public void OnServerStateChanged(PlayerState newState)
    {
        state = newState;
        if (pendingMoves != null)
        {
            while (pendingMoves.Count > (state.timestamp - predictedState.timestamp))
            {
                pendingMoves.RemoveAt(0);
            }
            UpdatePredictedState();
        }
    }

    // Update de voorspelde staat op basis van de opgeslagen input
    public void UpdatePredictedState()
    {
        predictedState = state;
        foreach (PlayerInput playerInput in pendingMoves)
        {
            predictedState = ProcessPlayerInput(predictedState, playerInput);
        }
    }
}