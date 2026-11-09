using System.Collections.Generic;
using AdfactenAds;
using UnityEngine;

/// <summary>
/// Shows every Adfacten format. Preconfigured with Adfacten's public test apps, which always get unbilled test ads.
/// Set Player Settings > Other Settings > Package Name / Bundle Identifier to com.adfacten.sample: the ad server
/// checks it against the app key. In the Editor, the plugin shows placeholders instead.
/// </summary>
public sealed class AdfactenDemo : MonoBehaviour
{
    private const string SampleBundleId = "com.adfacten.sample";

#if UNITY_IOS
    private const string AppKey = "apk_r2FjVF7YUGb3ITZZUt6ENHH1";
    private const string BannerId = "plc_034uZ7K268zGmUPhbl0LgL";
    private const string InterstitialId = "plc_034uZ7K26J5FvjnFBJjThJ";
    private const string RewardedId = "plc_034uZ7K2CBEP9L6EQszuIk";
    private const string NativeId = "plc_034uZ7K2I1fWOEzDnkJpwt";
    private const string AppOpenId = "plc_034uZ7K2IBiNmiy9FGrHNe";
#else
    private const string AppKey = "apk_3cnu84fqX8ucMX9XPyeQYHrR";
    private const string BannerId = "plc_034uZ7K1c31r4F5eyGyUpC";
    private const string InterstitialId = "plc_034uZ7K1i1xnu0Ksljnmy4";
    private const string RewardedId = "plc_034uZ7K1nrWgf7S5ATCLrI";
    private const string NativeId = "plc_034uZ7K1o0SSewGeBCGY9P";
    private const string AppOpenId = "plc_034uZ7K1o901BKzbEbWDjK";
#endif

    private static readonly AdSize[] Sizes = { AdSize.Banner, AdSize.LargeBanner, AdSize.MediumRectangle, AdSize.Leaderboard };
    private static readonly AdPosition[] Positions = { AdPosition.Bottom, AdPosition.Top, AdPosition.Center, AdPosition.BottomLeft, AdPosition.TopRight };

    private readonly List<string> _log = new List<string>();
    private BannerView _banner;
    private int _size;
    private int _position;
    private InterstitialAd _interstitial;
    private RewardedAd _rewarded;
    private AppOpenAd _appOpen;
    private bool _showAppOpenOnResume = true;
    private NativeAd _native;
    private NativeAdTemplate _template = NativeAdTemplate.Small;
    private Vector2 _scroll;
    private int _coins;

    private void Awake()
    {
        if (!Application.isEditor && Application.identifier != SampleBundleId)
            Log("Bundle id is " + Application.identifier + ": the test apps only accept " + SampleBundleId + " (expect rejected / bundle_mismatch).");

        // The public test apps always get test ads; your own app gets them in development builds by default.
        Adfacten.Initialize(AppKey, new AdfactenConfig { TestMode = true, LogLevel = LogLevel.Info });
        Adfacten.SetUserId("demo-user");
        Log("Initialized, SDK " + Adfacten.SdkVersion);
        LoadAppOpen();
    }

    private void OnDestroy()
    {
        if (_banner != null) _banner.Destroy();
        if (_native != null) _native.Destroy();
        if (_interstitial != null) _interstitial.Destroy();
        if (_rewarded != null) _rewarded.Destroy();
        if (_appOpen != null) _appOpen.Destroy();
    }

    private void OnApplicationPause(bool paused)
    {
        // Back from the background: show the app-open ad if one is ready.
        if (!paused && _showAppOpenOnResume && _appOpen != null && _appOpen.IsReady) _appOpen.Show();
    }

    // Banner

    private void LoadBanner()
    {
        if (_banner != null) _banner.Destroy();
        _banner = new BannerView(BannerId, Sizes[_size], Positions[_position]);
        _banner.OnLoaded += () => Log("Banner loaded (" + _banner.GetWidthInPixels() + " x " + _banner.GetHeightInPixels() + " px)");
        _banner.OnFailedToLoad += e => Log("Banner failed: " + e.Name + " " + e.ServerCode);
        _banner.OnShown += () => Log("Banner impression");
        _banner.OnClicked += () => Log("Banner clicked");
        _banner.Load();
    }

    // Full screen

    private void LoadInterstitial()
    {
        InterstitialAd.Load(InterstitialId, (ad, error) =>
        {
            if (error != null)
            {
                Log("Interstitial failed: " + error);
                return;
            }
            _interstitial = ad;
            ad.OnShown += () => Log("Interstitial shown");
            ad.OnClicked += () => Log("Interstitial clicked");
            ad.OnFailedToShow += e => Log("Interstitial show failed: " + e.Name);
            ad.OnClosed += () =>
            {
                Log("Interstitial closed");
                _interstitial = null;
            };
            Log("Interstitial loaded");
        });
    }

    private void LoadRewarded()
    {
        RewardedAd.Load(RewardedId, "level=3", (ad, error) =>
        {
            if (error != null)
            {
                Log("Rewarded failed: " + error);
                return;
            }
            _rewarded = ad;
            ad.OnShown += () => Log("Rewarded shown");
            ad.OnFailedToShow += e => Log("Rewarded show failed: " + e.Name);
            ad.OnClosed += () =>
            {
                Log("Rewarded closed");
                _rewarded = null;
            };
            Log("Rewarded loaded" + (ad.Reward != null ? ": " + ad.Reward.Amount + " " + ad.Reward.Type : ""));
        });
    }

    private void ShowRewarded()
    {
        _rewarded.Show(reward =>
        {
            // UI only. Grant the reward from your server's reward callback.
            _coins += (int)reward.Amount;
            Log("Earned " + reward.Amount + " " + reward.Type);
        });
    }

    private void LoadAppOpen()
    {
        AppOpenAd.Load(AppOpenId, (ad, error) =>
        {
            if (error != null)
            {
                Log("App open failed: " + error);
                return;
            }
            _appOpen = ad;
            ad.OnClosed += () =>
            {
                Log("App open closed");
                _appOpen = null;
                LoadAppOpen();
            };
            Log("App open loaded");
        });
    }

    // Native

    private void LoadNative()
    {
        if (_native != null) _native.Destroy();
        _native = null;
        NativeAd.Load(NativeId, (ad, error) =>
        {
            if (error != null)
            {
                Log("Native failed: " + error);
                return;
            }
            _native = ad;
            ad.OnShown += () => Log("Native impression");
            ad.OnClicked += () => Log("Native clicked");
            Log("Native loaded: " + ad.Title);
            ShowNative();
        });
    }

    private void ShowNative()
    {
        if (_native == null) return;
        _native.Show(_template, NativeRect(), new NativeAdStyle { CallToActionBackgroundColor = new Color32(0x1E, 0x88, 0xE5, 0xFF) });
    }

    /// <summary>The bottom part of the safe area, above a bottom banner, in screen pixels from the top-left.</summary>
    private Rect NativeRect()
    {
        var safe = AdfactenScreen.FromBottomLeft(Screen.safeArea, Screen.height);
        var dp = Scale();
        var height = (_template == NativeAdTemplate.Small ? 90 : 320) * dp;
        var bottomGap = (_banner != null && _banner.IsVisible ? _banner.GetHeightInPixels() : 0) + 8 * dp;
        return new Rect(safe.x + 8 * dp, safe.yMax - bottomGap - height, safe.width - 16 * dp, height);
    }

    // UI

    private void OnGUI()
    {
        var scale = Scale();
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        var safe = AdfactenScreen.FromBottomLeft(Screen.safeArea, Screen.height);
        var area = new Rect(safe.x / scale + 8, safe.y / scale + 8, safe.width / scale - 16, safe.height / scale * 0.5f);
        GUILayout.BeginArea(area);
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label("Adfacten demo  |  coins (client side): " + _coins);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Banner: load")) LoadBanner();
        if (GUILayout.Button(_banner != null && !_banner.IsVisible ? "Show" : "Hide") && _banner != null)
        {
            if (_banner.IsVisible) _banner.Hide(); else _banner.Show();
        }
        if (GUILayout.Button("Position") && _banner != null)
        {
            _position = (_position + 1) % Positions.Length;
            _banner.SetPosition(Positions[_position]);
        }
        if (GUILayout.Button("Size " + Sizes[_size]))
        {
            _size = (_size + 1) % Sizes.Length;
            if (_banner != null) LoadBanner();
        }
        if (GUILayout.Button("Destroy") && _banner != null)
        {
            _banner.Destroy();
            _banner = null;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Interstitial: load")) LoadInterstitial();
        GUI.enabled = _interstitial != null && _interstitial.IsReady;
        if (GUILayout.Button("Show")) _interstitial.Show();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Rewarded: load")) LoadRewarded();
        GUI.enabled = _rewarded != null && _rewarded.IsReady;
        if (GUILayout.Button("Show")) ShowRewarded();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("App open: load")) LoadAppOpen();
        GUI.enabled = _appOpen != null && _appOpen.IsReady;
        if (GUILayout.Button("Show")) _appOpen.Show();
        GUI.enabled = true;
        _showAppOpenOnResume = GUILayout.Toggle(_showAppOpenOnResume, "on resume");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Native: load")) LoadNative();
        if (GUILayout.Button(_template == NativeAdTemplate.Small ? "Small" : "Medium"))
        {
            _template = _template == NativeAdTemplate.Small ? NativeAdTemplate.Medium : NativeAdTemplate.Small;
            ShowNative();
        }
        if (GUILayout.Button("Hide") && _native != null) _native.Hide();
        if (GUILayout.Button("Destroy") && _native != null)
        {
            _native.Destroy();
            _native = null;
        }
        GUILayout.EndHorizontal();

        if (Application.platform == RuntimePlatform.IPhonePlayer && GUILayout.Button("Ask for tracking (ATT)"))
            Adfacten.RequestTrackingAuthorization(status => Log("Tracking: " + status));

        foreach (var line in _log) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static float Scale()
    {
        return Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
    }

    private void Log(string message)
    {
        Debug.Log("[AdfactenDemo] " + message);
        _log.Insert(0, message);
        if (_log.Count > 30) _log.RemoveAt(_log.Count - 1);
    }
}
