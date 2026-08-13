using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WordPuzzle.Gameplay
{
    public class LetterNode : MonoBehaviour
    {
        [Header("2D World / UI Visual Components")]
        public SpriteRenderer bgSprite;
        public TextMeshPro letterTextMesh;

        [Header("Sprite State Assets")]
        public Sprite normalSprite;
        public Sprite selectedSprite;

        public char Letter { get; private set; }
        public int Index { get; private set; }
        public bool IsSelected { get; private set; }

        private Vector3 _baseScale = Vector3.one;

        public void Initialize(char letter, int index)
        {
            Letter = char.ToUpperInvariant(letter);
            Index = index;
            IsSelected = false;

            EnsureComponents();
            if (letterTextMesh != null)
            {
                letterTextMesh.text = Letter.ToString();
                letterTextMesh.fontSize = 2.4f;
            }
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (bgSprite != null)
            {
                bgSprite.color = Color.white;
                if (selected && selectedSprite != null)
                {
                    bgSprite.sprite = selectedSprite;
                }
                else if (normalSprite != null)
                {
                    bgSprite.sprite = normalSprite;
                }
            }

            if (transform != null)
            {
                transform.localScale = selected ? _baseScale * 1.2f : _baseScale;
            }
        }

        /// <summary>
        /// Scales the node so its sprite renders exactly <paramref name="worldSize"/> units wide,
        /// independent of the source texture's resolution / pixels-per-unit.
        /// </summary>
        /// <summary>
        /// Sets the letter size. The label is a child of a transform already scaled to the node,
        /// so this value is in the node's local space and stays proportional automatically.
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

        public void SetSize(float worldSize)
        {
            EnsureComponents();

            if (bgSprite == null || bgSprite.sprite == null) return;

            float nativeSize = bgSprite.sprite.bounds.size.x;
            if (nativeSize <= 0f) return;

            float factor = worldSize / nativeSize;
            _baseScale = new Vector3(factor, factor, 1f);
            transform.localScale = IsSelected ? _baseScale * 1.2f : _baseScale;
        }

        private void EnsureComponents()
        {
            if (bgSprite == null) bgSprite = GetComponent<SpriteRenderer>();
            if (bgSprite == null)
            {
                bgSprite = gameObject.AddComponent<SpriteRenderer>();
            }
            bgSprite.sortingOrder = 6;

            if (normalSprite == null)
            {
                normalSprite = Resources.Load<Sprite>("Sprites/letter_node_normal");
#if UNITY_EDITOR
                if (normalSprite == null)
                {
                    normalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/letter_node_normal.png");
                }
#endif
            }

            if (selectedSprite == null)
            {
                selectedSprite = Resources.Load<Sprite>("Sprites/letter_node_selected");
#if UNITY_EDITOR
                if (selectedSprite == null)
                {
                    selectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WordPuzzle/Sprites/letter_node_selected.png");
                }
#endif
            }

            if (letterTextMesh == null) letterTextMesh = GetComponentInChildren<TextMeshPro>();
            if (letterTextMesh == null)
            {
                GameObject textObj = new GameObject("LetterText");
                textObj.transform.SetParent(transform, false);
                letterTextMesh = textObj.AddComponent<TextMeshPro>();
                letterTextMesh.fontSize = 2.4f;
                letterTextMesh.fontWeight = FontWeight.Bold;
                letterTextMesh.color = Color.white;
                letterTextMesh.sortingOrder = 7;
                TileTextCentering.Apply(letterTextMesh);
            }
            else
            {
                letterTextMesh.fontSize = 2.4f;
                letterTextMesh.sortingOrder = 7;
                TileTextCentering.Apply(letterTextMesh);
            }
        }
    }
}
