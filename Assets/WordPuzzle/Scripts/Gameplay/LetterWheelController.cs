using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using ServiceLocatorFramework;
using WordPuzzle.Factory;
using WordPuzzle.Models;
using WordPuzzle.Services;
using WordPuzzle.Audio;
using WordPuzzle.Feedback;

namespace WordPuzzle.Gameplay
{
    [RequireComponent(typeof(LineRenderer))]
    public class LetterWheelController : MonoBehaviour
    {
        [Header("Wheel Settings")]
        [Tooltip("Used as the radius when Auto Wheel Radius is off, and as the upper limit when it is on.")]
        public float wheelRadius = 1.015f;

        [Tooltip("Derive the radius from the letter count so the gap between letters stays constant. " +
                 "A fixed radius spreads 4 letters far apart and crowds 7.")]
        public bool autoWheelRadius = true;

        [Tooltip("Centre-to-centre distance between neighbouring letters, as a multiple of node size. " +
                 "1.0 means the circles touch; raise it to open the ring up.")]
        public float nodeSpacingRatio = 1.55f;

        public float selectionRadius = 0.28f;
        public float nodeSize = 0.44f;

        [Tooltip("Fraction of the screen width the wheel may span in portrait, where it has " +
                 "the full width to itself.")]
        [Range(0.3f, 1f)]
        public float wheelWidthFraction = 0.86f;

        [Tooltip("Fraction of the screen width the wheel may span in landscape, where it shares " +
                 "the screen with the grid.")]
        [Range(0.2f, 0.8f)]
        public float landscapeWidthFraction = 0.4f;

        [Header("Placement")]
        [Tooltip("Viewport point the ring centres on in portrait: bottom centre, under the grid.")]
        public Vector2 portraitAnchor = new Vector2(0.5f, 0.22f);

        [Tooltip("Viewport point in landscape: left of centre, with the grid on the right.")]
        public Vector2 landscapeAnchor = new Vector2(0.22f, 0.45f);

        [Tooltip("Never shrink nodes below this, even if the ring then overflows.")]
        public float minNodeSize = 0.2f;

        [Tooltip("Largest a letter may grow to when there is room to spare.")]
        public float maxNodeSize = 0.85f;

        [Tooltip("Letter size in node-local units. Lower values leave more margin inside the circle.")]
        public float letterFontSize = 2.1f;

        [Tooltip("Optional. Leave empty to use the TextMeshPro default font.")]
        public TMP_FontAsset letterFont;

        [Header("Backdrop")]
        [Tooltip("The reference design places the letters straight onto the artwork with no disc behind them.")]
        public bool showBackdrop = false;
        public SpriteRenderer backdropRenderer;
        public float backdropPadding = 0.1f;

        [Header("LineRenderer Settings")]
        public LineRenderer lineRenderer;
        public Color lineColor = new Color(0.965f, 0.867f, 0.604f, 0.85f);
        public float lineWidth = 0.058f;

        public event Action<string> OnWordSubmitted;

        /// <summary>
        /// Raised after the ring is rebuilt - a new level or a shuffle. Anything holding node
        /// positions (the onboarding hint traces them) must drop them: the letters have moved.
        /// </summary>
        public event Action WheelRebuilt;

        private readonly List<LetterNode> _nodes = new List<LetterNode>();
        private readonly List<LetterNode> _selectedNodes = new List<LetterNode>();
        private string _wheelLetters = "";
        private bool _isDragging = false;
        private Camera _mainCamera;
        private float _fittedNodeSize;
        private WondersOfWordGameModel _gameModel;
        private AudioManager _audioManager;

        private void Awake()
        {
            _mainCamera = Camera.main;
            EnsureLineRenderer();
        }

        private void Start()
        {
            if (ServiceLocator.Current.Has<WondersOfWordGameModel>())
                _gameModel = ServiceLocator.Current.Get<WondersOfWordGameModel>();
            if (ServiceLocator.Current.Has<AudioManager>())
                _audioManager = ServiceLocator.Current.Get<AudioManager>();
        }

        public void SetupWheel(string letters)
        {
            _wheelLetters = letters.ToUpperInvariant();
            ClearNodes();

            int count = _wheelLetters.Length;
            if (count == 0) return;

            // Node size is resolved against the screen before the ring is measured: the ring
            // grows with letter count, and on a narrow-aspect device (a 4:3 tablet, where the
            // camera's vertical extent is fixed but horizontal is tighter) a 7-letter wheel at
            // the authored size runs past both edges.
            MoveToAnchor();

            _fittedNodeSize = FitNodeSize(count);
            float radius = GetRingRadius(count);
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angleRad = (i * angleStep + 90f) * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * radius, Mathf.Sin(angleRad) * radius, 0f);

                LetterNode node = FactoryFuncMapping.CreateLetterNode();
                if (node == null) continue;

                node.transform.SetParent(transform, false);
                node.transform.localPosition = pos;
                node.Initialize(_wheelLetters[i], i);
                node.SetSize(_fittedNodeSize);
                node.SetFontSize(letterFontSize);
                node.SetFont(letterFont);
                _nodes.Add(node);
            }

            ResizeBackdrop();

            // The trail width was set in Awake, before the fit was known.
            EnsureLineRenderer();
            ResetSelection();

            WheelRebuilt?.Invoke();
        }

        /// <summary>
        /// Radius that puts neighbouring letters a fixed distance apart, so a 4-letter wheel
        /// is not sparse and a 7-letter wheel is not cramped.
        /// Chord between neighbours on a ring of n points is 2*R*sin(pi/n), so solving for the
        /// wanted chord gives R = chord / (2*sin(pi/n)).
        /// </summary>
        private float GetRingRadius(int count)
        {
            if (!autoWheelRadius || count < 2) return wheelRadius;

            float chord = EffectiveNodeSize * nodeSpacingRatio;
            return chord / (2f * Mathf.Sin(Mathf.PI / count));
        }

        /// <summary>
        /// World positions to swipe through to spell a word, or false if the wheel cannot spell
        /// it. Repeated letters consume separate nodes, so ABASHED uses both As rather than
        /// pointing at the same one twice.
        /// </summary>
        public bool TryGetSwipePath(string word, List<Vector3> path)
        {
            path.Clear();
            if (string.IsNullOrEmpty(word) || _nodes.Count == 0) return false;

            var used = new bool[_nodes.Count];

            foreach (char letter in word.ToUpperInvariant())
            {
                int match = -1;
                for (int i = 0; i < _nodes.Count; i++)
                {
                    if (used[i] || _nodes[i] == null || _nodes[i].Letter != letter) continue;
                    match = i;
                    break;
                }

                if (match < 0)
                {
                    path.Clear();
                    return false;
                }

                used[match] = true;
                path.Add(_nodes[match].transform.position);
            }

            return path.Count > 0;
        }

        private void OnEnable() => LayoutService.LayoutChanged += OnLayoutChanged;

        private void OnDisable() => LayoutService.LayoutChanged -= OnLayoutChanged;

        /// <summary>
        /// Rebuilt rather than repositioned: the ring's radius depends on the width it is
        /// allowed, which differs between orientations, so moving it alone would leave a
        /// portrait-sized wheel sitting in the landscape slot.
        /// </summary>
        private void OnLayoutChanged(ScreenLayout layout)
        {
            if (!string.IsNullOrEmpty(_wheelLetters)) SetupWheel(_wheelLetters);
        }

        /// <summary>
        /// Puts the ring where the current orientation wants it: bottom centre in portrait,
        /// left of centre in landscape with the grid taking the right.
        /// </summary>
        private void MoveToAnchor()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector2 anchor = LayoutService.IsLandscape ? landscapeAnchor : portraitAnchor;

            float depth = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
            Vector3 world = _mainCamera.ViewportToWorldPoint(new Vector3(anchor.x, anchor.y, depth));
            transform.position = new Vector3(world.x, world.y, transform.position.z);
        }

        /// <summary>Node size actually in use, which is the authored size until the screen forces it down.</summary>
        private float EffectiveNodeSize => _fittedNodeSize > 0f ? _fittedNodeSize : nodeSize;

        /// <summary>
        /// The on-screen size of one letter, after the screen-fit shrink. Overlays size
        /// themselves against this so they hold their proportions on any aspect ratio.
        /// </summary>
        public float NodeSizeInUse => EffectiveNodeSize;

        /// <summary>How much the ring was shrunk to fit, 1 when it was not.</summary>
        private float FitScale => nodeSize > 0.0001f ? EffectiveNodeSize / nodeSize : 1f;

        /// <summary>
        /// Largest node size whose ring still fits across the screen width, capped at the
        /// authored <see cref="nodeSize"/>. Ring diameter scales linearly with node size, so
        /// the fit is a single ratio rather than a search.
        /// </summary>
        private float FitNodeSize(int count)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || count < 2) return nodeSize;

            float depth = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
            float halfWidth = Mathf.Abs(
                _mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x -
                _mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth)).x);

            float fraction = LayoutService.IsLandscape ? landscapeWidthFraction : wheelWidthFraction;
            float available = halfWidth * 2f * fraction;

            // Ring outer diameter at the authored size, including the backdrop ring.
            float chord = nodeSize * nodeSpacingRatio;
            float radius = chord / (2f * Mathf.Sin(Mathf.PI / count));
            float diameter = (radius + nodeSize * 0.5f + backdropPadding) * 2f;
            if (diameter <= 0.0001f) return nodeSize;

            // Scales both ways: a four-letter wheel on a wide screen was a tiny cluster in a
            // large empty column, which is what made landscape look unfinished.
            float scaled = nodeSize * (available / diameter);
            return Mathf.Clamp(scaled, minNodeSize, maxNodeSize);
        }

        /// <summary>
        /// Sizes the wheel backdrop to enclose the node ring. Done at runtime so a prefab
        /// carrying a stale baked-in scale corrects itself instead of silently overflowing.
        /// </summary>
        private void ResizeBackdrop()
        {
            if (backdropRenderer == null)
            {
                Transform t = transform.Find("WheelBackdrop");
                if (t != null) backdropRenderer = t.GetComponent<SpriteRenderer>();
            }

            if (backdropRenderer == null) return;

            backdropRenderer.enabled = showBackdrop;
            if (!showBackdrop || backdropRenderer.sprite == null) return;

            float nativeSize = backdropRenderer.sprite.bounds.size.x;
            if (nativeSize <= 0f) return;

            float backdropRadius = GetRingRadius(_nodes.Count) + EffectiveNodeSize * 0.5f + backdropPadding;
            float scale = (backdropRadius * 2f) / nativeSize;
            backdropRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void ShuffleWheel()
        {
            if (string.IsNullOrEmpty(_wheelLetters)) return;

            char[] chars = _wheelLetters.ToCharArray();
            System.Random rng = new System.Random();
            int n = chars.Length;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = chars[k];
                chars[k] = chars[n];
                chars[n] = value;
            }

            SetupWheel(new string(chars));
            if (_audioManager != null) _audioManager.PlayShuffleSound();
            HapticManager.Play(HapticType.Light);
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            Vector3 inputPos = Vector3.zero;
            bool isDown = false;
            bool isHeld = false;
            bool isUp = false;

            if (Pointer.current != null)
            {
                inputPos = Pointer.current.position.ReadValue();
                isDown = Pointer.current.press.wasPressedThisFrame;
                isHeld = Pointer.current.press.isPressed;
                isUp = Pointer.current.press.wasReleasedThisFrame;
            }
            else if (Input.touchCount > 0)
            {
                UnityEngine.Touch touch = Input.GetTouch(0);
                inputPos = touch.position;
                if (touch.phase == UnityEngine.TouchPhase.Began) isDown = true;
                if (touch.phase == UnityEngine.TouchPhase.Moved || touch.phase == UnityEngine.TouchPhase.Stationary) isHeld = true;
                if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled) isUp = true;
            }
            else
            {
                inputPos = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) isDown = true;
                if (Input.GetMouseButton(0)) isHeld = true;
                if (Input.GetMouseButtonUp(0)) isUp = true;
            }

            Vector3 worldPos = GetWorldPointFromInput(inputPos);

            if (isDown)
            {
                _isDragging = true;
                _selectedNodes.Clear();
                CheckNodeHit(worldPos);
            }
            else if (isHeld && _isDragging)
            {
                CheckNodeHit(worldPos);
                UpdateLineRenderer(worldPos);
            }
            else if (isUp && _isDragging)
            {
                _isDragging = false;
                SubmitSelectedWord();
                ResetSelection();
            }
        }

        private void CheckNodeHit(Vector3 worldPos)
        {
            foreach (var node in _nodes)
            {
                float dist = Vector3.Distance(node.transform.position, worldPos);
                // Hit radius tracks the drawn node, or shrunken letters would be
                // selectable from well outside their circle.
                if (dist <= selectionRadius * FitScale)
                {
                    if (!_selectedNodes.Contains(node))
                    {
                        _selectedNodes.Add(node);
                        node.SetSelected(true);

                        if (_audioManager != null) _audioManager.PlaySwipeCharSound();
                        HapticManager.Play(HapticType.Selection);
                        if (_gameModel != null)
                        {
                            _gameModel.CurrentWordPreview.Value = GetSelectedWordString();
                            _gameModel.NotifySwipeCharAdded(node.Letter);
                        }
                    }
                    else if (_selectedNodes.Count > 1 && _selectedNodes[_selectedNodes.Count - 2] == node)
                    {
                        // Backtrack gesture (undo last selection)
                        LetterNode last = _selectedNodes[_selectedNodes.Count - 1];
                        last.SetSelected(false);
                        _selectedNodes.RemoveAt(_selectedNodes.Count - 1);

                        if (_gameModel != null) _gameModel.CurrentWordPreview.Value = GetSelectedWordString();
                    }
                    break;
                }
            }
        }

        private void UpdateLineRenderer(Vector3 currentPointerPos)
        {
            if (lineRenderer == null || _selectedNodes.Count == 0)
            {
                if (lineRenderer != null) lineRenderer.positionCount = 0;
                return;
            }

            Vector3 lastNodePos = _selectedNodes[_selectedNodes.Count - 1].transform.position;

            // Only draw the trailing segment to the cursor once it has left the last node.
            // A near-zero final segment makes the corner join collapse into a spike that reads
            // as an arrowhead, and the pointer sits on the node for the whole of each tap.
            float trailingDistance = Vector3.Distance(currentPointerPos, lastNodePos);
            bool showTrailing = trailingDistance > EffectiveNodeSize * 0.5f;

            int count = _selectedNodes.Count + (showTrailing ? 1 : 0);

            // A single point renders nothing useful and still builds cap geometry.
            if (count < 2)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.positionCount = count;

            for (int i = 0; i < _selectedNodes.Count; i++)
            {
                lineRenderer.SetPosition(i, _selectedNodes[i].transform.position);
            }

            if (showTrailing) lineRenderer.SetPosition(count - 1, currentPointerPos);
        }

        private void SubmitSelectedWord()
        {
            string word = GetSelectedWordString();
            if (!string.IsNullOrEmpty(word) && word.Length >= 2)
            {
                OnWordSubmitted?.Invoke(word);
            }
        }

        private string GetSelectedWordString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var node in _selectedNodes)
            {
                sb.Append(node.Letter);
            }
            return sb.ToString();
        }

        private void ResetSelection()
        {
            foreach (var node in _nodes)
            {
                node.SetSelected(false);
            }
            _selectedNodes.Clear();
            if (lineRenderer != null) lineRenderer.positionCount = 0;
            if (_gameModel != null) _gameModel.CurrentWordPreview.Value = "";
        }

        private void ClearNodes()
        {
            // Nodes are pooled - return them to the factory instead of destroying them.
            foreach (var node in _nodes)
            {
                if (node != null) FactoryFuncMapping.RecycleLetterNode(node);
            }
            _nodes.Clear();
        }

        private Vector3 GetWorldPointFromInput(Vector3 screenPos)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                screenPos.z = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
                return _mainCamera.ScreenToWorldPoint(screenPos);
            }
            return screenPos;
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.startWidth = lineWidth * FitScale;
            lineRenderer.endWidth = lineWidth * FitScale;

            // One flat colour at both ends. A start/end gradient is normalised across the whole
            // polyline, so a 2-letter path and a 6-letter path would render different colour
            // spreads - the line has to look identical no matter how long the word is.
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            lineRenderer.useWorldSpace = true;
            lineRenderer.sortingOrder = 10;
            lineRenderer.positionCount = 0;

            // A drag path doubles back sharply between nodes. With no corner/cap geometry the
            // miter join at that reversal degenerates into a spike, which reads as a wedge.
            lineRenderer.numCornerVertices = 6;
            lineRenderer.numCapVertices = 6;

            // Billboard toward the camera so the ribbon keeps a constant on-screen thickness
            // regardless of the path's direction.
            lineRenderer.alignment = LineAlignment.View;

            // Tile rather than Stretch: a stretched texture would scale with path length and
            // reintroduce the per-word difference this is meant to remove.
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.widthMultiplier = 1f;

            if (lineRenderer.sharedMaterial == null)
            {
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }
}
