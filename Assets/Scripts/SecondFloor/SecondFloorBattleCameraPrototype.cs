using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Camera and input for the standalone second-floor encounter test, not the production battle flow.</summary>
public sealed class SecondFloorBattleCameraPrototype : MonoBehaviour
{
    private enum Phase { Explore, Entering, Battle, Returning, Advancing }

    [Header("Exploration grid")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform gridOrigin;
    [SerializeField, Min(0.1f)] private float cellSize = 3f;
    [SerializeField, Min(0.1f)] private float eyeHeight = 0.8f;
    [SerializeField, Range(30f, 100f)] private float explorationFov = 72f;

    [Header("Battle shot")]
    [SerializeField] private Transform cameraEnd;
    [SerializeField] private Transform battleAim;
    [SerializeField, Range(30f, 90f)] private float battleFov = 52f;
    [SerializeField, Min(0.05f)] private float enterSeconds = 0.95f;
    [SerializeField, Min(0.05f)] private float returnSeconds = 0.85f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Vector3 arcOffset = new Vector3(1.65f, 0.14f, 0.63f);
    [SerializeField, Range(0f, 1f)] private float playerRevealAt = 0.67f;
    [SerializeField] private Transform playerBillboard;
    [SerializeField] private Transform enemyBillboard;
    [SerializeField] private GameObject playerVisual;
    [SerializeField] private GameObject enemyOnTile;
    [SerializeField] private Renderer[] battleCutaway = System.Array.Empty<Renderer>();
    [SerializeField, Min(0.05f)] private float victoryStepSeconds = 0.18f;

    [Header("Scene UI")]
    [SerializeField] private GameObject explorationControls;
    [SerializeField] private GameObject battleHud;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button finishButton;
    [SerializeField] private TMP_Text status;
    [SerializeField] private string explorationStatus;
    [SerializeField] private string wallStatus;
    [SerializeField] private string enteringStatus;
    [SerializeField] private string battleStatus;
    [SerializeField] private string returningStatus;
    [SerializeField] private string returnedStatus;

    // Fixed whitebox: all five cells belong to the same straight strip.
    private const int StripLength = 5;
    private const int EncounterRow = 3;
    private FirstPersonGridExplorationSession exploration;
    private Phase phase;
    private float shotTime;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private float returnFov;
    private bool enemyPresent;
    private bool cutawayOpen;
    private bool[] initialRendererStates;
    private Vector3 victoryStepEnd;

    private void Awake()
    {
        initialRendererStates = new bool[battleCutaway.Length];
        for (int i = 0; i < battleCutaway.Length; i++)
            initialRendererStates[i] = battleCutaway[i] != null && battleCutaway[i].enabled;
        ResetPrototype();
    }

    private void Update()
    {
        if (phase == Phase.Entering || phase == Phase.Returning)
        {
            bool entering = phase == Phase.Entering;
            shotTime = Mathf.Clamp01(shotTime + (entering ? 1f : -1f) * Time.unscaledDeltaTime
                / Mathf.Max(0.05f, entering ? enterSeconds : returnSeconds));
            ApplyBattleShot(shotTime);

            if (entering && shotTime >= 1f)
                SetPhase(Phase.Battle, battleStatus);
            else if (!entering && shotTime <= 0f)
            {
                victoryStepEnd = GetEyePosition(exploration.X + exploration.FacingX, exploration.Y + exploration.FacingY);
                SetPhase(Phase.Advancing, returningStatus);
            }
            return;
        }

        if (phase == Phase.Advancing)
        {
            shotTime = Mathf.Clamp01(shotTime + Time.unscaledDeltaTime / Mathf.Max(0.05f, victoryStepSeconds));
            viewCamera.transform.position = Vector3.Lerp(returnPosition, victoryStepEnd, Mathf.SmoothStep(0f, 1f, shotTime));
            if (shotTime >= 1f)
            {
                exploration.MoveForward();
                ApplyExplorationPose();
                SetPhase(Phase.Explore, returnedStatus);
            }
            return;
        }

        if (phase != Phase.Explore) return;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) TurnLeft();
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) TurnRight();
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) MoveForward();
    }

    private void LateUpdate()
    {
        if (playerVisual.activeSelf) FaceCamera(playerBillboard);
        if (enemyPresent) FaceCamera(enemyBillboard);
    }

    private void FaceCamera(Transform billboard)
    {
        // Keep feet on the floor. The art lies on XY and its visible front faces local -Z.
        Vector3 awayFromCamera = billboard.position - viewCamera.transform.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(awayFromCamera, gridOrigin.up);
        if (horizontal.sqrMagnitude > 0.0001f)
            billboard.rotation = Quaternion.LookRotation(horizontal, gridOrigin.up);
    }

    public void ResetPrototype()
    {
        exploration = CreateExploration(0);
        SetEnemyPresent(true);
        SetCutaway(false);
        shotTime = 0f;
        playerVisual.SetActive(false);
        ApplyExplorationPose();
        SetPhase(Phase.Explore, explorationStatus);
    }

    public void TurnLeft() => Turn(-1);
    public void TurnRight() => Turn(1);

    private void Turn(int quarterTurns)
    {
        if (phase != Phase.Explore) return;
        exploration.Turn(quarterTurns);
        ApplyExplorationPose();
        status.text = explorationStatus;
    }

    public void MoveForward()
    {
        if (phase != Phase.Explore) return;
        int nextX = exploration.X + exploration.FacingX;
        int nextRow = exploration.Y + exploration.FacingY;
        if (enemyPresent && nextX == 0 && nextRow == EncounterRow)
        {
            BeginEncounter();
            return;
        }
        if (!exploration.MoveForward())
        {
            status.text = wallStatus;
            return;
        }
        ApplyExplorationPose();
        status.text = explorationStatus;
    }

    public void ReplayEncounter()
    {
        if (phase != Phase.Explore && phase != Phase.Battle) return;
        SetEnemyPresent(true);
        exploration = CreateExploration(EncounterRow - 1);
        ApplyExplorationPose();
        BeginEncounter();
    }

    public void FinishEncounter()
    {
        if (phase != Phase.Battle) return;
        SetEnemyPresent(false);
        SetPhase(Phase.Returning, returningStatus);
    }

    private static FirstPersonGridExplorationSession CreateExploration(int startRow) =>
        new FirstPersonGridExplorationSession(1, StripLength, 0, startRow, -1, -1, false,
            (x, row) => x == 0 && row >= 0 && row < StripLength, (_, __) => false);

    private Vector3 GetEyePosition(int x, int row) => gridOrigin.TransformPoint(new Vector3(x * cellSize, eyeHeight, row * cellSize));

    private void ApplyExplorationPose()
    {
        Vector3 localFacing = new Vector3(exploration.FacingX, 0f, exploration.FacingY);
        viewCamera.transform.SetPositionAndRotation(GetEyePosition(exploration.X, exploration.Y),
            Quaternion.LookRotation(gridOrigin.TransformDirection(localFacing), gridOrigin.up));
        viewCamera.fieldOfView = explorationFov;
    }

    private void BeginEncounter()
    {
        returnPosition = viewCamera.transform.position;
        returnRotation = viewCamera.transform.rotation;
        returnFov = viewCamera.fieldOfView;
        shotTime = 0f;
        ApplyBattleShot(0f);
        SetPhase(Phase.Entering, enteringStatus);
    }

    private void ApplyBattleShot(float time)
    {
        // Retrace the cutaway shot to the original pose before taking the pending step.
        float t = time <= 0f ? 0f : time >= 1f ? 1f : Mathf.Clamp01(ease.Evaluate(time));
        Vector3 position = Vector3.Lerp(returnPosition, cameraEnd.position, t)
            + gridOrigin.TransformVector(arcOffset) * (4f * t * (1f - t));
        Quaternion battleRotation = Quaternion.LookRotation(battleAim.position - cameraEnd.position, gridOrigin.up);
        viewCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(returnRotation, battleRotation, t));
        viewCamera.fieldOfView = Mathf.Lerp(returnFov, battleFov, t);
        // Open the near side before the camera leaves the strip; restore it once inside.
        SetCutaway(t > 0f);
        playerVisual.SetActive(t > 0f && t >= playerRevealAt);
    }

    private void SetEnemyPresent(bool present)
    {
        enemyPresent = present;
        enemyOnTile.SetActive(present);
    }

    private void SetCutaway(bool open)
    {
        if (cutawayOpen == open) return;
        cutawayOpen = open;
        for (int i = 0; i < battleCutaway.Length; i++)
            if (battleCutaway[i] != null)
                battleCutaway[i].enabled = !open && initialRendererStates[i];
    }

    private void SetPhase(Phase next, string message)
    {
        phase = next;
        explorationControls.SetActive(next == Phase.Explore);
        battleHud.SetActive(next == Phase.Battle);
        replayButton.interactable = next == Phase.Explore || next == Phase.Battle;
        finishButton.interactable = next == Phase.Battle;
        status.text = message;
    }
}
