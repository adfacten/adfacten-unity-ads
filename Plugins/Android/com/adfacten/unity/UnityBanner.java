package com.adfacten.unity;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;

import com.adfacten.ads.AdError;
import com.adfacten.ads.AdSize;
import com.adfacten.ads.AdfactenBannerView;
import com.adfacten.ads.BannerAdListener;

/** One banner in the overlay. UI thread only. */
final class UnityBanner implements Overlay.LayoutListener {
    static final int TOP = 0;
    static final int BOTTOM = 1;
    static final int CENTER = 2;
    static final int TOP_LEFT = 3;
    static final int TOP_RIGHT = 4;
    static final int BOTTOM_LEFT = 5;
    static final int BOTTOM_RIGHT = 6;
    static final int CUSTOM = 7;

    private final int handle;
    private final Overlay overlay;
    private final AdfactenBannerView view;
    private int position = BOTTOM;
    private float x;
    private float y;
    private float screenWidth;
    private boolean visible = true;

    UnityBanner(Activity activity, final int handle, String placementId, int sizeIndex) {
        this.handle = handle;
        overlay = Overlay.of(activity);
        view = new AdfactenBannerView(activity);
        view.setPlacementId(placementId);
        view.setAdSize(sizeOf(sizeIndex));
        view.setListener(new BannerAdListener() {
            @Override public void onLoaded() { AdfactenUnityBridge.emit(handle, "loaded", null); }
            @Override public void onFailed(AdError error) { AdfactenUnityBridge.emit(handle, "failed", AdfactenUnityBridge.errorJson(error)); }
            @Override public void onShown() { AdfactenUnityBridge.emit(handle, "shown", null); }
            @Override public void onClicked() { AdfactenUnityBridge.emit(handle, "clicked", null); }
        });
        overlay.root.addView(view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        overlay.addLayoutListener(this);
        applyPosition();
    }

    static AdSize sizeOf(int index) {
        switch (index) {
            case 1: return AdSize.LARGE_BANNER;
            case 2: return AdSize.MEDIUM_RECTANGLE;
            case 3: return AdSize.LEADERBOARD;
            default: return AdSize.BANNER;
        }
    }

    void setPosition(int position, float x, float y, float screenWidth) {
        this.position = position;
        this.x = x;
        this.y = y;
        this.screenWidth = screenWidth;
        applyPosition();
    }

    void load() {
        view.load();
    }

    void setVisible(boolean visible) {
        this.visible = visible;
        view.setVisibility(visible ? View.VISIBLE : View.GONE);
        if (visible) view.resume(); else view.pause();
    }

    /** Width and height of a banner size in Unity screen pixels. Any thread. */
    static float[] sizeInUnityPixels(Activity activity, int sizeIndex, float unityScreenWidth) {
        AdSize size = sizeOf(sizeIndex);
        float density = activity.getResources().getDisplayMetrics().density;
        View content = activity.findViewById(android.R.id.content);
        int viewWidth = content != null && content.getWidth() > 0 ? content.getWidth() : activity.getResources().getDisplayMetrics().widthPixels;
        float toUnity = unityScreenWidth > 0 ? unityScreenWidth / viewWidth : 1f;
        return new float[] { size.getWidth() * density * toUnity, size.getHeight() * density * toUnity };
    }

    void destroy() {
        overlay.removeLayoutListener(this);
        view.destroy();
        ViewGroup parent = (ViewGroup) view.getParent();
        if (parent != null) parent.removeView(view);
    }

    @Override
    public void onOverlayLayout() {
        applyPosition();
    }

    private void applyPosition() {
        FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) view.getLayoutParams();
        if (lp == null) return;
        lp.leftMargin = lp.topMargin = lp.rightMargin = lp.bottomMargin = 0;
        if (position == CUSTOM) {
            float scale = overlay.scale(screenWidth);
            lp.gravity = Gravity.TOP | Gravity.LEFT;
            lp.leftMargin = Math.round(x * scale);
            lp.topMargin = Math.round(y * scale);
        } else {
            int[] inset = overlay.safeInsets();
            int vertical;
            int horizontal;
            switch (position) {
                case TOP: vertical = Gravity.TOP; horizontal = Gravity.CENTER_HORIZONTAL; break;
                case CENTER: vertical = Gravity.CENTER_VERTICAL; horizontal = Gravity.CENTER_HORIZONTAL; break;
                case TOP_LEFT: vertical = Gravity.TOP; horizontal = Gravity.LEFT; break;
                case TOP_RIGHT: vertical = Gravity.TOP; horizontal = Gravity.RIGHT; break;
                case BOTTOM_LEFT: vertical = Gravity.BOTTOM; horizontal = Gravity.LEFT; break;
                case BOTTOM_RIGHT: vertical = Gravity.BOTTOM; horizontal = Gravity.RIGHT; break;
                default: vertical = Gravity.BOTTOM; horizontal = Gravity.CENTER_HORIZONTAL; break;
            }
            lp.gravity = vertical | horizontal;
            lp.leftMargin = inset[0];
            lp.rightMargin = inset[2];
            if (vertical == Gravity.TOP) lp.topMargin = inset[1];
            if (vertical == Gravity.BOTTOM) lp.bottomMargin = inset[3];
        }
        view.setLayoutParams(lp);
        view.setVisibility(visible ? View.VISIBLE : View.GONE);
    }
}
