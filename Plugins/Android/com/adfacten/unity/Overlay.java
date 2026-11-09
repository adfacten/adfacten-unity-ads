package com.adfacten.unity;

import android.app.Activity;
import android.os.Build;
import android.view.DisplayCutout;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;

/**
 * A full-size, touch-transparent layer above the Unity view that holds banners and native templates.
 * Converts Unity screen pixels (top-left origin) to view pixels and tracks the safe area.
 */
final class Overlay {
    interface LayoutListener {
        void onOverlayLayout();
    }

    private static Overlay current;

    final Activity activity;
    final FrameLayout root;
    private final java.util.ArrayList<LayoutListener> listeners = new java.util.ArrayList<>();

    private Overlay(Activity activity) {
        this.activity = activity;
        root = new FrameLayout(activity);
        root.setClipChildren(false);
        // Not clickable: touches outside the ads go through to the game.
        root.setClickable(false);
        root.setFocusable(false);
        activity.addContentView(root, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        root.addOnLayoutChangeListener(new View.OnLayoutChangeListener() {
            @Override
            public void onLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob) {
                if (r - l != or - ol || b - t != ob - ot) {
                    for (LayoutListener listener : new java.util.ArrayList<>(listeners)) listener.onOverlayLayout();
                }
            }
        });
    }

    /** The overlay of {@code activity}, created on first use. UI thread only. */
    static Overlay of(Activity activity) {
        if (current == null || current.activity != activity || current.root.getParent() == null) {
            current = new Overlay(activity);
        }
        return current;
    }

    void addLayoutListener(LayoutListener listener) {
        if (!listeners.contains(listener)) listeners.add(listener);
    }

    void removeLayoutListener(LayoutListener listener) {
        listeners.remove(listener);
    }

    int width() {
        int w = root.getWidth();
        if (w <= 0) {
            View content = activity.findViewById(android.R.id.content);
            w = content != null ? content.getWidth() : 0;
        }
        if (w <= 0) w = activity.getResources().getDisplayMetrics().widthPixels;
        return w;
    }

    int height() {
        int h = root.getHeight();
        if (h <= 0) {
            View content = activity.findViewById(android.R.id.content);
            h = content != null ? content.getHeight() : 0;
        }
        if (h <= 0) h = activity.getResources().getDisplayMetrics().heightPixels;
        return h;
    }

    /** View pixels per Unity screen pixel. */
    float scale(float unityScreenWidth) {
        return unityScreenWidth > 0 ? width() / unityScreenWidth : 1f;
    }

    float density() {
        return activity.getResources().getDisplayMetrics().density;
    }

    /** Safe-area insets of the overlay in view pixels: left, top, right, bottom. */
    @SuppressWarnings("deprecation")
    int[] safeInsets() {
        int[] out = new int[4];
        if (Build.VERSION.SDK_INT < 23) return out;
        WindowInsets insets = root.getRootWindowInsets();
        if (insets == null) return out;
        out[0] = insets.getSystemWindowInsetLeft();
        out[1] = insets.getSystemWindowInsetTop();
        out[2] = insets.getSystemWindowInsetRight();
        out[3] = insets.getSystemWindowInsetBottom();
        if (Build.VERSION.SDK_INT >= 28) {
            DisplayCutout cutout = insets.getDisplayCutout();
            if (cutout != null) {
                out[0] = Math.max(out[0], cutout.getSafeInsetLeft());
                out[1] = Math.max(out[1], cutout.getSafeInsetTop());
                out[2] = Math.max(out[2], cutout.getSafeInsetRight());
                out[3] = Math.max(out[3], cutout.getSafeInsetBottom());
            }
        }
        // The insets are relative to the window; drop the part the overlay does not cover.
        View window = root.getRootView();
        int[] loc = new int[2];
        root.getLocationInWindow(loc);
        out[0] = Math.max(0, out[0] - loc[0]);
        out[1] = Math.max(0, out[1] - loc[1]);
        out[2] = Math.max(0, out[2] - (window.getWidth() - loc[0] - root.getWidth()));
        out[3] = Math.max(0, out[3] - (window.getHeight() - loc[1] - root.getHeight()));
        return out;
    }
}
