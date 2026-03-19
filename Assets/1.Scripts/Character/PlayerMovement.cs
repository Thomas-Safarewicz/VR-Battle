using System.Collections.Generic;
using Fusion;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.XR;

namespace _1.Scripts.Character
{
    public class PlayerMovement : NetworkBehaviour
    {
        public static PlayerMovement Instance { get; private set; }

        public static PlayerMovement Local { get; protected set; }

        [Header("Colliders")]
        public SphereCollider headCollider;

        [Header("Hands")]
        public Transform rightHandTransform;

        public Transform leftHandTransform;

        [Header("Movement")]
        public int velocityHistorySize;

        public float maxArmLength = 1.5f;
        public float unStickDistance = 1f;

        public float moveMulti;
        public float jumpMulti;
        public float velocityThreshold;
        public float jumpThreshold;

        [Header("Collision")]
        public float minimumRaycastDistance = 0.05f;

        public float defaultSlideFactor = 0.03f;
        public float defaultPrecision = 0.995f;

        public LayerMask locomotionEnabledLayers;

        public bool disableMovement;

        private Vector3 lastLeftHandPosition;
        private Vector3 lastRightHandPosition;
        private Vector3 lastHeadPosition;

        private Rigidbody playerRigidBody;

        private Vector3[] velocityHistory;
        private int velocityIndex;
        private Vector3 currentVelocity;
        private Vector3 velocityAverage;
        private bool jumpHandIsLeft;
        private Vector3 lastPosition;

        private bool wasLeftHandTouching;
        private bool wasRightHandTouching;

        private InputDevice leftDevice;
        private InputDevice rightDevice;

        private bool leftTracked;
        private bool rightTracked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }

            InitializeValues();
        }

        private void InitializeValues()
        {
            velocityIndex = 0;
            playerRigidBody = GetComponent<Rigidbody>();
            velocityHistory = new Vector3[velocityHistorySize];

            lastLeftHandPosition = leftHandTransform.position;
            lastRightHandPosition = rightHandTransform.position;
            lastHeadPosition = headCollider.transform.position;
            lastPosition = transform.position;

            AcquireDevices();
        }

        private void AcquireDevices()
        {
            List<InputDevice> devices = new();

            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
            if (devices.Count > 0)
            {
                leftDevice = devices[0];
            }

            devices.Clear();

            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0)
            {
                rightDevice = devices[0];
            }
        }

        private Vector3 CurrentLeftHandPosition()
        {
            Vector3 headPos = headCollider.transform.position;
            Vector3 handPos = leftHandTransform.position;

            if ((handPos - headPos).magnitude < maxArmLength)
            {
                return handPos;
            }

            return headPos + (handPos - headPos).normalized * maxArmLength;
        }

        private Vector3 CurrentRightHandPosition()
        {
            Vector3 headPos = headCollider.transform.position;
            Vector3 handPos = rightHandTransform.position;

            if ((handPos - headPos).magnitude < maxArmLength)
            {
                return handPos;
            }

            return headPos + (handPos - headPos).normalized * maxArmLength;
        }

        private void Update()
        {
            UpdateTracking();

            Vector3 firstIterationLeftHand = Vector3.zero;
            Vector3 firstIterationRightHand = Vector3.zero;

            bool leftHandColliding = false;
            bool rightHandColliding = false;

            if (leftTracked)
            {
                ProcessHand(leftTracked, ref lastLeftHandPosition, wasLeftHandTouching,
                    out firstIterationLeftHand, out leftHandColliding, out RaycastHit leftHandHitInfo, CurrentLeftHandPosition);
            }

            if (rightTracked)
            {
                ProcessHand(rightTracked, ref lastRightHandPosition, wasRightHandTouching,
                    out firstIterationRightHand, out rightHandColliding, out RaycastHit rightHandHitInfo, CurrentRightHandPosition);
            }

            Vector3 rigidBodyMovement = ResolveHandMovement(
                firstIterationLeftHand,
                firstIterationRightHand,
                leftHandColliding,
                rightHandColliding
            );

            ApplyHeadCollision(ref rigidBodyMovement);

            if (rigidBodyMovement != Vector3.zero)
                transform.position += rigidBodyMovement;

            lastHeadPosition = headCollider.transform.position;

            if (leftTracked)
            {
                ResolveFinalHandPosition(ref lastLeftHandPosition, ref leftHandColliding, CurrentLeftHandPosition);
            }

            if (rightTracked)
            {
                ResolveFinalHandPosition(ref lastRightHandPosition, ref rightHandColliding, CurrentRightHandPosition);
            }

            StoreVelocities();
            ApplyVelocity(leftHandColliding, rightHandColliding);

            if (leftTracked)
            {
                HandleUnstick(ref lastLeftHandPosition, ref leftHandColliding, CurrentLeftHandPosition);
            }

            if (rightTracked)
            {
                HandleUnstick(ref lastRightHandPosition, ref rightHandColliding, CurrentRightHandPosition);
            }

            wasLeftHandTouching = leftTracked && leftHandColliding;
            wasRightHandTouching = rightTracked && rightHandColliding;
        }

        private void UpdateTracking()
        {
            if (!leftDevice.isValid || !rightDevice.isValid)
                AcquireDevices();

            leftDevice.TryGetFeatureValue(CommonUsages.isTracked, out leftTracked);
            rightDevice.TryGetFeatureValue(CommonUsages.isTracked, out rightTracked);
        }

        public void DisableMovement(bool disable)
        {
            disableMovement = disable;
            playerRigidBody.isKinematic = disable;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            playerRigidBody.position = position;
            playerRigidBody.rotation = rotation;
            transform.position = position;
            transform.rotation = rotation;
            lastPosition = position;
        }

        private bool ProcessHand(bool isTracked, ref Vector3 lastHandPosition, bool wasTouching, out Vector3 firstIteration, out bool isColliding,
            out RaycastHit hitInfo, System.Func<Vector3> currentHandPositionFunc)
        {
            firstIteration = Vector3.zero;
            isColliding = false;
            hitInfo = default;

            if (!isTracked)
            {
                return false;
            }

            Vector3 currentHandPosition = currentHandPositionFunc();

            Vector3 distanceTraveled =
                currentHandPosition - lastHandPosition +
                Vector3.down * (2f * 9.8f * Time.deltaTime * Time.deltaTime);

            if (IterativeCollisionSphereCast(lastHandPosition, minimumRaycastDistance, distanceTraveled,
                    defaultPrecision, out Vector3 finalPosition, out hitInfo, true))
            {
                if (wasTouching)
                {
                    firstIteration = lastHandPosition - currentHandPosition;
                }
                else
                {
                    firstIteration = finalPosition - currentHandPosition;
                }

                playerRigidBody.velocity = Vector3.zero;

                isColliding = true;
                return true;
            }

            return false;
        }

        private Vector3 ResolveHandMovement(
            Vector3 leftIteration,
            Vector3 rightIteration,
            bool leftColliding,
            bool rightColliding)
        {
            if ((leftColliding || wasLeftHandTouching) && (rightColliding || wasRightHandTouching))
            {
                return (leftIteration + rightIteration) / 2f;
            }

            return leftIteration + rightIteration;
        }

        public void ApplyRecoil(Vector3 direction, float force)
        {
            if (disableMovement)
            {
                return;
            }

            playerRigidBody.velocity += direction * force;
        }

        private void ApplyHeadCollision(ref Vector3 rigidBodyMovement)
        {
            if (!IterativeCollisionSphereCast(lastHeadPosition, headCollider.radius,
                    headCollider.transform.position + rigidBodyMovement - lastHeadPosition, defaultPrecision,
                    out Vector3 finalPosition, out RaycastHit _, false))
            {
                return;
            }

            rigidBodyMovement = finalPosition - lastHeadPosition;

            if (Physics.Raycast(lastHeadPosition, headCollider.transform.position - lastHeadPosition + rigidBodyMovement,
                    out RaycastHit _, (headCollider.transform.position - lastHeadPosition + rigidBodyMovement).magnitude +
                                      headCollider.radius * defaultPrecision * 0.999f, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
            {
                rigidBodyMovement = lastHeadPosition - headCollider.transform.position;
            }
        }

        private void ResolveFinalHandPosition(ref Vector3 lastHandPosition, ref bool handColliding, System.Func<Vector3> currentHandPositionFunc)
        {
            Vector3 distanceTraveled = currentHandPositionFunc() - lastHandPosition;

            if (IterativeCollisionSphereCast(lastHandPosition, minimumRaycastDistance, distanceTraveled,
                    defaultPrecision, out Vector3 finalPosition, out RaycastHit _, true))
            {
                lastHandPosition = finalPosition;
                handColliding = true;
            }
            else
            {
                lastHandPosition = currentHandPositionFunc();
            }
        }

        private void ApplyVelocity(bool leftHandColliding, bool rightHandColliding)
        {
            if ((!rightHandColliding && !leftHandColliding) || disableMovement)
            {
                return;
            }

            if (!(velocityAverage.magnitude > velocityThreshold))
            {
                return;
            }

            if (velocityAverage.magnitude * moveMulti > jumpThreshold)
                playerRigidBody.velocity = velocityAverage.normalized * jumpMulti;
            else
                playerRigidBody.velocity = moveMulti * velocityAverage;
        }

        private void HandleUnstick(
            ref Vector3 lastHandPosition,
            ref bool handColliding,
            System.Func<Vector3> currentHandPositionFunc)
        {
            Vector3 current = currentHandPositionFunc();

            if (handColliding && (current - lastHandPosition).magnitude > unStickDistance &&
                !Physics.SphereCast(headCollider.transform.position, minimumRaycastDistance * defaultPrecision,
                    current - headCollider.transform.position, out RaycastHit _,
                    (current - headCollider.transform.position).magnitude - minimumRaycastDistance,
                    locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
            {
                lastHandPosition = current;
                handColliding = false;
            }
        }

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision,
            out Vector3 endPosition, out RaycastHit hitInfo, bool singleHand)
        {
            // first spherecast from the starting position to the final position
            if (CollisionsSphereCast(
                    startPosition,
                    sphereRadius * precision,
                    movementVector,
                    precision,
                    out endPosition,
                    out hitInfo))
            {
                // if we hit a surface, do a bit of a slide

                // take the surface normal that we hit, then along that plane, do a spherecast
                Vector3 firstPosition = endPosition;
                Surface gorillaSurface = hitInfo.collider.GetComponent<Surface>();
                float slipPercentage = gorillaSurface ? gorillaSurface.slipPercentage : (!singleHand ? defaultSlideFactor : 0.001f);
                Vector3 movementToProjectedAboveCollisionPlane =
                    Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, hitInfo.normal) * slipPercentage;

                if (CollisionsSphereCast(
                        endPosition,
                        sphereRadius,
                        movementToProjectedAboveCollisionPlane,
                        precision * precision,
                        out endPosition,
                        out hitInfo))
                {
                    // if we hit trying to move perpendicularly, stop there and our end position is the final spot we hit
                    return true;
                }

                // if not, try to move closer towards the true point
                if (CollisionsSphereCast(
                        movementToProjectedAboveCollisionPlane + firstPosition,
                        sphereRadius,
                        startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition),
                        precision * precision * precision,
                        out endPosition,
                        out hitInfo))
                {
                    // if we hit, then return the spot we hit
                    return true;
                }

                // fallback: just use the first hit position
                endPosition = firstPosition;
                return true;
            }

            // sanity check: smaller spherecast
            if (CollisionsSphereCast(
                    startPosition,
                    sphereRadius * precision * 0.66f,
                    movementVector.normalized * (movementVector.magnitude + sphereRadius * precision * 0.34f),
                    precision * 0.66f,
                    out endPosition,
                    out hitInfo))
            {
                endPosition = startPosition;
                return true;
            }

            endPosition = Vector3.zero;
            return false;
        }

        private bool CollisionsSphereCast(
            Vector3 startPosition,
            float sphereRadius,
            Vector3 movementVector,
            float precision,
            out Vector3 finalPosition,
            out RaycastHit hitInfo)
        {
            // initial spherecast
            if (Physics.SphereCast(
                    startPosition,
                    sphereRadius * precision,
                    movementVector,
                    out hitInfo,
                    movementVector.magnitude + sphereRadius * (1 - precision),
                    locomotionEnabledLayers.value,
                    QueryTriggerInteraction.Ignore))
            {
                // if we hit, we're trying to move to a position a sphereradius distance from the normal
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                // check a spherecast from the original position to the intended final position
                if (Physics.SphereCast(
                        startPosition,
                        sphereRadius * precision * precision,
                        finalPosition - startPosition,
                        out RaycastHit innerHit,
                        (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision),
                        locomotionEnabledLayers.value,
                        QueryTriggerInteraction.Ignore))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized *
                        Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                // bonus raycast check to make sure that something odd didn't happen with the spherecast
                else if (Physics.Raycast(
                             startPosition,
                             finalPosition - startPosition,
                             out innerHit,
                             (finalPosition - startPosition).magnitude +
                             sphereRadius * precision * precision * 0.999f,
                             locomotionEnabledLayers.value,
                             QueryTriggerInteraction.Ignore))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                }

                return true;
            }

            // anti-clipping through geometry check
            if (Physics.Raycast(
                    startPosition,
                    movementVector,
                    out hitInfo,
                    movementVector.magnitude + sphereRadius * precision * 0.999f,
                    locomotionEnabledLayers.value,
                    QueryTriggerInteraction.Ignore))
            {
                finalPosition = startPosition;
                return true;
            }

            finalPosition = Vector3.zero;
            return false;
        }

        private void StoreVelocities()
        {
            velocityIndex = (velocityIndex + 1) % velocityHistorySize;
            Vector3 oldestVelocity = velocityHistory[velocityIndex];
            currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
            velocityAverage += (currentVelocity - oldestVelocity) / velocityHistorySize;
            velocityHistory[velocityIndex] = currentVelocity;
            lastPosition = transform.position;
        }
    }
}