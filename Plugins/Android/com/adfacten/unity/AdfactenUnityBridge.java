package com.adfacten.unity;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import com.adfacten.ads.AdError;
import com.adfacten.ads.AdLoadCallback;
import com.adfacten.ads.AdReward;
import com.adfacten.ads.Adfacten;
import com.adfacten.ads.AdfactenConfig;
import com.adfacten.ads.AppOpenAd;
import com.adfacten.ads.FullScreenAd;
import com.adfacten.ads.FullScreenAdListener;
import com.adfacten.ads.InterstitialAd;
import com.adfacten.ads.LogLevel;
import com.adfacten.ads.NativeAd;
import com.adfacten.ads.NativeAdListener;
import com.adfacten.ads.OnUserEarnedRewardListener;
import com.adfacten.ads.RewardedAd;

import org.json.JSONObject;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Static entry points called from C# (AndroidClient). Every SDK and view call runs on the UI thread; results go
 * back through {@link AdfactenUnityListener}. Objects are keyed by handles chosen in C#.
 * Strings from C# are never null: "" means "not set".
 */
public final class AdfactenUnityBridge {
    private static final String TAG = "AdfactenUnity";
    private static final int INTERSTITIAL = 0;
    private static final int REWARDED = 1;
    private static final int APP_OPEN = 2;

    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static volatile AdfactenUnityListener listener;

    private static final Map<Integer, UnityBanner> banners = new ConcurrentHashMap<>();
    // Written on the calling thread so the size is known before the view exists on the UI thread.
    private static final Map<Integer, Integer> bannerSizes = new ConcurrentHashMap<>();
    private static volatile Activity bannerActivity;
    private static final Map<Integer, FullScreenAd> fullScreenAds = new ConcurrentHashMap<>();
    private static final Map<Integer, Boolean> shown = new ConcurrentHashMap<>();
    private static final Map<Integer, Boolean> loading = new ConcurrentHashMap<>();
    private static final Map<Integer, NativeAd> nativeAds = new ConcurrentHashMap<>();
    private static final Map<Integer, UnityNativeTemplate> nativeViews = new ConcurrentHashMap<>();

    private AdfactenUnityBridge() {
    }

    // SDK

    public static void setListener(AdfactenUnityListener l) {
        listener = l;
    }

    public static void setWrapper(String name, String version) {
        Adfacten.setWrapper(name, version);
    }

    public static void initialize(final Activity activity, final String appKey, final boolean testMode, final int logLevel,
                                  final boolean childDirected, final String userId) {
        final AdfactenConfig config = new AdfactenConfig.Builder()
                .setTestMode(testMode)
                .setLogLevel(logLevelOf(logLevel))
                .setChildDirected(childDirected)
                .setUserId(emptyToNull(userId))
                .build();
        ui(new Runnable() {
            @Override
            public void run() {
                Adfacten.initialize(activity.getApplicationContext(), appKey, config);
            }
        });
    }

    public static void setUserId(final String userId) {
        ui(new Runnable() {
            @Override
            public void run() {
                Adfacten.setUserId(emptyToNull(userId));
            }
        });
    }

    public static String sdkVersion() {
        return Adfacten.getSdkVersion();
    }

    public static boolean isInitialized() {
        return Adfacten.isInitialized();
    }

    // Banner

    public static void bannerCreate(final Activity activity, final int handle, final String placementId, final int sizeIndex) {
        bannerSizes.put(handle, sizeIndex);
        bannerActivity = activity;
        ui(new Runnable() {
            @Override
            public void run() {
                UnityBanner old = banners.remove(handle);
                if (old != null) old.destroy();
                banners.put(handle, new UnityBanner(activity, handle, placementId, sizeIndex));
            }
        });
    }

    public static void bannerSetPosition(final int handle, final int position, final float x, final float y,
                                         final int screenWidth, final int screenHeight) {
        ui(new Runnable() {
            @Override
            public void run() {
                UnityBanner b = banners.get(handle);
                if (b != null) b.setPosition(position, x, y, screenWidth);
            }
        });
    }

    public static void bannerLoad(final int handle) {
        ui(new Runnable() {
            @Override
            public void run() {
                UnityBanner b = banners.get(handle);
                if (b != null) b.load();
            }
        });
    }

    public static void bannerSetVisible(final int handle, final boolean visible) {
        ui(new Runnable() {
            @Override
            public void run() {
                UnityBanner b = banners.get(handle);
                if (b != null) b.setVisible(visible);
            }
        });
    }

    public static void bannerDestroy(final int handle) {
        ui(new Runnable() {
            @Override
            public void run() {
                UnityBanner b = banners.remove(handle);
                if (b != null) b.destroy();
            }
        });
        bannerSizes.remove(handle);
    }

    /** Width and height in Unity screen pixels, or null. */
    public static float[] bannerSize(int handle, int screenWidth, int screenHeight) {
        Integer index = bannerSizes.get(handle);
        Activity activity = bannerActivity;
        if (index == null || activity == null) return null;
        try {
            return UnityBanner.sizeInUnityPixels(activity, index, screenWidth);
        } catch (Throwable t) {
            return null;
        }
    }

    // Interstitial, rewarded, app open

    public static void fullScreenLoad(final Activity activity, final int handle, final int format, final String placementId,
                                      final String customData) {
        ui(new Runnable() {
            @Override
            public void run() {
                loading.put(handle, Boolean.TRUE);
                switch (format) {
                    case REWARDED:
                        RewardedAd.load(activity, placementId, new Loaded<RewardedAd>(handle), emptyToNull(customData));
                        break;
                    case APP_OPEN:
                        AppOpenAd.load(activity, placementId, new Loaded<AppOpenAd>(handle));
                        break;
                    default:
                        InterstitialAd.load(activity, placementId, new Loaded<InterstitialAd>(handle));
                        break;
                }
            }
        });
    }

    public static void fullScreenShow(final Activity activity, final int handle) {
        ui(new Runnable() {
            @Override
            public void run() {
                FullScreenAd ad = fullScreenAds.get(handle);
                if (ad == null) {
                    emit(handle, "failedToShow", errorJson("not_ready", "the ad is not loaded", null, -1));
                    return;
                }
                shown.put(handle, Boolean.TRUE);
                ad.setListener(new FullScreenAdListener() {
                    @Override public void onShown() { emit(handle, "shown", null); }
                    @Override public void onClicked() { emit(handle, "clicked", null); }
                    @Override public void onClosed() {
                        fullScreenAds.remove(handle);
                        shown.remove(handle);
                        emit(handle, "closed", null);
                    }
                    @Override public void onFailedToShow(AdError error) {
                        // "Another ad is on screen" leaves the ad usable; C# allows another show() then.
                        if (error.getCode() == AdError.SHOW_FAILED) shown.remove(handle);
                        emit(handle, "failedToShow", errorJson(error));
                    }
                });
                if (ad instanceof RewardedAd) {
                    ((RewardedAd) ad).show(activity, new OnUserEarnedRewardListener() {
                        @Override
                        public void onUserEarnedReward(AdReward reward) {
                            emit(handle, "reward", json("type", reward.getType(), "amount", reward.getAmount()));
                        }
                    });
                } else if (ad instanceof AppOpenAd) {
                    ((AppOpenAd) ad).show(activity);
                } else if (ad instanceof InterstitialAd) {
                    ((InterstitialAd) ad).show(activity);
                }
            }
        });
    }

    public static boolean fullScreenIsReady(int handle) {
        FullScreenAd ad = fullScreenAds.get(handle);
        return ad != null && !shown.containsKey(handle) && !ad.isExpired();
    }

    public static void fullScreenDestroy(final int handle) {
        ui(new Runnable() {
            @Override
            public void run() {
                loading.remove(handle);
                // An ad on screen keeps running until the user closes it.
                if (shown.containsKey(handle)) return;
                FullScreenAd ad = fullScreenAds.remove(handle);
                if (ad != null) ad.setListener(null);
            }
        });
    }

    private static final class Loaded<T extends FullScreenAd> implements AdLoadCallback<T> {
        private final int handle;

        Loaded(int handle) {
            this.handle = handle;
        }

        @Override
        public void onLoaded(T ad) {
            // Destroyed from C# while loading: drop the ad.
            if (loading.remove(handle) == null) return;
            fullScreenAds.put(handle, ad);
            String payload = null;
            if (ad instanceof RewardedAd) {
                AdReward reward = ((RewardedAd) ad).getReward();
                if (reward != null) payload = json("rewardType", reward.getType(), "rewardAmount", reward.getAmount());
            }
            emit(handle, "loaded", payload);
        }

        @Override
        public void onFailed(AdError error) {
            if (loading.remove(handle) == null) return;
            emit(handle, "failed", errorJson(error));
        }
    }

    // Native

    public static void nativeLoad(final Activity activity, final int handle, final String placementId) {
        ui(new Runnable() {
            @Override
            public void run() {
                loading.put(handle, Boolean.TRUE);
                NativeAd.load(activity, placementId, new AdLoadCallback<NativeAd>() {
                    @Override
                    public void onLoaded(NativeAd ad) {
                        if (loading.remove(handle) == null) {
                            ad.destroy();
                            return;
                        }
                        ad.setListener(new NativeAdListener() {
                            @Override public void onShown() { emit(handle, "shown", null); }
                            @Override public void onClicked() { emit(handle, "clicked", null); }
                        });
                        nativeAds.put(handle, ad);
                        JSONObject o = new JSONObject();
                        try {
                            o.put("title", ad.getTitle());
                            o.put("body", ad.getBody());
                            o.put("brand", ad.getBrand());
                            o.put("callToAction", ad.getCallToAction());
                            o.put("adLabel", ad.getAdLabel());
                            o.put("hasImage", ad.getImage() != null);
                            o.put("hasIcon", ad.getIcon() != null);
                            o.put("imageWidth", ad.getImageWidth());
                            o.put("imageHeight", ad.getImageHeight());
                        } catch (Exception ignored) {
                        }
                        emit(handle, "loaded", o.toString());
                    }

                    @Override
                    public void onFailed(AdError error) {
                        if (loading.remove(handle) == null) return;
                        emit(handle, "failed", errorJson(error));
                    }
                });
            }
        });
    }

    public static void nativeShow(final Activity activity, final int handle, final int template, final float x, final float y,
                                  final float width, final float height, final int screenWidth, final int screenHeight,
                                  final String styleJson) {
        ui(new Runnable() {
            @Override
            public void run() {
                NativeAd ad = nativeAds.get(handle);
                if (ad == null) return;
                UnityNativeTemplate view = nativeViews.get(handle);
                if (view == null) {
                    view = new UnityNativeTemplate(activity, ad);
                    nativeViews.put(handle, view);
                }
                view.show(template, x, y, width, height, screenWidth, styleJson);
            }
        });
    }

    public static void nativeHide(final int handle) {
        ui(new Runnable() {
            @Override
            public void run() {
                UnityNativeTemplate view = nativeViews.get(handle);
                if (view != null) view.hide();
            }
        });
    }

    public static void nativeDestroy(final int handle) {
        ui(new Runnable() {
            @Override
            public void run() {
                loading.remove(handle);
                UnityNativeTemplate view = nativeViews.remove(handle);
                NativeAd ad = nativeAds.remove(handle);
                if (view != null) view.destroy();
                else if (ad != null) ad.destroy();
                if (ad != null) ad.setListener(null);
            }
        });
    }

    // Helpers

    static void emit(int handle, String name, String payload) {
        AdfactenUnityListener l = listener;
        if (l == null) return;
        try {
            l.onEvent(handle, name, payload == null ? "{}" : payload);
        } catch (Throwable t) {
            Log.w(TAG, "event " + name + " could not be delivered", t);
        }
    }

    static String errorJson(AdError error) {
        return errorJson(error.getName(), error.getMessage(), error.getServerCode(), error.getCode());
    }

    static String errorJson(String name, String message, String serverCode, int nativeCode) {
        JSONObject o = new JSONObject();
        try {
            o.put("name", name);
            o.put("message", message);
            if (serverCode != null) o.put("serverCode", serverCode);
            o.put("nativeCode", nativeCode);
        } catch (Exception ignored) {
        }
        return o.toString();
    }

    private static String json(String k1, Object v1, String k2, Object v2) {
        JSONObject o = new JSONObject();
        try {
            o.put(k1, v1);
            o.put(k2, v2);
        } catch (Exception ignored) {
        }
        return o.toString();
    }

    private static String emptyToNull(String s) {
        return s == null || s.isEmpty() ? null : s;
    }

    private static LogLevel logLevelOf(int value) {
        switch (value) {
            case 0: return LogLevel.NONE;
            case 1: return LogLevel.ERROR;
            case 3: return LogLevel.INFO;
            case 4: return LogLevel.DEBUG;
            default: return LogLevel.WARN;
        }
    }

    /** Runs on the UI thread; SDK errors never reach Unity. */
    private static void ui(final Runnable r) {
        Runnable safe = new Runnable() {
            @Override
            public void run() {
                try {
                    r.run();
                } catch (Throwable t) {
                    Log.e(TAG, "bridge call failed", t);
                }
            }
        };
        if (Looper.myLooper() == Looper.getMainLooper()) safe.run();
        else MAIN.post(safe);
    }
}
