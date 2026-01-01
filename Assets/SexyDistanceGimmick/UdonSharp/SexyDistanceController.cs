using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SexyDistanceController : UdonSharpBehaviour
{
    [Header("References")]
    [SerializeField] Transform ghostRoot;
    [SerializeField] Transform[] touchPoints;
    [SerializeField] AudioSource whisperSource;
    [SerializeField] AudioSource touchSource;
    [SerializeField] AudioClip[] whisperClips;
    [SerializeField] AudioClip touchClip;
    [SerializeField] Animator animator;

    [Header("Distance")]
    [SerializeField] float triggerDistance = 1.5f;
    [SerializeField] float minDistance = 0.35f;
    [SerializeField] float moveSpeed = 0.18f;

    [Header("Touch")]
    [SerializeField] float touchRadius = 0.09f;
    [SerializeField] float touchCooldown = 0.35f;

    [Header("Audio")]
    [SerializeField] float whisperMaxVolume = 0.22f;
    [SerializeField] float whisperHoldDuration = 2.0f;

    [Header("Lean")]
    [SerializeField] float[] leanAmounts = { 0.33f, 0.50f, 0.65f, 0.70f, 0.70f, 0.45f, 0.40f };

    bool isActive;
    float touchTimer;
    float whisperTimer;
    float currentLean;
    float sqrTouchRadius;
    VRCPlayerApi player;

    void Start()
    {
        player = Networking.LocalPlayer;
        sqrTouchRadius = touchRadius * touchRadius;
        currentLean = leanAmounts[0];
    }

    void Update()
    {
        Vector3 headPos = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        Vector3 toHead = headPos - ghostRoot.position;
        float dist = toHead.magnitude;

        isActive = isActive || dist < triggerDistance;
        animator.SetBool("Active", isActive);

        if (!isActive) return;

        float move = Mathf.Min(moveSpeed * Time.deltaTime, dist - minDistance);
        if (move > 0) ghostRoot.position += toHead / dist * move;

        UpdateWhisper(dist);
        UpdateTouch();
        UpdateAnimator();
    }

    void UpdateWhisper(float dist)
    {
        float baseVolume = Mathf.Clamp01(1f - (dist - minDistance) / (triggerDistance - minDistance)) * whisperMaxVolume;
        whisperTimer = Mathf.Max(0, whisperTimer - Time.deltaTime);
        whisperSource.volume = whisperTimer > 0 ? whisperMaxVolume : baseVolume;
    }

    void UpdateTouch()
    {
        touchTimer = Mathf.Max(0, touchTimer - Time.deltaTime);
        if (touchTimer > 0) return;

        Vector3 left = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
        Vector3 right = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;

        for (int i = 0; i < touchPoints.Length; i++)
        {
            Vector3 p = touchPoints[i].position;
            if ((left - p).sqrMagnitude < sqrTouchRadius || (right - p).sqrMagnitude < sqrTouchRadius)
            {
                OnTouch(i);
                return;
            }
        }

        currentLean = leanAmounts[0];
    }

    void OnTouch(int index)
    {
        touchTimer = touchCooldown;
        whisperTimer = whisperHoldDuration;
        currentLean = leanAmounts[index + 1];

        touchSource.PlayOneShot(touchClip);

        if (index < whisperClips.Length && whisperClips[index] != null)
        {
            whisperSource.clip = whisperClips[index];
            whisperSource.Play();
        }

        if (player.IsUserInVR())
        {
            player.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.3f, 0.5f, 0.2f);
            player.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.3f, 0.5f, 0.2f);
        }
    }

    void UpdateAnimator()
    {
        animator.SetFloat("LeanAmount", currentLean);
    }
}
