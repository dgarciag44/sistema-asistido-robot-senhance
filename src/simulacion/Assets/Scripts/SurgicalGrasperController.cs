using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SurgicalGrasperController : MonoBehaviour
{
    [Header("Pivotes de la pinza")]
    public Transform jawPivotLeft;
    public Transform jawPivotRight;

    [Header("Configuracion de apertura")]
    [Range(0f, 35f)] public float maxOpeningAngle = 25f;
    public float openingSpeed = 45f;
    [SerializeField] private float currentOpeningAngle = 0f;

    [Header("Sistema de agarre")]
    public Transform graspPoint;
    public float graspRadius = 0.06f;
    public float graspThreshold = 5f;

    [Header("Configuracion de brazo")]
    public bool isLeftArm = true;

    [Header("Consola quirurgica")]
    public SurgicalConsoleController surgicalConsole;

    [Header("Manipulacion de tejido")]
    public bool enableTissueGrasp = true;

    private Rigidbody grabbedObject;
    private AbdominalTissueMesh grabbedTissue;
    private RobotArmController robotController;

    private bool wasOpenCommand;
    private bool pendingGrab;
    private bool waitingForTriggerRelease;

    private static readonly Dictionary<Rigidbody, SurgicalGrasperController> Owners =
        new Dictionary<Rigidbody, SurgicalGrasperController>();

    private void Start()
    {
        robotController = GetComponentInParent<RobotArmController>();

        currentOpeningAngle = 0f;
        wasOpenCommand = false;
        pendingGrab = false;

        ApplyJawRotation();
    }

    private void Update()
    {
        if (robotController != null &&
            robotController.IsEmergencyStopped)
            return;

        if (surgicalConsole == null)
            return;

        bool controllingCamera = isLeftArm
            ? surgicalConsole.cameraSide ==
              SurgicalConsoleController.CameraControlSide.Left
            : surgicalConsole.cameraSide ==
              SurgicalConsoleController.CameraControlSide.Right;

        if (controllingCamera)
        {
            pendingGrab = false;
            wasOpenCommand = false;
            waitingForTriggerRelease = true;
            return;
        }

        bool openGripper = isLeftArm
            ? surgicalConsole.LeftGripperOpen
            : surgicalConsole.RightGripperOpen;

        if (isLeftArm &&
            Keyboard.current != null &&
            Keyboard.current.rKey.isPressed)
        {
            openGripper = true;
        }

        if (waitingForTriggerRelease)
        {
            if (openGripper)
                return;

            waitingForTriggerRelease = false;
        }

        bool closingCommand =
            wasOpenCommand && !openGripper;

        wasOpenCommand = openGripper;

        if (closingCommand)
            pendingGrab = true;

        if (openGripper)
            pendingGrab = false;

        float targetAngle =
            openGripper ? maxOpeningAngle : 0f;

        currentOpeningAngle = Mathf.MoveTowards(
            currentOpeningAngle,
            targetAngle,
            openingSpeed * Time.deltaTime
        );

        ApplyJawRotation();

        if (pendingGrab &&
            grabbedObject == null &&
            grabbedTissue == null &&
            currentOpeningAngle <= graspThreshold)
        {
            TryGrabObject();
            pendingGrab = false;
        }

        if (currentOpeningAngle > graspThreshold)
        {
            if (grabbedObject != null)
                ReleaseObject();

            if (grabbedTissue != null)
                ReleaseTissue();
        }
    }

    private void ApplyJawRotation()
    {
        if (jawPivotLeft != null)
        {
            jawPivotLeft.localRotation =
                Quaternion.Euler(
                    0f, -currentOpeningAngle, 0f
                );
        }

        if (jawPivotRight != null)
        {
            jawPivotRight.localRotation =
                Quaternion.Euler(
                    0f, currentOpeningAngle, 0f
                );
        }
    }

    private void TryGrabObject()
    {
        if (graspPoint == null)
            return;

        Collider[] nearbyObjects = Physics.OverlapSphere(
            graspPoint.position,
            graspRadius
        );

        Rigidbody nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        foreach (Collider col in nearbyObjects)
        {
            Rigidbody rb = col.attachedRigidbody;

            if (rb == null || !rb.CompareTag("Graspable"))
                continue;

            float sqrDistance =
                (col.ClosestPoint(graspPoint.position) -
                 graspPoint.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = rb;
            }
        }

        if (nearest != null)
        {
            GrabRigidObject(nearest);
            return;
        }

        if (enableTissueGrasp)
            TryGrabTissue(nearbyObjects);
    }

    private void GrabRigidObject(Rigidbody nearest)
    {
        if (Owners.TryGetValue(
                nearest,
                out SurgicalGrasperController previousOwner)
            && previousOwner != null &&
            previousOwner != this)
        {
            previousOwner.grabbedObject = null;
            previousOwner.pendingGrab = false;
        }

        Owners[nearest] = this;
        grabbedObject = nearest;

        grabbedObject.isKinematic = true;
        grabbedObject.useGravity = false;

        grabbedObject.transform.SetParent(
            graspPoint, true
        );

        Debug.Log(
            gameObject.name + " - OBJETO SUJETADO"
        );
    }

    private void TryGrabTissue(Collider[] nearbyObjects)
    {
        AbdominalTissueMesh nearestTissue = null;
        Vector3 nearestPoint = Vector3.zero;
        float nearestDistance = float.PositiveInfinity;

        foreach (Collider col in nearbyObjects)
        {
            AbdominalTissueMesh tissue =
                col.GetComponent<AbdominalTissueMesh>();

            if (tissue == null)
                continue;

            Vector3 contactPoint =
                col.ClosestPoint(graspPoint.position);

            float distance =
                (contactPoint - graspPoint.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTissue = tissue;
                nearestPoint = contactPoint;
            }
        }

        if (nearestTissue != null &&
            nearestTissue.TryGrab(
                nearestPoint, graspPoint
            ))
        {
            grabbedTissue = nearestTissue;

            Debug.Log(
                gameObject.name +
                " - PIEL ABDOMINAL SUJETADA"
            );
        }
    }

    private void ReleaseTissue()
    {
        if (grabbedTissue == null)
            return;

        grabbedTissue.ReleaseTissue(graspPoint);
        grabbedTissue = null;
    }

    private void ReleaseObject()
    {
        if (grabbedObject == null)
            return;

        Rigidbody released = grabbedObject;
        grabbedObject = null;

        if (!Owners.TryGetValue(
                released,
                out SurgicalGrasperController owner)
            || owner != this)
        {
            return;
        }

        Owners.Remove(released);

        released.transform.SetParent(null, true);
        released.isKinematic = false;
        released.useGravity = true;

        Debug.Log(
            gameObject.name + " - OBJETO LIBERADO"
        );
    }

    private void OnDestroy()
    {
        if (grabbedObject != null)
            ReleaseObject();

        if (grabbedTissue != null)
            ReleaseTissue();
    }

    private void OnDrawGizmosSelected()
    {
        if (graspPoint == null)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            graspPoint.position,
            graspRadius
        );
    }
}