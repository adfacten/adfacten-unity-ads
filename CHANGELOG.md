# Changelog

## [1.0.0] - 2026-11-09

First release.

- Wraps the native Adfacten SDKs 1.0.2 (Android `com.adfacten:ads`, iOS `AdfactenAds`) and reports itself as
  `unity` / `1.0.0` to the ad server.
- Banner (`BannerView`): 320x50, 320x100, 300x250, 728x90; top, bottom, center, corners or a custom position.
- Interstitial, rewarded (custom data, `OnUserEarnedReward`) and app-open ads.
- Native ads in small and medium native templates, placed over the game at a screen rectangle.
- Stable error names (`AdError.Name`, `AdError.Code`) and server codes, the same on Android and iOS.
- All callbacks on the Unity main thread.
- Editor mock with placeholder ads and the same events as the device SDKs.
- Project Settings > Adfacten: app keys, test mode in development builds, child-directed, Android video and
  advertising ID options, iOS dependency mode and ATT usage description.
- Dependencies through the External Dependency Manager, with a Gradle fallback on Android and the checksum-verified
  XCFramework on iOS.
- Demo sample preconfigured with the public test apps.
