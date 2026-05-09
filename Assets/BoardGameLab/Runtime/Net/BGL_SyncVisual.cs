using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

namespace BoardGameLab.Runtime.Net
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BGL_SyncVisual : UdonSharpBehaviour
    {
        public BGL_SyncManager Manager;
        public Transform PulseCore;
        public MeshRenderer CoreRenderer;
        public TextMeshPro ScoreText;
        public TextMeshPro MilestoneText;
        
        [Header("Bug Window")]
        public GameObject BugWindow;
        public TextMeshPro BugReportText;

        [Header("Colors")]
        public Color ColorNormal = Color.cyan;
        public Color ColorMilestone = Color.yellow;
        public Color ColorVictory = Color.green;
        public Color ColorError = Color.red;

        private Vector3 _baseScale;
        private float _lastPulseTime;
        private int _lastSeenScore;
        private int _desyncCount;

        void Start()
        {
            if (PulseCore != null) _baseScale = PulseCore.localScale;
            UpdateDisplay(0);
            if (BugWindow != null) BugWindow.SetActive(false);
        }

        public void _OnScoreChanged(int score)
        {
            // Detect Desync / Gap
            if (score < _lastSeenScore)
            {
                _ReportBug("OWNERSHIP ROLLBACK DETECTED");
            }
            else if (score > _lastSeenScore + 1 && _lastSeenScore != 0)
            {
                _ReportBug("SYNC GAP DETECTED (MISSING PACKETS)");
            }
            
            _lastSeenScore = score;
            UpdateDisplay(score);
        }

        public void _OnPulseEffect()
        {
            _lastPulseTime = Time.time;
        }

        public void _ReportBug(string message)
        {
            _desyncCount++;
            if (BugWindow != null) BugWindow.SetActive(true);
            if (BugReportText != null)
            {
                BugReportText.text = $"[ !!! BUG DETECTED !!! ]\n{message}\nTOTAL ERRORS: {_desyncCount}\n\nCLICK TO DISMISS";
            }
            if (CoreRenderer != null) CoreRenderer.material.SetColor("_EmissionColor", ColorError);
        }

        public void _DismissBug()
        {
            if (BugWindow != null) BugWindow.SetActive(false);
            UpdateDisplay(_lastSeenScore);
        }

        void Update()
        {
            if (PulseCore == null) return;
            float timeSincePulse = Time.time - _lastPulseTime;
            float pulseScale = Mathf.Exp(-timeSincePulse * 5f) * 0.5f;
            PulseCore.Rotate(Vector3.up, 20f * Time.deltaTime);
            PulseCore.localScale = _baseScale * (1f + pulseScale + (Manager != null ? (Manager.Score % 10) * 0.05f : 0));
        }

        private void UpdateDisplay(int score)
        {
            if (ScoreText != null) ScoreText.text = $"COLLECTIVE PULSE: {score}";
            if (MilestoneText != null)
            {
                if (score >= 100) { MilestoneText.text = "GOAL REACHED!"; MilestoneText.color = ColorVictory; }
                else if (score > 0 && score % 10 == 0) { MilestoneText.text = "MILESTONE!"; MilestoneText.color = ColorMilestone; }
                else { MilestoneText.text = "CLICK TO SYNC"; MilestoneText.color = Color.white; }
            }

            if (CoreRenderer != null)
            {
                CoreRenderer.material.SetColor("_EmissionColor", score >= 100 ? ColorVictory : (score % 10 == 0 ? ColorMilestone : ColorNormal));
            }
        }
    }
}
