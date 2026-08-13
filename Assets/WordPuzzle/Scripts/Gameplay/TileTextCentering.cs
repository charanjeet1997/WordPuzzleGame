using UnityEngine;
using TMPro;

namespace WordPuzzle.Gameplay
{
    /// <summary>
    /// Shared optical-centering for the single-character labels drawn on grid tiles and
    /// letter nodes.
    /// </summary>
    internal static class TileTextCentering
    {
        /// <summary>
        /// Centres a single-glyph label inside its parent sprite.
        /// <see cref="TextAlignmentOptions.Center"/> centres on the line box, which reserves
        /// descender space no capital letter uses, so the glyph reads as sitting high. Midline
        /// centres on the cap-height midline instead, which is what looks centred for A-Z.
        /// </summary>
        public static void Apply(TextMeshPro text)
        {
            if (text == null) return;

            text.alignment = TextAlignmentOptions.Midline;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;

            RectTransform rect = text.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }
    }
}
