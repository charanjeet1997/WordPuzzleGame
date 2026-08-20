using UnityEngine;
using WordPuzzle.Services;

namespace WordPuzzle.Gameplay
{
    /// <summary>
    /// Scales the background sprite to cover the camera view, whatever shape the screen is.
    ///
    /// The artwork is authored portrait, so on a landscape window a fixed scale leaves the
    /// sides empty - the dark bars either side of the menu. Covering means scaling by the
    /// larger of the two ratios and letting the excess run off the other axis, which is what
    /// a background is for: it should never be the thing that runs out.
    /// </summary>
    // ExecuteAlways so the cover is visible while editing and when the Game view aspect is
    // changed, rather than only after entering play mode.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundFitter : MonoBehaviour
    {
        [Tooltip("Camera to cover. Falls back to Camera.main.")]
        public Camera viewCamera;

        [Tooltip("Extra scale beyond an exact fit, so a rounding error never shows a seam at the edge.")]
        [Range(1f, 1.2f)]
        public float overscan = 1.02f;

        private SpriteRenderer _renderer;

        // Camera aspect and size rather than Screen dimensions: in the editor the Game view
        // aspect can change without Screen reporting a new size, and that was the case where
        // the background stayed portrait-sized on a landscape view.
        private float _lastAspect = -1f;
        private float _lastOrthoSize = -1f;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            LayoutService.LayoutChanged += OnLayoutChanged;
            Fit();
        }

        private void OnDisable()
        {
            LayoutService.LayoutChanged -= OnLayoutChanged;
        }

        private void OnLayoutChanged(ScreenLayout layout) => Fit();

        private void Update()
        {
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            // Refits on any shape change, including a plain resize that does not flip
            // orientation - dragging a desktop browser window wider is exactly that.
            if (Mathf.Approximately(cam.aspect, _lastAspect)
                && Mathf.Approximately(cam.orthographicSize, _lastOrthoSize)) return;

            Fit();
        }

        private void Fit()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _renderer.sprite == null) return;

            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null || !cam.orthographic) return;

            _lastAspect = cam.aspect;
            _lastOrthoSize = cam.orthographicSize;

            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * cam.aspect;

            Vector2 spriteSize = _renderer.sprite.bounds.size;
            if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;

            // Cover, not fit: the larger ratio wins so neither axis can leave a gap.
            float scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y) * overscan;
            transform.localScale = new Vector3(scale, scale, 1f);

            // Centred by the renderer's own bounds rather than by the transform: a sprite
            // whose pivot is not centred would otherwise sit off to one side, which reads as
            // "the background vanished" on a wide screen.
            Vector3 camPos = cam.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);

            Vector3 offset = transform.position - _renderer.bounds.center;
            transform.position += new Vector3(offset.x, offset.y, 0f);
        }
    }
}
