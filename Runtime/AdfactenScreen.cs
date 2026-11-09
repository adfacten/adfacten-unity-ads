using UnityEngine;

namespace AdfactenAds
{
    /// <summary>Helpers for the screen rectangles used by <see cref="NativeAd.Show"/> and <see cref="AdPosition.Custom"/>.</summary>
    public static class AdfactenScreen
    {
        /// <summary>
        /// The on-screen rectangle of a UI element, in Unity screen pixels from the top-left corner. Use a
        /// placeholder <c>RectTransform</c> in your layout to reserve the space for a native ad.
        /// </summary>
        /// <param name="camera">The canvas camera; null for a Screen Space - Overlay canvas.</param>
        public static Rect FromRectTransform(RectTransform rectTransform, Camera camera = null)
        {
            if (rectTransform == null) return new Rect();
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var max = min;
            for (var i = 1; i < 4; i++)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return FromBottomLeft(new Rect(min.x, min.y, max.x - min.x, max.y - min.y), Screen.height);
        }

        /// <summary>Converts a rectangle in Unity's bottom-left screen space to the top-left space used by the plugin.</summary>
        public static Rect FromBottomLeft(Rect rect, float screenHeight)
        {
            return new Rect(rect.x, screenHeight - rect.y - rect.height, rect.width, rect.height);
        }
    }
}
