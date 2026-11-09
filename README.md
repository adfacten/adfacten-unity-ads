# Adfacten Ads for Unity

Banner, interstitial, rewarded, app-open and native ads from the Adfacten network in Unity games for Android and iOS.

The plugin wraps the native Adfacten SDKs (1.0.2): ads are loaded, rendered and tracked by the same code as in
native apps. In the Editor a mock shows placeholder ads and fires the same events, so you can build the whole flow
without a device.

- Unity 2021.3 LTS to Unity 6
- Android 5.0+ (API 21; video ads need API 23), iOS 15+
- All callbacks run on the Unity main thread

## 1. Install

**Package Manager:** *Window > Package Manager > + > Add package from git URL…* and enter

```
https://github.com/adfacten/adfacten-unity-ads.git#1.0.0
```

or add it to `Packages/manifest.json`:

```json
"dependencies": {
  "com.adfacten.ads": "https://github.com/adfacten/adfacten-unity-ads.git#1.0.0"
}
```

**External Dependency Manager (recommended).** The native SDKs come from Maven (Android) and CocoaPods (iOS). The
External Dependency Manager for Unity (EDM4U) resolves them together with your other plugins. If your project has no
EDM4U yet, add it from OpenUPM (1.2.183 or newer):

```json
"scopedRegistries": [
  {
    "name": "package.openupm.com",
    "url": "https://package.openupm.com",
    "scopes": ["com.google.external-dependency-manager"]
  }
],
"dependencies": {
  "com.google.external-dependency-manager": "1.2.183"
}
```

The plugin declares its dependencies in `Editor/AdfactenDependencies.xml`. Without EDM4U it still builds: see
[Android without EDM4U](#android-without-edm4u) and [iOS](#ios-build).

## 2. Set up

Open **Project Settings > Adfacten** and enter your app keys (`apk_…`, one per platform, from the Adfacten panel
under *Apps*). The server checks the app's package name / bundle identifier against the key.

Initialize once, early, for example in your first scene:

```csharp
using AdfactenAds;

void Awake()
{
    Adfacten.Initialize(); // app key for this platform from Project Settings > Adfacten
}
```

or pass the key and options in code:

```csharp
Adfacten.Initialize("apk_your_app_key", new AdfactenConfig
{
    TestMode = true,        // null (default): on in development builds, off in release builds
    ChildDirected = false,  // COPPA / Google Families, see Privacy
    UserId = "player-42",   // echoed in the rewarded server callback
});
Adfacten.SetUserId("player-42"); // change it later, e.g. after login
```

Placement ids (`plc_…`) are in the panel under your app.

## 3. Banner

```csharp
var banner = new BannerView("plc_your_banner", AdSize.Banner, AdPosition.Bottom);
banner.OnLoaded += () => Debug.Log("banner loaded");
banner.OnFailedToLoad += error => Debug.Log("banner: " + error.Name);
banner.OnShown += () => Debug.Log("impression");
banner.OnClicked += () => Debug.Log("clicked");
banner.Load();

banner.Hide();                       // no refresh, no impressions while hidden
banner.Show();
banner.SetPosition(AdPosition.Top);
banner.Destroy();                    // when you no longer need it
```

Sizes: `AdSize.Banner` (320x50), `LargeBanner` (320x100), `MediumRectangle` (300x250), `Leaderboard` (728x90,
tablets). Positions: `Top`, `Bottom`, `Center`, `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` (inside the safe
area), or `AdPosition.Custom(x, y)` in Unity screen pixels from the top-left corner. `GetWidthInPixels()` /
`GetHeightInPixels()` give the banner size in screen pixels, for example to move your UI above it.

The banner refreshes itself on the placement's interval (30–120 s of visible time) and counts the impression only
when it is on screen. After a failed load it tries again by itself while visible, so keep it in place instead of
destroying it in `OnFailedToLoad`. A refusal (`Rejected`, `InvalidRequest`) stops retries until the next `Load()`.

## 4. Interstitial

```csharp
InterstitialAd interstitial;

InterstitialAd.Load("plc_your_interstitial", (ad, error) =>
{
    if (error != null) { Debug.Log(error); return; }
    interstitial = ad;
    ad.OnClosed += () => { interstitial = null; /* resume, load the next one */ };
});

// At a natural break:
if (interstitial != null && interstitial.IsReady) interstitial.Show();
```

A loaded ad is valid for about 55 minutes and can be shown once. The game is paused while a full-screen ad is on
screen (Unity stops the player loop and audio); events raised meanwhile arrive when it resumes.

## 5. Rewarded

```csharp
RewardedAd.Load("plc_your_rewarded", "level=3", (ad, error) =>
{
    if (error != null) return;
    ad.Show(reward => ShowToast("+" + reward.Amount + " " + reward.Type)); // UI only
});
```

`customData` (up to 256 characters) and the user id from `AdfactenConfig.UserId` / `Adfacten.SetUserId` are
echoed to your server. `OnUserEarnedReward` (or the `Show` callback) runs on the device and can be faked: use it
for UI only.

### Server-side verification

Grant rewards from the reward callback URL you set on the placement in the panel. After a valid completion Adfacten
calls:

```
GET https://your.server/reward?user_id=…&custom=…&reward_type=coins&reward_amount=10
    &transaction_id=<impression id>&placement=plc_…&ts=<unix>[&test=1]&sig=<hex>
```

`sig` is `HMAC-SHA256(reward secret, raw query string)`, where the raw query string is everything after `?` up to
(not including) `&sig=`, exactly as received. Answer with 2xx (Adfacten retries until it gets one), store
`transaction_id` and ignore repeats, and skip `test=1` in production.

## 6. App open

```csharp
AppOpenAd appOpen;

void LoadAppOpen() =>
    AppOpenAd.Load("plc_your_app_open", (ad, error) => { if (error == null) appOpen = ad; });

void OnApplicationPause(bool paused)
{
    if (!paused && appOpen != null && appOpen.IsReady)
    {
        appOpen.OnClosed += LoadAppOpen;
        appOpen.Show();
    }
}
```

Load ahead of time and show it on a cold start or when the player returns; don't keep them waiting for it.

## 7. Native

Unity UI cannot contain Android or iOS views, so native ads are drawn by the native SDK in a **native template**
placed over the game at a screen rectangle. That native view is what records impressions and clicks: drawing the
ad's texts with Unity UI is not tracked and earns nothing.

```csharp
NativeAd.Load("plc_your_native", (ad, error) =>
{
    if (error != null) return;
    nativeAd = ad;
    ad.OnShown += () => Debug.Log("impression");
    // A placeholder RectTransform in your layout reserves the space.
    var rect = AdfactenScreen.FromRectTransform(placeholder); // null camera for Screen Space - Overlay
    ad.Show(NativeAdTemplate.Medium, rect, new NativeAdStyle { CallToActionBackgroundColor = Color.blue });
});

nativeAd.Hide();     // e.g. when a menu covers the slot
nativeAd.Destroy();  // when you leave the screen
```

- `NativeAdTemplate.Small`: icon, label, brand, title, one line of text and a button; about 80 dp/pt high.
- `NativeAdTemplate.Medium`: a card with the main image; give it at least 300 dp/pt. The image takes the height left.
- Rectangles are Unity screen pixels from the top-left corner (`AdfactenScreen.FromBottomLeft` converts from
  Unity's bottom-left screen space). The template sits on top of everything Unity draws, so keep the area free of
  your own UI, call `Show` again with the new rectangle when the layout changes (rotation, scrolling), and `Hide` it
  when a popup covers it.
- `Title`, `Body`, `Brand`, `CallToAction`, `AdLabel`, `HasImage`, `HasIcon` are there to pick a template or
  skip a slot.

## 8. Errors

`AdError` has a stable `Name` shared by Android and iOS (`no_fill`, `network_error`, `rejected`, …), the parsed
`Code` (`AdErrorCode.NoFill`, …), `Message`, `ServerCode` and the platform's `NativeCode`.

| Name | Meaning |
|---|---|
| `no_fill` | No ad now. The SDK waits at least 30 s before asking again for that placement. |
| `network_error` | Offline or timeout. |
| `rejected` | The server refused the request. `ServerCode`: `unknown_app`, `app_not_approved`, `bundle_mismatch`, `platform_mismatch`, `placement_mismatch`, `coppa`, … |
| `invalid_request` | For example an empty placement id. |
| `not_initialized` | `Adfacten.Initialize` was not called. |
| `ad_expired`, `already_shown`, `not_ready` | Load a new ad. |
| `show_failed` | Another ad is on screen, or the app is in the background. The ad can be shown later. |
| `video_not_supported` | A video ad was served but Android video support is off (Project Settings > Adfacten). |
| `server_error`, `invalid_response`, `creative_failed`, `load_in_progress`, `child_directed`, `internal_error` | |

## 9. Test mode, Editor and demo

- `TestMode` (on by default in development builds) requests test ads, which are never billed. New apps get test
  ads until they are approved and app-ads.txt is verified; emulators and simulators always get test ads.
- In the Editor nothing is requested: placeholders appear where the ads would, full-screen placeholders have a
  close button and rewarded ones grant `10 coins` after 5 seconds. `AdfactenMock.LoadError = AdErrorCode.NoFill;`
  makes every load fail so you can test your error paths. Release builds for other platforms show nothing and
  fail loads with `internal_error`.
- **Demo sample:** import it from the package's *Samples* tab. It uses Adfacten's public test apps, which always
  receive unbilled test ads. Set **Player Settings > Other Settings > Package Name / Bundle Identifier** to
  `com.adfacten.sample`: the server checks it (otherwise loads fail with `rejected` / `bundle_mismatch`).

## 10. Privacy

- **Consent (GDPR / TCF v2, GPP, US Privacy):** the SDKs read the IAB keys your consent platform stores
  (`IABTCF_*`, `IABGPP_*`, `IABUSPrivacy_String`). Show your CMP before loading ads. Without purpose 1 consent under
  GDPR no device advertising ID is sent.
- **Children (COPPA / Google Families):** check *Child-directed* in Project Settings > Adfacten (or
  `AdfactenConfig.ChildDirected`). The SDKs then never read advertising IDs and Adfacten does not serve ads to such
  traffic. On Android also turn off *Advertising ID* or check *Remove AD_ID permission*.
- **Android AD_ID:** the optional `ads-gaid` artifact (on by default) reads the advertising ID and App Set ID and
  adds the `com.google.android.gms.permission.AD_ID` permission. Declare the advertising ID in Play Console. *Remove
  AD_ID permission* adds a `tools:node="remove"` entry to the merged manifest. The SDK collects device IDs, approximate
  location from the IP and ad interactions; user IDs only if you set one.
- **iOS ATT:** the SDK never shows the App Tracking Transparency prompt. To ask for it, enter a *Tracking usage
  description* (written to `NSUserTrackingUsageDescription`) and call
  `Adfacten.RequestTrackingAuthorization(status => …)`. Without permission the SDK uses the IDFV or a random
  install id.
- **iOS privacy manifest:** `PrivacyInfo.xcprivacy` ships inside the AdfactenAds framework (or the pod's resource
  bundle); Xcode includes it in the app's privacy report. Nothing to copy.
- **SKAdNetwork:** not used yet; no `SKAdNetworkItems` entries are needed.
- Clicks open in the browser or the store, never inside your game.

## 11. Android build

- *Video ads* (`com.adfacten:ads-video`) need **Minimum API Level 23**; the build stops with a clear message if it
  is lower. Turn video off in Project Settings > Adfacten if you must support API 21–22.
- The optional artifacts chosen in Project Settings > Adfacten are written to
  `Assets/Adfacten/Editor/AdfactenOptionalDependencies.xml` for EDM4U. Commit that file.
- With EDM4U, enable *Custom Main Gradle Template* (and *Custom Gradle Settings Template* on Unity 2022.2+) in
  Player Settings > Publishing Settings, as EDM4U suggests, so the artifacts resolve with Gradle.
- The bridge is Java source (`Plugins/Android`), compiled by Unity's Gradle build. No ProGuard rules needed: the
  AARs ship their own.
- The SDK depends on `kotlin-stdlib` 2.2. If another plugin pulls an old `kotlin-stdlib-jdk7/jdk8` and Gradle reports
  duplicate classes, add `implementation 'org.jetbrains.kotlin:kotlin-stdlib-jdk8:2.2.10'` to align them.

### Android without EDM4U

With *Add Gradle dependencies without EDM4U* on (default), the plugin adds the repository and artifacts to the
generated Gradle project when nothing else did. To manage it yourself, turn that off and add to your templates:

`settingsTemplate.gradle` (Unity 2022.2+), inside `dependencyResolutionManagement { repositories { … } }`, or
`baseProjectTemplate.gradle` (2021.3), inside `allprojects { repositories { … } }`:

```groovy
maven { url 'https://cdn.adfacten.com/maven/' }
```

`mainTemplate.gradle`, inside `dependencies { … }`:

```groovy
implementation 'com.adfacten:ads:1.0.2'
implementation 'com.adfacten:ads-video:1.0.2' // optional, API 23+
implementation 'com.adfacten:ads-gaid:1.0.2'  // optional, AD_ID permission
```

## <a id="ios-build"></a>12. iOS build

*iOS dependency* in Project Settings > Adfacten:

- **CocoaPods** (default): EDM4U adds `AdfactenAds` to the Podfile and the plugin points it at the source pod
  (`pod 'AdfactenAds', :git => 'https://github.com/adfacten/adfacten-ios-sdk.git', :tag => '1.0.2'`; it is not on the
  CocoaPods trunk) before `pod install`. Keep EDM4U's *Link frameworks statically* and *Swift framework support*
  options on (their defaults). Open the generated `.xcworkspace`.
- **XCFramework**: the build downloads `AdfactenAds-1.0.2.xcframework.zip` from the Adfacten CDN, checks its SHA-256
  (`a7ce159e…bc9ce7d`), caches it in `Library/Adfacten` and embeds it. No CocoaPods needed. This is also what
  happens when the build has no Podfile (no EDM4U). For offline builds put the zip in `Library/Adfacten`.

The build step also links the system frameworks the SDK uses (WebKit, AVFoundation, StoreKit, AdSupport,
AppTrackingTransparency, Network, SwiftUI) and raises the deployment target to iOS 15.0 when it is lower.

## 13. Troubleshooting

- `rejected` with `bundle_mismatch` / `platform_mismatch`: the package name or bundle id doesn't match the app key,
  or you used the Android key on iOS.
- `rejected` with `app_not_approved`: the app gets test ads only until it is approved in the panel.
- Nothing on screen in the Editor: call `Adfacten.Initialize` first; loads before it fail with `not_initialized`.
- Native logs: set *Native log level* to Debug. Android: Logcat tag `Adfacten`. iOS: Console, subsystem
  `com.adfacten.ads`.

## License

Apache License 2.0. See [LICENSE](LICENSE).
