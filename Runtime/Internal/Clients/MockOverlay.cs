using UnityEngine;

namespace AdfactenAds.Internal
{
    /// <summary>Draws the Editor mock's placeholder ads with IMGUI, on top of the game.</summary>
    [AddComponentMenu("")]
    internal sealed class MockOverlay : MonoBehaviour
    {
        private MockClient _mock;
        private GUIStyle _box;
        private GUIStyle _title;
        private GUIStyle _small;
        private Texture2D _banner;
        private Texture2D _native;
        private Texture2D _fullScreen;

        public static void Create(MockClient mock)
        {
            var go = new GameObject("Adfacten Mock Ads");
            go.hideFlags = HideFlags.HideInHierarchy;
            if (Application.isPlaying) DontDestroyOnLoad(go);
            go.AddComponent<MockOverlay>()._mock = mock;
        }

        private void OnDestroy()
        {
            if (_banner != null) Destroy(_banner);
            if (_native != null) Destroy(_native);
            if (_fullScreen != null) Destroy(_fullScreen);
        }

        private void OnGUI()
        {
            if (_mock == null) return;
            GUI.depth = -1000;
            EnsureStyles();
            var scale = _mock.PixelsPerDp > 0 ? _mock.PixelsPerDp : Mathf.Max(1f, Screen.dpi / 160f);

            foreach (var b in _mock.Banners)
            {
                if (!b.Visible || !b.Loaded) continue;
                var r = _mock.BannerRect(b, Screen.width, Screen.height);
                GUI.DrawTexture(r, _banner);
                if (GUI.Button(r, "TEST BANNER - Editor placeholder\n" + b.PlacementId, _box)) _mock.ClickBanner(b.Handle);
            }

            foreach (var n in _mock.Natives)
            {
                if (!n.Visible || !n.Loaded) continue;
                GUI.DrawTexture(n.Rect, _native);
                var label = (n.Template == 0 ? "SMALL" : "MEDIUM") + " NATIVE TEMPLATE\nTest native ad - Editor placeholder\n" + n.PlacementId;
                if (GUI.Button(n.Rect, label, _box)) _mock.ClickNative(n.Handle);
            }

            var f = _mock.Showing;
            if (f == null) return;
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.DrawTexture(screen, _fullScreen);
            var kind = f.Format == AdFormat.Rewarded ? "REWARDED" : f.Format == AdFormat.AppOpen ? "APP OPEN" : "INTERSTITIAL";
            var left = _mock.RewardSecondsLeft(f);
            var text = "TEST " + kind + " AD\nEditor placeholder, no request was sent\n" + f.PlacementId;
            if (f.Format == AdFormat.Rewarded)
            {
                text += left > 0
                    ? "\n\nReward in " + Mathf.CeilToInt((float)left) + " s (closing now forfeits it)"
                    : "\n\nReward granted: " + MockClient.RewardAmount + " " + MockClient.RewardType;
            }
            if (GUI.Button(new Rect(0, Screen.height * 0.2f, Screen.width, Screen.height * 0.5f), text, _title)) _mock.ClickFullScreen(f.Handle);
            var size = 48 * scale;
            var top = Screen.height - Screen.safeArea.yMax;
            if (GUI.Button(new Rect(Screen.width - size - 12 * scale, top + 12 * scale, size, size), "X", _small))
                _mock.CloseFullScreen(f.Handle);
        }

        private void EnsureStyles()
        {
            if (_box != null) return;
            _banner = Solid(new Color(0.85f, 0.85f, 0.85f, 1f));
            _native = Solid(new Color(0.93f, 0.93f, 0.97f, 1f));
            _fullScreen = Solid(new Color(0.1f, 0.1f, 0.12f, 0.96f));
            _box = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            _box.normal.textColor = new Color(0.2f, 0.2f, 0.25f);
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Mathf.Max(14, Screen.height / 40) };
            _title.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(14, Screen.height / 50) };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
