using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Camera and input for the standalone second-floor encounter test, not the production battle flow.</summary>
public sealed class SecondFloorBattleCameraPrototype : MonoBehaviour
{
    private enum Phase { Explore, Entering, Battle, Returning }

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

    // Fixed whitebox: one column of corridor, then three columns of encounter room.
    private const int CenterColumn = 1;
    private const int EncounterRow = 3;
    private FirstPersonGridExplorationSession exploration;
    private Phase phase;
    private float shotTime;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private float returnFov;
    private bool encounterArmed;

    private void Awake() => ResetPrototype();

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
                SetPhase(Phase.Explore, returnedStatus);
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
        FaceCamera(enemyBillboard);
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
        encounterArmed = true;
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
        if (!exploration.MoveForward())
        {
            status.text = wallStatus;
            return;
        }

        ApplyExplorationPose();
        if (exploration.X != CenterColumn || exploration.Y != EncounterRow)
            encounterArmed = true;
        else if (encounterArmed && exploration.FacingY == 1)
        {
            BeginEncounter();
            return;
        }
        status.text = explorationStatus;
    }

    public void ReplayEncounter()
    {
        if (phase == Phase.Entering || phase == Phase.Returning) return;
        exploration = CreateExploration(EncounterRow);
        ApplyExplorationPose();
        BeginEncounter();
    }

    public void FinishEncounter()
    {
        if (phase != Phase.Battle) return;
        SetPhase(Phase.Returning, returningStatus);
    }

    private static FirstPersonGridExplorationSession CreateExploration(int startRow) =>
        new FirstPersonGridExplorationSession(3, 5, CenterColumn, startRow, -1, -1, false,
            IsWalkable, (_, __) => false);

    private static bool IsWalkable(int x, int row) => x >= 0 && x < 3 && row >= 0 && row < 5
        && (row >= 2 || x == CenterColumn) && (x != 2 || row != 4); // Enemy occupies the last tile.

    private void ApplyExplorationPose()
    {
        Vector3 localPosition = new Vector3((exploration.X - CenterColumn) * cellSize, eyeHeight, exploration.Y * cellSize);
        Vector3 localFacing = new Vector3(exploration.FacingX, 0f, exploration.FacingY);
        viewCamera.transform.SetPositionAndRotation(gridOrigin.TransformPoint(localPosition),
            Quaternion.LookRotation(gridOrigin.TransformDirection(localFacing), gridOrigin.up));
        viewCamera.fieldOfView = explorationFov;
    }

    private void BeginEncounter()
    {
        encounterArmed = false;
        returnPosition = viewCamera.transform.position;
        returnRotation = viewCamera.transform.rotation;
        returnFov = viewCamera.fieldOfView;
        shotTime = 0f;
        ApplyBattleShot(0f);
        SetPhase(Phase.Entering, enteringStatus);
    }

    private void ApplyBattleShot(float time)
    {
        // Both legs evaluate the same curve, so the return retraces the shot exactly.
        float t = time <= 0f ? 0f : time >= 1f ? 1f : Mathf.Clamp01(ease.Evaluate(time));
        Vector3 position = Vector3.Lerp(returnPosition, cameraEnd.position, t)
            + gridOrigin.TransformVector(arcOffset) * (4f * t * (1f - t));
        Quaternion battleRotation = Quaternion.LookRotation(battleAim.position - cameraEnd.position, gridOrigin.up);
        viewCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(returnRotation, battleRotation, t));
        viewCamera.fieldOfView = Mathf.Lerp(returnFov, battleFov, t);
        // Bring in the foreground only once the camera has cleared its start at the player's feet.
        playerVisual.SetActive(t > 0f && t >= playerRevealAt);
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
