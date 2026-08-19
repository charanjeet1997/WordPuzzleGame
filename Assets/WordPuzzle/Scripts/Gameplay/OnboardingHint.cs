using System.Collections.Generic;
using UnityEngine;

namespace WordPuzzle.Gameplay
{
    /// <summary>
    /// Traces the swipe path of a word across the letter wheel with a moving marker, so a first
    /// time player learns the one mechanic that is not self-evident: that letters are dragged
    /// through, not tapped.
    ///
    /// World space rather than UI: the wheel is made of world-space nodes whose positions shift
    /// with letter count and screen fit, so a screen-space overlay would have to re-project them
    /// every frame and would drift on any aspect ratio it was not authored for.
    /// </summary>
    public class OnboardingHint : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("Marker that travels the path. Created at runtime when left empty.")]
        public SpriteRenderer marker;
        public Sprite markerSprite;
        [Tooltip("Marker diameter as a multiple of one letter's size, so it scales with the wheel.")]
        public float markerSizeRatio = 0.95f;
        public Color markerColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("Trail")]
        public LineRenderer trail;
        [Tooltip("Trail thickness as a multiple of one letter's size.")]
        public float trailWidthRatio = 0.16f;
        public Color trailColor = new Color(0.96f, 0.87f, 0.6f, 0.55f);

        [Header("Timing")]
        [Tooltip("Seconds to travel between two letters.")]
        public float secondsPerStep = 0.42f;

        [Tooltip("Pause at the end before the demonstration repeats.")]
        public float loopPause = 0.7f;

        private readonly List<Vector3> _path = new List<Vector3>();
        private float _time;
        private bool _playing;

        private void Awake()
        {
            EnsureVisuals();
            SetVisible(false);
        }

        private void EnsureVisuals()
        {
            if (marker == null)
            {
                var markerObj = new GameObject("HintMarker");
                markerObj.transform.SetParent(transform, false);
                marker = markerObj.AddComponent<SpriteRenderer>();
            }

            if (markerSprite == null) markerSprite = Resources.Load<Sprite>("Sprites/hint_finger");
            marker.sprite = markerSprite;
            marker.color = markerColor;

            // Above the letter nodes, or the marker disappears behind the tile it is pointing at.
            marker.sortingOrder = 100;


            if (trail == null)
            {
                var trailObj = new GameObject("HintTrail");
                trailObj.transform.SetParent(transform, false);
                trail = trailObj.AddComponent<LineRenderer>();
            }

            trail.useWorldSpace = true;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = trailColor;
            trail.endColor = trailColor;
            trail.startWidth = trailWidthRatio;
            trail.endWidth = trailWidthRatio;
            trail.numCapVertices = 4;
            trail.sortingOrder = 99;
        }

        /// <summary>
        /// Sizes the marker and trail against one letter, so the hint holds its proportions
        /// whatever the screen did to the wheel. A fixed world size looked right on the phone
        /// it was authored on and swamped the letters on a narrow tablet, where the ring
        /// shrinks to fit.
        /// </summary>
        public void SetScaleReference(float letterSize)
        {
            if (letterSize <= 0.0001f) return;

            if (marker != null && markerSprite != null && markerSprite.bounds.size.x > 0f)
            {
                float scale = (letterSize * markerSizeRatio) / markerSprite.bounds.size.x;
                marker.transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (trail != null)
            {
                float width = letterSize * trailWidthRatio;
                trail.startWidth = width;
                trail.endWidth = width;
            }
        }

        /// <summary>Starts demonstrating the given path. Fewer than two points does nothing.</summary>
        public void Play(List<Vector3> path)
        {
            _path.Clear();
            if (path != null) _path.AddRange(path);

            if (_path.Count < 2)
            {
                Stop();
                return;
            }

            _time = 0f;
            _playing = true;
            SetVisible(true);
        }

        public void Stop()
        {
            _playing = false;
            SetVisible(false);
        }

        public bool IsPlaying => _playing;

        private void SetVisible(bool visible)
        {
            if (marker != null) marker.enabled = visible;
            if (trail != null) trail.enabled = visible;
        }

        private void Update()
        {
            if (!_playing || _path.Count < 2) return;

            float travel = (_path.Count - 1) * secondsPerStep;
            float total = travel + loopPause;

            // Unscaled: the hint should keep animating even if something has paused the game.
            _time += Time.unscaledDeltaTime;
            if (_time > total) _time = 0f;

            float t = Mathf.Clamp01(_time / travel);
            float exact = t * (_path.Count - 1);
            int segment = Mathf.Min(Mathf.FloorToInt(exact), _path.Count - 2);
            float within = exact - segment;

            Vector3 position = Vector3.Lerp(_path[segment], _path[segment + 1], within);
            marker.transform.position = position;

            // The trail draws only the part already travelled, so the stroke appears to be
            // drawn rather than sitting there fully formed.
            int points = segment + 2;
            trail.positionCount = points;
            for (int i = 0; i <= segment; i++) trail.SetPosition(i, _path[i]);
            trail.SetPosition(points - 1, position);

            // Fade the whole thing out during the pause so the loop restart is not a hard cut.
            float alpha = _time > travel
                ? Mathf.InverseLerp(total, travel, _time)
                : 1f;

            marker.color = new Color(markerColor.r, markerColor.g, markerColor.b, markerColor.a * alpha);
            Color fadedTrail = new Color(trailColor.r, trailColor.g, trailColor.b, trailColor.a * alpha);
            trail.startColor = fadedTrail;
            trail.endColor = fadedTrail;
        }
    }
}
