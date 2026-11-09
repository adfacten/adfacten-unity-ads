package com.adfacten.unity;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.Outline;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewOutlineProvider;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import com.adfacten.ads.NativeAd;
import com.adfacten.ads.NativeAdView;

import org.json.JSONObject;

/**
 * A native ad laid out from the small or medium template inside the SDK's NativeAdView, placed in the overlay at
 * a Unity screen rectangle. The SDK view records the impression, the viewable impression and clicks; this class
 * only builds the layout. UI thread only.
 */
final class UnityNativeTemplate implements Overlay.LayoutListener {
    static final int SMALL = 0;
    static final int MEDIUM = 1;

    private final Activity activity;
    private final Overlay overlay;
    private final NativeAd ad;
    private final float density;
    private NativeAdView adView;
    private int template = -1;
    private String styleJson = "";
    private float x, y, width, height, screenWidth;

    UnityNativeTemplate(Activity activity, NativeAd ad) {
        this.activity = activity;
        this.overlay = Overlay.of(activity);
        this.ad = ad;
        this.density = activity.getResources().getDisplayMetrics().density;
    }

    void show(int template, float x, float y, float width, float height, float screenWidth, String styleJson) {
        String style = styleJson == null ? "" : styleJson;
        if (adView == null || template != this.template || !style.equals(this.styleJson)) {
            removeView();
            this.template = template;
            this.styleJson = style;
            adView = build(template == SMALL, new Style(style));
            overlay.root.addView(adView, new FrameLayout.LayoutParams(1, 1));
            overlay.addLayoutListener(this);
            // Binding again (or to a new view) never records a second impression.
            adView.setNativeAd(ad);
        }
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        this.screenWidth = screenWidth;
        adView.setVisibility(View.VISIBLE);
        applyRect();
    }

    void hide() {
        if (adView != null) adView.setVisibility(View.GONE);
    }

    void destroy() {
        removeView();
        ad.destroy();
    }

    @Override
    public void onOverlayLayout() {
        applyRect();
    }

    private void removeView() {
        if (adView == null) return;
        overlay.removeLayoutListener(this);
        if (adView.getNativeAd() != null) adView.setNativeAd(null);
        ViewGroup parent = (ViewGroup) adView.getParent();
        if (parent != null) parent.removeView(adView);
        adView = null;
    }

    private void applyRect() {
        if (adView == null) return;
        float scale = overlay.scale(screenWidth);
        FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) adView.getLayoutParams();
        lp.gravity = Gravity.TOP | Gravity.LEFT;
        lp.leftMargin = Math.round(x * scale);
        lp.topMargin = Math.round(y * scale);
        lp.width = Math.max(1, Math.round(width * scale));
        lp.height = Math.max(1, Math.round(height * scale));
        adView.setLayoutParams(lp);
    }

    // Templates

    private NativeAdView build(boolean small, Style style) {
        NativeAdView view = new NativeAdView(activity);
        View content = small ? buildSmall(view, style) : buildMedium(view, style);
        view.addView(content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        return view;
    }

    private View buildMedium(NativeAdView view, Style style) {
        LinearLayout root = column();
        root.setPadding(dp(12), dp(12), dp(12), dp(12));
        root.setBackground(rounded(style.background, style.cornerRadius));
        clipRounded(root, style.cornerRadius);

        LinearLayout header = row();
        ImageView icon = imageView(8f);
        LinearLayout.LayoutParams iconLp = new LinearLayout.LayoutParams(dp(40), dp(40));
        iconLp.setMarginEnd(dp(10));
        header.addView(icon, iconLp);
        LinearLayout names = column();
        TextView title = text(15f, style.title, true, 2);
        TextView brand = text(12f, style.brand, false, 1);
        names.addView(title);
        names.addView(brand);
        header.addView(names, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        TextView label = adLabel(style);
        LinearLayout.LayoutParams labelLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        labelLp.setMarginStart(dp(8));
        labelLp.gravity = Gravity.TOP;
        header.addView(label, labelLp);
        root.addView(header);

        // The image takes the height left over; without an image a spacer keeps the button at the bottom.
        boolean hasImage = ad.getImage() != null;
        ImageView image = imageView(8f);
        LinearLayout.LayoutParams imageLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f);
        imageLp.topMargin = dp(10);
        root.addView(image, imageLp);
        if (!hasImage) image.setVisibility(View.GONE);

        TextView body = text(13f, style.body, false, 3);
        LinearLayout.LayoutParams bodyLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        bodyLp.topMargin = dp(8);
        root.addView(body, bodyLp);
        if (!hasImage) root.addView(new View(activity), new LinearLayout.LayoutParams(1, 0, 1f));

        TextView cta = callToAction(style);
        LinearLayout.LayoutParams ctaLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(40));
        ctaLp.topMargin = dp(10);
        root.addView(cta, ctaLp);

        view.setTitleView(title);
        view.setBrandView(brand);
        view.setBodyView(body);
        view.setIconView(icon);
        if (hasImage) view.setImageView(image);
        view.setCallToActionView(cta);
        view.setAdLabelView(label);
        if (ad.getIcon() == null) icon.setVisibility(View.GONE);
        return root;
    }

    private View buildSmall(NativeAdView view, Style style) {
        LinearLayout root = row();
        root.setPadding(dp(12), dp(10), dp(12), dp(10));
        root.setBackground(rounded(style.background, style.cornerRadius));
        clipRounded(root, style.cornerRadius);

        ImageView thumb = imageView(8f);
        LinearLayout.LayoutParams thumbLp = new LinearLayout.LayoutParams(dp(56), dp(56));
        thumbLp.setMarginEnd(dp(10));
        root.addView(thumb, thumbLp);
        if (ad.getIcon() != null) view.setIconView(thumb);
        else if (ad.getImage() != null) view.setImageView(thumb);
        else thumb.setVisibility(View.GONE);

        LinearLayout texts = column();
        LinearLayout top = row();
        TextView label = adLabel(style);
        top.addView(label);
        TextView brand = text(12f, style.brand, false, 1);
        LinearLayout.LayoutParams brandLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        brandLp.setMarginStart(dp(6));
        top.addView(brand, brandLp);
        texts.addView(top);
        TextView title = text(14f, style.title, true, 2);
        LinearLayout.LayoutParams titleLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        titleLp.topMargin = dp(2);
        texts.addView(title, titleLp);
        TextView body = text(12f, style.body, false, 1);
        texts.addView(body);
        root.addView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        TextView cta = callToAction(style);
        cta.setPadding(dp(14), 0, dp(14), 0);
        LinearLayout.LayoutParams ctaLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(36));
        ctaLp.setMarginStart(dp(10));
        root.addView(cta, ctaLp);

        view.setTitleView(title);
        view.setBrandView(brand);
        view.setBodyView(body);
        view.setCallToActionView(cta);
        view.setAdLabelView(label);
        return root;
    }

    // Building blocks

    private int dp(int v) {
        return Math.round(v * density);
    }

    private LinearLayout column() {
        LinearLayout l = new LinearLayout(activity);
        l.setOrientation(LinearLayout.VERTICAL);
        return l;
    }

    private LinearLayout row() {
        LinearLayout l = new LinearLayout(activity);
        l.setOrientation(LinearLayout.HORIZONTAL);
        l.setGravity(Gravity.CENTER_VERTICAL);
        return l;
    }

    private GradientDrawable rounded(int color, float radiusDp) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(color);
        d.setCornerRadius(radiusDp * density);
        return d;
    }

    private void clipRounded(View view, float radiusDp) {
        final float r = radiusDp * density;
        view.setOutlineProvider(new ViewOutlineProvider() {
            @Override
            public void getOutline(View v, Outline outline) {
                outline.setRoundRect(0, 0, v.getWidth(), v.getHeight(), r);
            }
        });
        view.setClipToOutline(r > 0);
    }

    private ImageView imageView(float radiusDp) {
        ImageView v = new ImageView(activity);
        v.setScaleType(ImageView.ScaleType.CENTER_CROP);
        clipRounded(v, radiusDp);
        return v;
    }

    private TextView text(float sizeSp, int color, boolean bold, int lines) {
        TextView t = new TextView(activity);
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, sizeSp);
        t.setTextColor(color);
        if (bold) t.setTypeface(Typeface.DEFAULT_BOLD);
        t.setMaxLines(lines);
        t.setEllipsize(TextUtils.TruncateAt.END);
        return t;
    }

    private TextView adLabel(Style style) {
        TextView t = text(10f, style.labelText, true, 1);
        t.setPadding(dp(5), dp(1), dp(5), dp(1));
        t.setBackground(rounded(style.labelBackground, 4f));
        return t;
    }

    private TextView callToAction(Style style) {
        TextView t = text(14f, style.ctaText, true, 1);
        t.setGravity(Gravity.CENTER);
        t.setBackground(rounded(style.ctaBackground, style.ctaCornerRadius));
        return t;
    }

    /** Colors (0xAARRGGBB) and radii (dp) sent from C#; unset values use the light theme. */
    static final class Style {
        final int background;
        final int title;
        final int body;
        final int brand;
        final int ctaBackground;
        final int ctaText;
        final int labelBackground;
        final int labelText;
        final float cornerRadius;
        final float ctaCornerRadius;

        Style(String json) {
            JSONObject o;
            try {
                o = new JSONObject(json == null || json.isEmpty() ? "{}" : json);
            } catch (Exception e) {
                o = new JSONObject();
            }
            background = color(o, "backgroundColor", Color.WHITE);
            title = color(o, "titleColor", 0xFF1B1B1F);
            body = color(o, "bodyColor", 0xFF46464F);
            brand = color(o, "brandColor", 0xFF46464F);
            ctaBackground = color(o, "callToActionBackgroundColor", 0xFF3F51B5);
            ctaText = color(o, "callToActionTextColor", Color.WHITE);
            labelBackground = color(o, "adLabelBackgroundColor", 0xFFFFE082);
            labelText = color(o, "adLabelTextColor", 0xFF3E2E00);
            cornerRadius = (float) o.optDouble("cornerRadius", 12);
            ctaCornerRadius = (float) o.optDouble("callToActionCornerRadius", 20);
        }

        private static int color(JSONObject o, String key, int fallback) {
            return o.has(key) ? (int) o.optLong(key, fallback) : fallback;
        }
    }
}
