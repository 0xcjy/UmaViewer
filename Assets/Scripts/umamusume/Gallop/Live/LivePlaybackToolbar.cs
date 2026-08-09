using UnityEngine;

namespace Gallop.Live
{
    /// <summary>Always-visible live debugging controls independent of the scene's auto-hidden UI.</summary>
    public sealed class LivePlaybackToolbar : MonoBehaviour
    {
        private bool _draggingTimeline;

        private void Update()
        {
            if (Director.instance == null || !Director.instance._isLiveSetup)
                return;

            if (Input.GetKeyDown(KeyCode.Space))
                Director.instance.SetPlaybackPaused(!Director.instance.PlaybackPaused);
            if (Input.GetKeyDown(KeyCode.F6))
                Director.instance.SetFreeCameraEnabled(!Director.instance.FreeCameraActive);
        }

        private void OnGUI()
        {
            Director director = Director.instance;
            if (director == null || !director._isLiveSetup)
                return;

            // Keep this overlay above the scene's Canvas/UI, which may cover the lower edge.
            int oldDepth = GUI.depth;
            GUI.depth = 1000;
            GUILayout.BeginArea(new Rect(16, Mathf.Max(8, Screen.height - 68), 560, 52), GUI.skin.window);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(director.PlaybackPaused ? "Play" : "Pause", GUILayout.Width(62)))
                director.SetPlaybackPaused(!director.PlaybackPaused);

            bool freeCamera = GUILayout.Toggle(director.FreeCameraActive, "Free Camera", GUILayout.Width(105));
            if (freeCamera != director.FreeCameraActive)
                director.SetFreeCameraEnabled(freeCamera);

            float time = GUILayout.HorizontalSlider(director._liveCurrentTime, 0f, Mathf.Max(0.01f, director.totalTime), GUILayout.Width(245));
            Rect sliderRect = GUILayoutUtility.GetLastRect();
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && sliderRect.Contains(currentEvent.mousePosition))
                _draggingTimeline = true;
            if (_draggingTimeline && (currentEvent.type == EventType.MouseDrag || currentEvent.type == EventType.MouseDown))
                director.SeekPlayback(time);
            if (currentEvent.type == EventType.MouseUp)
                _draggingTimeline = false;

            GUILayout.Label(director._liveCurrentTime.ToString("0.00") + " / " + director.totalTime.ToString("0.00"), GUILayout.Width(84));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.depth = oldDepth;
        }
    }
}
