using System.Collections.Generic;
using Fusion.XR.Shared;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Rig;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _1.Scripts.Character
{
    public class RayBeamerUI : MonoBehaviour, IRayDescriptor
    {
        public HardwareHand hand;

        public bool useRayActionInput = true;
#if ENABLE_INPUT_SYSTEM
        public InputActionProperty rayAction;
#endif

        public Transform origin;
        public LayerMask targetLayerMask = ~0;
        public float maxDistance = 100f;

        [Header("Representation")]
        public LineRenderer lineRenderer;

        public float width = 0.02f;
        public Material lineMaterial;

        public Color hitColor = Color.green;
        public Color noHitColor = Color.red;

        public UnityEvent<Collider, Vector3> onHitEnter = new();
        public UnityEvent<Collider, Vector3> onHitExit = new();
        public UnityEvent<Collider, Vector3> onRelease = new();

        public bool isRayEnabled = false;

        public enum Status
        {
            NoBeam,
            BeamNoHit,
            BeamHit
        }

        public Status status = Status.NoBeam;


        [HideInInspector]
        public Vector3 lastHit;

        private Collider lastHitCollider;

        public RayData Ray => ray;
        private RayData ray;

        // UI
        private EventSystem eventSystem;
        private PointerEventData pointerData;
        private GraphicRaycaster[] raycasters;

        private GameObject currentUIObject;
        private GameObject lastUIObject;

        private bool lastRayEnabled;

        public void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.material = lineMaterial;
                lineRenderer.numCapVertices = 4;
            }

            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;

            if (origin == null) origin = transform;
            if (hand == null) hand = GetComponentInParent<HardwareHand>();
        }

        public void Start()
        {
#if ENABLE_INPUT_SYSTEM
            rayAction.EnableWithDefaultXRBindings(hand.side, new List<string> { "thumbstickClicked", "primaryButton", "secondaryButton" });
#endif

            eventSystem = EventSystem.current;
            pointerData = new PointerEventData(eventSystem);
        }

        private bool UIRaycast(out RaycastResult result)
        {
            raycasters = FindObjectsOfType<GraphicRaycaster>();
            result = new RaycastResult();

            if (raycasters == null || raycasters.Length == 0)
                return false;

            Ray newRay = new(origin.position, origin.forward);

            foreach (GraphicRaycaster raycaster in raycasters)
            {
                Canvas canvas = raycaster.GetComponent<Canvas>();
                if (!canvas)
                    continue;

                Plane plane = new(canvas.transform.forward * -1, canvas.transform.position);

                if (!plane.Raycast(newRay, out float enter))
                    continue;

                Vector3 hitPoint = newRay.GetPoint(enter);

                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                    canvas.worldCamera ?? Camera.main,
                    hitPoint
                );

                pointerData.position = screenPoint;

                List<RaycastResult> results = new();
                raycaster.Raycast(pointerData, results);

                if (results.Count <= 0)
                    continue;

                result = results[0];
                result.worldPosition = hitPoint;
                return true;
            }

            return false;
        }

        private bool BeamCast(out RaycastHit hitInfo)
        {
            Ray handRay = new(origin.position, origin.forward);
            return Physics.Raycast(handRay, out hitInfo, maxDistance, targetLayerMask);
        }

        public void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (useRayActionInput && rayAction is { action: not null })
            {
                isRayEnabled = Mathf.Approximately(rayAction.action.ReadValue<float>(), 1);
            }
#endif

            ray.isRayEnabled = isRayEnabled;

            currentUIObject = null;

            if (ray.isRayEnabled)
            {
                ray.origin = origin.position;

                bool hitUI = UIRaycast(out RaycastResult uiHit);
                bool hit3D = BeamCast(out RaycastHit physicsHit);

                if (hitUI)
                {
                    ray.target = uiHit.worldPosition;
                    ray.color = hitColor;

                    lastHit = uiHit.worldPosition;

                    currentUIObject = uiHit.gameObject;

                    status = Status.BeamHit;
                }
                else if (hit3D)
                {
                    if (status == Status.BeamHit)
                    {
                        if (lastHitCollider != physicsHit.collider)
                        {
                            OnHitExit(lastHitCollider, lastHit);
                            OnHitEnter(physicsHit.collider, lastHit);
                        }
                    }
                    else
                    {
                        OnHitEnter(physicsHit.collider, lastHit);
                    }

                    lastHitCollider = physicsHit.collider;

                    ray.target = physicsHit.point;
                    ray.color = hitColor;

                    lastHit = physicsHit.point;

                    status = Status.BeamHit;
                }
                else
                {
                    if (status == Status.BeamHit && lastHitCollider)
                        OnHitExit(lastHitCollider, lastHit);

                    lastHitCollider = null;

                    ray.target = ray.origin + origin.forward * maxDistance;
                    ray.color = noHitColor;

                    status = Status.BeamNoHit;
                }
            }
            else
            {
                if (status == Status.BeamHit && lastHitCollider)
                    OnHitRelease(lastHitCollider, lastHit);

                status = Status.NoBeam;
                lastHitCollider = null;
            }

            // Hover enter / exit
            if (currentUIObject != lastUIObject)
            {
                if (lastUIObject)
                    ExecuteEvents.Execute(lastUIObject, pointerData, ExecuteEvents.pointerExitHandler);

                if (currentUIObject)
                    ExecuteEvents.Execute(currentUIObject, pointerData, ExecuteEvents.pointerEnterHandler);
            }

            // Trigger release → UI click
            if (lastRayEnabled && !isRayEnabled)
            {
                if (lastUIObject != null)
                {
                    ExecuteEvents.Execute(lastUIObject, pointerData, ExecuteEvents.pointerClickHandler);
                }
            }

            lastRayEnabled = isRayEnabled;
            lastUIObject = currentUIObject;

            UpdateRay();
        }

        void OnHitEnter(Collider hitCollider, Vector3 hitPosition)
        {
            onHitEnter?.Invoke(hitCollider, hitPosition);
        }

        void OnHitExit(Collider lastHitCollider, Vector3 lastHitPosition)
        {
            onHitExit?.Invoke(lastHitCollider, lastHitPosition);
        }

        void OnHitRelease(Collider lastHitCollider, Vector3 lastHitPosition)
        {
            onRelease?.Invoke(lastHitCollider, lastHitPosition);
        }

        void UpdateRay()
        {
            lineRenderer.enabled = ray.isRayEnabled;

            if (ray.isRayEnabled)
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPositions(new[] { ray.origin, ray.target });

                lineRenderer.startColor = ray.color;
                lineRenderer.endColor = ray.color;
                lineMaterial.color = ray.color;
            }
        }
    }
}