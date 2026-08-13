using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WordPuzzle.Gameplay
{
    public class GridTile : MonoBehaviour
    {
        [Header("Tile 2D World / UI Components")]
        public SpriteRenderer tileSprite;
        public TextMeshPro letterTextMesh;

        [Header("Sprite State Assets")]
        public Sprite hiddenTileSprite;
        public Sprite revealedTileSprite;

        public char ExpectedLetter { get; private set; }
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool IsRevealed { get; private set; }

        private Color _textColorRevealed = new Color(0.08f, 0.12f, 0.22f, 1.0f);
        private Vector3 _baseScale = Vector3.one;
        private Coroutine _scaleRoutine;

        public void Initialize(char letter, int row, int col)
        {
            ExpectedLetter = char.ToUpperInvariant(letter);
            Row = row;
            Col = col;
            IsRevealed = false;

            EnsureComponents();
            SetRevealed(false, false);
        }

        public void Reveal(bool animate = true)
        {
            if (IsRevealed) return;
            SetRevealed(true, animate);
        }

        private void SetRevealed(bool revealed, bool animate)
        {
            IsRevealed = revealed;

            if (tileSprite != null)
            {
                tileSprite.color = Color.white;
                if (revealed && revealedTileSprite != null)
                {
                    tileSprite.sprite = revealedTileSprite;
                }
                else if (hiddenTileSprite != null)
                {
                    tileSprite.sprite = hiddenTileSprite;
                }
            }

            if (letterTextMesh != null)
            {
                letterTextMesh.text = revealed ? ExpectedLetter.ToString() : "";
                letterTextMesh.color = revealed ? _textColorRevealed : Color.clear;
            }

            if (revealed && animate)
            {
                AnimateBounce();
            }
        }

        /// <summary>
        /// Scales the tile so its sprite renders exactly <paramref name="worldSize"/> units wide,
        /// independent of the source texture's resolution / pixels-per-unit.
        /// </summary>
        public void SetSize(float worldSize)
        {
            EnsureComponents();

            if (tileSprite == null || tileSprite.sprite == null) return;

            float nativeSize = tileSprite.sprite.bounds.size.x;
            if (nativeSize <= 0f) return;

            float factor = worldSize / nativeSize;
            _baseScale = new Vector3(factor, factor, 1f);
            transform.localScale = _baseScale;
        }

        /// <summary>
        /// Sets the letter size. The label is a child of a transform already scaled to the tile,
        /// so this value is in the tile's local space and stays proportional automatically.
        /// </summary>
        public void SetFontSize(float fontSize)
        {
            EnsureComponents();
            if (letterTextMesh != null) letterTextMesh.fontSize = fontSize;
        }

        /// <summary>Applies a font asset. Null leaves the TMP default in place.</summary>
        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;

            EnsureComponents();
            if (letterTextMesh != null) letterTextMesh.font = font;
        }

        private void AnimateBounce()
        {
            // Without this a second reveal (or a disable mid-coroutine) can strand the tile at
            // an intermediate scale, leaving revealed tiles visibly larger than hidden ones.
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);

            if (!isActiveAndEnabled)
            {
                transform.localScale = _baseScale;
                return;
            }

            transform.localScale = _baseScale * 1.25f;
            _scaleRoutine = StartCoroutine(ScaleRoutine(_baseScale, 0.25f));
        }

        private void OnDisable()
        {
            _scaleRoutine = null;
            transform.localScale = _baseScale;
        }

        private System.Collections.IEnumerator ScaleRoutine(Vector3 targetScale, float time)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < time)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / time;
                transform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }
            transform.localScale = targetScale;
            _scaleRoutine = null;
        }

        private void EnsureComponents()
        {
            if (tileSprite == null) tileSprite = GetComponent<SpriteRenderer>();
            if (tileSprite == null)
            {
                tileSprite = gameObject.AddComponent<SpriteRenderer>();
            }
            tileSprite.sortingOrder = 5;

            if (hiddenTileSprite == null)
            {
                hiddenTileSprite = Resources.Load<Sprite>("Sprites/grid_tile_hidden");
#if UNITY_EDITOR
                if (hiddenTileSprite == null)
                {
                    hiddenTileSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/grid_tile_hidden.png");
                }
#endif
            }

            if (revealedTileSprite == null)
            {
                revealedTileSprite = Resources.Load<Sprite>("Sprites/grid_tile_revealed");
#if UNITY_EDITOR
                if (revealedTileSprite == null)
                {
                    revealedTileSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/grid_tile_revealed.png");
                }
#endif
            }

            if (letterTextMesh == null) letterTextMesh = GetComponentInChildren<TextMeshPro>();
            if (letterTextMesh == null)
            {
                GameObject textObj = new GameObject("TileLetterText");
                textObj.transform.SetParent(transform, false);
                letterTextMesh = textObj.AddComponent<TextMeshPro>();
                letterTextMesh.fontSize = 2.5f;
                letterTextMesh.fontWeight = FontWeight.Bold;
                letterTextMesh.color = Color.clear;
                letterTextMesh.sortingOrder = 6;
            }
            TileTextCentering.Apply(letterTextMesh);
        }
    }
}
