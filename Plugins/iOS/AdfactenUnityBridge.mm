// Unity bridge for the Adfacten iOS SDK. C# calls the extern "C" functions below (DllImport "__Internal");
// events go back through one callback. Everything runs on the main thread, which is Unity's thread on iOS.

#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#if __has_include(<AdfactenAds/AdfactenAds-Swift.h>)
#import <AdfactenAds/AdfactenAds-Swift.h>
#else
#import "AdfactenAds-Swift.h"
#endif

// From Unity's UnityInterface.h.
extern "C" UIViewController *UnityGetGLViewController(void);
extern "C" void UnityPause(int pause);

typedef void (*AdfactenUnityEventCallback)(int handle, const char *name, const char *payload);

static AdfactenUnityEventCallback gEventCallback = NULL;
static NSMutableDictionary<NSNumber *, id> *gObjects = nil;

static NSMutableDictionary<NSNumber *, id> *Objects(void)
{
    if (gObjects == nil) gObjects = [NSMutableDictionary dictionary];
    return gObjects;
}

static NSString *ToNSString(const char *s)
{
    return s ? [NSString stringWithUTF8String:s] : nil;
}

static NSString *NonEmpty(const char *s)
{
    NSString *v = ToNSString(s);
    return v.length > 0 ? v : nil;
}

static void Emit(int handle, NSString *name, NSDictionary *payload)
{
    AdfactenUnityEventCallback cb = gEventCallback;
    if (cb == NULL) return;
    NSString *json = @"{}";
    if (payload.count > 0) {
        NSData *data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
        if (data) json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    }
    cb(handle, name.UTF8String, json.UTF8String);
}

static NSDictionary *ErrorPayload(NSError *error)
{
    NSMutableDictionary *p = [NSMutableDictionary dictionary];
    NSString *name = nil;
    if ([error.domain isEqualToString:@"com.adfacten.ads"]) {
        id value = error.userInfo[ADFAdfacten.errorNameKey];
        if ([value isKindOfClass:[NSString class]] && [value length] > 0) name = value;
    }
    p[@"name"] = name ?: @"internal_error";
    p[@"message"] = error.localizedDescription ?: @"unknown error";
    id serverCode = error.userInfo[@"serverCode"];
    if ([serverCode isKindOfClass:[NSString class]] && [serverCode length] > 0) p[@"serverCode"] = serverCode;
    p[@"nativeCode"] = @(error.code);
    return p;
}

static NSDictionary *SimpleError(NSString *name, NSString *message)
{
    return @{ @"name": name, @"message": message };
}

static UIView *RootView(void)
{
    return UnityGetGLViewController().view;
}

/// Points per Unity screen pixel.
static CGFloat Scale(CGFloat unityScreenWidth)
{
    UIView *root = RootView();
    return unityScreenWidth > 0 && root ? root.bounds.size.width / unityScreenWidth : 1;
}

static void RunOnMain(dispatch_block_t block)
{
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

#pragma mark - Banner

@interface AdfactenUnityBanner : NSObject <ADFBannerViewDelegate>
@property (nonatomic, readonly) int handle;
@property (nonatomic, strong) ADFBannerView *view;
@property (nonatomic, strong) NSArray<NSLayoutConstraint *> *constraints;
@end

@implementation AdfactenUnityBanner

- (instancetype)initWithHandle:(int)handle placementId:(NSString *)placementId size:(ADFBannerSize)size
{
    if ((self = [super init])) {
        _handle = handle;
        _view = [[ADFBannerView alloc] initWithPlacementId:placementId size:size];
        _view.delegate = self;
        _view.rootViewController = UnityGetGLViewController();
        _view.translatesAutoresizingMaskIntoConstraints = NO;
        [RootView() addSubview:_view];
    }
    return self;
}

- (void)setPosition:(int)position x:(CGFloat)x y:(CGFloat)y screenWidth:(CGFloat)screenWidth
{
    UIView *root = self.view.superview;
    if (root == nil) return;
    [NSLayoutConstraint deactivateConstraints:self.constraints ?: @[]];
    UILayoutGuide *safe = root.safeAreaLayoutGuide;
    NSMutableArray *c = [NSMutableArray array];
    UIView *v = self.view;
    switch (position) {
        case 0: // top
            [c addObject:[v.topAnchor constraintEqualToAnchor:safe.topAnchor]];
            [c addObject:[v.centerXAnchor constraintEqualToAnchor:safe.centerXAnchor]];
            break;
        case 2: // center
            [c addObject:[v.centerYAnchor constraintEqualToAnchor:safe.centerYAnchor]];
            [c addObject:[v.centerXAnchor constraintEqualToAnchor:safe.centerXAnchor]];
            break;
        case 3: // top left
            [c addObject:[v.topAnchor constraintEqualToAnchor:safe.topAnchor]];
            [c addObject:[v.leftAnchor constraintEqualToAnchor:safe.leftAnchor]];
            break;
        case 4: // top right
            [c addObject:[v.topAnchor constraintEqualToAnchor:safe.topAnchor]];
            [c addObject:[v.rightAnchor constraintEqualToAnchor:safe.rightAnchor]];
            break;
        case 5: // bottom left
            [c addObject:[v.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor]];
            [c addObject:[v.leftAnchor constraintEqualToAnchor:safe.leftAnchor]];
            break;
        case 6: // bottom right
            [c addObject:[v.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor]];
            [c addObject:[v.rightAnchor constraintEqualToAnchor:safe.rightAnchor]];
            break;
        case 7: { // custom, Unity pixels from the top-left corner
            CGFloat s = Scale(screenWidth);
            [c addObject:[v.topAnchor constraintEqualToAnchor:root.topAnchor constant:y * s]];
            [c addObject:[v.leftAnchor constraintEqualToAnchor:root.leftAnchor constant:x * s]];
            break;
        }
        default: // bottom
            [c addObject:[v.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor]];
            [c addObject:[v.centerXAnchor constraintEqualToAnchor:safe.centerXAnchor]];
            break;
    }
    self.constraints = c;
    [NSLayoutConstraint activateConstraints:c];
}

- (void)destroy
{
    [NSLayoutConstraint deactivateConstraints:self.constraints ?: @[]];
    self.view.delegate = nil;
    [self.view destroy];
    [self.view removeFromSuperview];
}

- (void)bannerViewDidLoad:(ADFBannerView *)bannerView { Emit(self.handle, @"loaded", nil); }
- (void)bannerView:(ADFBannerView *)bannerView didFailWithError:(NSError *)error { Emit(self.handle, @"failed", ErrorPayload(error)); }
- (void)bannerViewDidRecordImpression:(ADFBannerView *)bannerView { Emit(self.handle, @"shown", nil); }
- (void)bannerViewDidClick:(ADFBannerView *)bannerView { Emit(self.handle, @"clicked", nil); }

@end

#pragma mark - Interstitial, rewarded, app open

@interface AdfactenUnityFullScreen : NSObject <ADFFullScreenAdDelegate>
@property (nonatomic, readonly) int handle;
@property (nonatomic, strong) ADFFullScreenAd *ad;
@property (nonatomic) BOOL loading;
@property (nonatomic) BOOL presenting;
@property (nonatomic) BOOL pausedUnity;
@property (nonatomic) BOOL destroyed;
@end

@implementation AdfactenUnityFullScreen

- (instancetype)initWithHandle:(int)handle
{
    if ((self = [super init])) _handle = handle;
    return self;
}

- (void)pauseUnity
{
    if (self.pausedUnity) return;
    self.pausedUnity = YES;
    UnityPause(1);
}

- (void)resumeUnity
{
    if (!self.pausedUnity) return;
    self.pausedUnity = NO;
    UnityPause(0);
}

- (void)finish
{
    [Objects() removeObjectForKey:@(self.handle)];
}

- (void)adDidPresent:(ADFFullScreenAd *)ad { Emit(self.handle, @"shown", nil); }
- (void)adDidClick:(ADFFullScreenAd *)ad { Emit(self.handle, @"clicked", nil); }

- (void)ad:(ADFFullScreenAd *)ad didFailToPresentWithError:(NSError *)error
{
    self.presenting = NO;
    [self resumeUnity];
    Emit(self.handle, @"failedToShow", ErrorPayload(error));
}

- (void)adDidDismiss:(ADFFullScreenAd *)ad
{
    self.presenting = NO;
    [self resumeUnity];
    Emit(self.handle, @"closed", nil);
    [self finish];
}

@end

#pragma mark - Native templates

/// The "Ad" badge: a label with a little padding.
@interface AdfactenUnityPaddedLabel : UILabel
@end

@implementation AdfactenUnityPaddedLabel

- (void)drawTextInRect:(CGRect)rect
{
    [super drawTextInRect:UIEdgeInsetsInsetRect(rect, UIEdgeInsetsMake(1, 5, 1, 5))];
}

- (CGSize)intrinsicContentSize
{
    CGSize s = [super intrinsicContentSize];
    return CGSizeMake(s.width + 10, s.height + 2);
}

@end

/// A button with horizontal padding that still uses setTitle:forState:, which the SDK calls.
@interface AdfactenUnityPaddedButton : UIButton
@property (nonatomic) CGFloat horizontalPadding;
@end

@implementation AdfactenUnityPaddedButton

- (CGSize)intrinsicContentSize
{
    CGSize s = [super intrinsicContentSize];
    return CGSizeMake(s.width + 2 * self.horizontalPadding, s.height);
}

@end

static UIColor *StyleColor(NSDictionary *style, NSString *key, UIColor *fallback)
{
    id n = style[key];
    if (![n isKindOfClass:[NSNumber class]]) return fallback;
    uint32_t argb = (uint32_t)[n unsignedLongLongValue];
    return [UIColor colorWithRed:((argb >> 16) & 0xFF) / 255.0
                           green:((argb >> 8) & 0xFF) / 255.0
                            blue:(argb & 0xFF) / 255.0
                           alpha:((argb >> 24) & 0xFF) / 255.0];
}

static CGFloat StyleNumber(NSDictionary *style, NSString *key, CGFloat fallback)
{
    id n = style[key];
    return [n isKindOfClass:[NSNumber class]] ? (CGFloat)[n doubleValue] : fallback;
}

/// A native ad laid out from the small or medium template inside the SDK's NativeAdView. The SDK view records
/// the impression, the viewable impression and clicks; this class only builds the layout.
@interface AdfactenUnityNativeTemplate : NSObject
@property (nonatomic, strong, readonly) ADFNativeAdView *view;
@end

@implementation AdfactenUnityNativeTemplate

- (instancetype)initWithAd:(ADFNativeAd *)ad small:(BOOL)small style:(NSDictionary *)style
{
    if (!(self = [super init])) return nil;
    UIColor *background = StyleColor(style, @"backgroundColor", UIColor.whiteColor);
    UIColor *titleColor = StyleColor(style, @"titleColor", [UIColor colorWithRed:0x1B / 255.0 green:0x1B / 255.0 blue:0x1F / 255.0 alpha:1]);
    UIColor *bodyColor = StyleColor(style, @"bodyColor", [UIColor colorWithRed:0x46 / 255.0 green:0x46 / 255.0 blue:0x4F / 255.0 alpha:1]);
    UIColor *brandColor = StyleColor(style, @"brandColor", bodyColor);
    UIColor *ctaBackground = StyleColor(style, @"callToActionBackgroundColor", [UIColor colorWithRed:0x3F / 255.0 green:0x51 / 255.0 blue:0xB5 / 255.0 alpha:1]);
    UIColor *ctaText = StyleColor(style, @"callToActionTextColor", UIColor.whiteColor);
    UIColor *labelBackground = StyleColor(style, @"adLabelBackgroundColor", [UIColor colorWithRed:0xFF / 255.0 green:0xE0 / 255.0 blue:0x82 / 255.0 alpha:1]);
    UIColor *labelText = StyleColor(style, @"adLabelTextColor", [UIColor colorWithRed:0x3E / 255.0 green:0x2E / 255.0 blue:0 alpha:1]);
    CGFloat corner = StyleNumber(style, @"cornerRadius", 12);
    CGFloat ctaCorner = StyleNumber(style, @"callToActionCornerRadius", 20);

    ADFNativeAdView *view = [[ADFNativeAdView alloc] initWithFrame:CGRectZero];
    view.backgroundColor = background;
    view.layer.cornerRadius = corner;
    view.clipsToBounds = YES;
    _view = view;

    UILabel *title = [self label:small ? 14 : 15 weight:UIFontWeightSemibold color:titleColor lines:2];
    UILabel *body = [self label:small ? 12 : 13 weight:UIFontWeightRegular color:bodyColor lines:small ? 1 : 3];
    UILabel *brand = [self label:12 weight:UIFontWeightRegular color:brandColor lines:1];
    AdfactenUnityPaddedLabel *badge = [[AdfactenUnityPaddedLabel alloc] init];
    badge.font = [UIFont systemFontOfSize:10 weight:UIFontWeightBold];
    badge.textColor = labelText;
    badge.backgroundColor = labelBackground;
    badge.layer.cornerRadius = 4;
    badge.clipsToBounds = YES;
    [badge setContentHuggingPriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
    [badge setContentCompressionResistancePriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];

    UIImageView *icon = [self imageView];
    AdfactenUnityPaddedButton *cta = [AdfactenUnityPaddedButton buttonWithType:UIButtonTypeCustom];
    cta.backgroundColor = ctaBackground;
    [cta setTitleColor:ctaText forState:UIControlStateNormal];
    cta.titleLabel.font = [UIFont systemFontOfSize:14 weight:UIFontWeightSemibold];
    cta.titleLabel.lineBreakMode = NSLineBreakByTruncatingTail;
    cta.layer.cornerRadius = MIN(ctaCorner, small ? 18 : 20);
    cta.clipsToBounds = YES;

    view.titleLabel = title;
    view.bodyLabel = body;
    view.brandLabel = brand;
    view.adLabel = badge;
    view.callToActionButton = cta;

    UIStackView *content = [[UIStackView alloc] init];
    content.translatesAutoresizingMaskIntoConstraints = NO;
    content.layoutMarginsRelativeArrangement = YES;
    [view addSubview:content];

    if (small) {
        content.axis = UILayoutConstraintAxisHorizontal;
        content.spacing = 10;
        content.alignment = UIStackViewAlignmentCenter;
        content.directionalLayoutMargins = NSDirectionalEdgeInsetsMake(10, 12, 10, 12);
        UIStackView *top = [[UIStackView alloc] initWithArrangedSubviews:@[badge, brand]];
        top.axis = UILayoutConstraintAxisHorizontal;
        top.spacing = 6;
        top.alignment = UIStackViewAlignmentCenter;
        UIStackView *texts = [[UIStackView alloc] initWithArrangedSubviews:@[top, title, body]];
        texts.axis = UILayoutConstraintAxisVertical;
        texts.spacing = 2;
        texts.alignment = UIStackViewAlignmentLeading;
        [texts setContentCompressionResistancePriority:UILayoutPriorityDefaultLow forAxis:UILayoutConstraintAxisHorizontal];
        cta.horizontalPadding = 14;
        [cta setContentHuggingPriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
        [cta setContentCompressionResistancePriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisHorizontal];
        [NSLayoutConstraint activateConstraints:@[
            [icon.widthAnchor constraintEqualToConstant:56],
            [icon.heightAnchor constraintEqualToConstant:56],
            [cta.heightAnchor constraintEqualToConstant:36],
        ]];
        for (UIView *v in @[icon, texts, cta]) [content addArrangedSubview:v];
        // One thumbnail: the icon, or a square crop of the main image.
        if (ad.icon != nil) view.iconView = icon;
        else if (ad.image != nil) view.mediaView = icon;
        icon.hidden = ad.icon == nil && ad.image == nil;
        [NSLayoutConstraint activateConstraints:@[
            [content.leadingAnchor constraintEqualToAnchor:view.leadingAnchor],
            [content.trailingAnchor constraintEqualToAnchor:view.trailingAnchor],
            [content.centerYAnchor constraintEqualToAnchor:view.centerYAnchor],
            [content.topAnchor constraintGreaterThanOrEqualToAnchor:view.topAnchor],
        ]];
    } else {
        content.axis = UILayoutConstraintAxisVertical;
        content.spacing = 10;
        content.directionalLayoutMargins = NSDirectionalEdgeInsetsMake(12, 12, 12, 12);
        UIStackView *names = [[UIStackView alloc] initWithArrangedSubviews:@[title, brand]];
        names.axis = UILayoutConstraintAxisVertical;
        names.spacing = 2;
        UIStackView *badgeColumn = [[UIStackView alloc] initWithArrangedSubviews:@[badge, [[UIView alloc] init]]];
        badgeColumn.axis = UILayoutConstraintAxisVertical;
        UIStackView *header = [[UIStackView alloc] initWithArrangedSubviews:@[icon, names, badgeColumn]];
        header.axis = UILayoutConstraintAxisHorizontal;
        header.spacing = 10;
        header.alignment = UIStackViewAlignmentTop;
        [NSLayoutConstraint activateConstraints:@[
            [icon.widthAnchor constraintEqualToConstant:40],
            [icon.heightAnchor constraintEqualToConstant:40],
            [cta.heightAnchor constraintEqualToConstant:40],
        ]];
        // The image takes the height left over; without one a spacer keeps the button at the bottom.
        UIImageView *media = [self imageView];
        [media setContentHuggingPriority:UILayoutPriorityDefaultLow - 1 forAxis:UILayoutConstraintAxisVertical];
        [media setContentCompressionResistancePriority:UILayoutPriorityDefaultLow - 1 forAxis:UILayoutConstraintAxisVertical];
        UIView *spacer = [[UIView alloc] init];
        [spacer setContentHuggingPriority:UILayoutPriorityDefaultLow - 1 forAxis:UILayoutConstraintAxisVertical];
        for (UIView *v in @[header, media, body, spacer, cta]) [content addArrangedSubview:v];
        [content setCustomSpacing:8 afterView:media];
        for (UIView *v in @[header, body, cta]) {
            [v setContentHuggingPriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisVertical];
            [v setContentCompressionResistancePriority:UILayoutPriorityRequired forAxis:UILayoutConstraintAxisVertical];
        }
        view.iconView = icon;
        view.mediaView = media;
        icon.hidden = ad.icon == nil;
        media.hidden = ad.image == nil;
        spacer.hidden = ad.image != nil;
        [NSLayoutConstraint activateConstraints:@[
            [content.topAnchor constraintEqualToAnchor:view.topAnchor],
            [content.leadingAnchor constraintEqualToAnchor:view.leadingAnchor],
            [content.trailingAnchor constraintEqualToAnchor:view.trailingAnchor],
            [content.bottomAnchor constraintEqualToAnchor:view.bottomAnchor],
        ]];
    }

    // Binding fills the outlets and starts tracking. Binding again never records a second impression.
    view.nativeAd = ad;
    brand.hidden = ad.brand == nil;
    body.hidden = ad.body == nil;
    return self;
}

- (UILabel *)label:(CGFloat)size weight:(UIFontWeight)weight color:(UIColor *)color lines:(NSInteger)lines
{
    UILabel *l = [[UILabel alloc] init];
    l.font = [UIFont systemFontOfSize:size weight:weight];
    l.textColor = color;
    l.numberOfLines = lines;
    l.lineBreakMode = NSLineBreakByTruncatingTail;
    return l;
}

- (UIImageView *)imageView
{
    UIImageView *v = [[UIImageView alloc] init];
    v.contentMode = UIViewContentModeScaleAspectFill;
    v.clipsToBounds = YES;
    v.layer.cornerRadius = 8;
    return v;
}

- (void)remove
{
    self.view.nativeAd = nil;
    [self.view removeFromSuperview];
}

@end

@interface AdfactenUnityNative : NSObject <ADFNativeAdDelegate>
@property (nonatomic, readonly) int handle;
@property (nonatomic, strong) ADFNativeAd *ad;
@property (nonatomic, strong) AdfactenUnityNativeTemplate *template_;
@property (nonatomic) int templateKind;
@property (nonatomic, copy) NSString *styleJson;
@property (nonatomic) BOOL loading;
@end

@implementation AdfactenUnityNative

- (instancetype)initWithHandle:(int)handle
{
    if ((self = [super init])) _handle = handle;
    return self;
}

- (void)showTemplate:(int)kind frame:(CGRect)frame styleJson:(NSString *)styleJson
{
    if (self.ad == nil) return;
    NSString *style = styleJson ?: @"";
    if (self.template_ == nil || kind != self.templateKind || ![style isEqualToString:self.styleJson ?: @""]) {
        [self.template_ remove];
        NSDictionary *styleMap = nil;
        NSData *data = [style dataUsingEncoding:NSUTF8StringEncoding];
        if (data.length > 0) {
            id parsed = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
            if ([parsed isKindOfClass:[NSDictionary class]]) styleMap = parsed;
        }
        self.templateKind = kind;
        self.styleJson = style;
        self.template_ = [[AdfactenUnityNativeTemplate alloc] initWithAd:self.ad small:kind == 0 style:styleMap ?: @{}];
        [RootView() addSubview:self.template_.view];
    }
    self.template_.view.frame = frame;
    self.template_.view.hidden = NO;
}

- (void)hide
{
    self.template_.view.hidden = YES;
}

- (void)destroy
{
    [self.template_ remove];
    self.template_ = nil;
    self.ad.delegate = nil;
    self.ad = nil;
}

- (void)nativeAdDidRecordImpression:(ADFNativeAd *)nativeAd { Emit(self.handle, @"shown", nil); }
- (void)nativeAdDidClick:(ADFNativeAd *)nativeAd { Emit(self.handle, @"clicked", nil); }

@end

#pragma mark - C API

extern "C" {

void AdfactenUnity_SetEventCallback(AdfactenUnityEventCallback callback)
{
    gEventCallback = callback;
}

const char *AdfactenUnity_SdkVersion(void)
{
    // The caller (Mono / IL2CPP) frees the returned string.
    return strdup(ADFAdfacten.sdkVersion.UTF8String ?: "");
}

int AdfactenUnity_IsInitialized(void)
{
    return ADFAdfacten.isInitialized ? 1 : 0;
}

void AdfactenUnity_SetWrapper(const char *name, const char *version)
{
    [ADFAdfacten setWrapperName:ToNSString(name) ?: @"unity" version:ToNSString(version) ?: @""];
}

void AdfactenUnity_Initialize(const char *appKey, int testMode, int logLevel, int childDirected, const char *userId)
{
    NSString *key = ToNSString(appKey) ?: @"";
    NSString *user = NonEmpty(userId);
    RunOnMain(^{
        ADFConfig *config = [[ADFConfig alloc] init];
        config.testMode = testMode != 0;
        config.logLevel = (ADFLogLevel)MAX(0, MIN(4, logLevel));
        config.childDirected = childDirected != 0;
        config.userId = user;
        [ADFAdfacten initializeWithAppKey:key config:config];
    });
}

void AdfactenUnity_SetUserId(const char *userId)
{
    [ADFAdfacten setUserId:NonEmpty(userId)];
}

void AdfactenUnity_BannerCreate(int handle, const char *placementId, int sizeIndex)
{
    NSString *placement = ToNSString(placementId) ?: @"";
    ADFBannerSize size = ADFBannerSizeBanner;
    if (sizeIndex == 1) size = ADFBannerSizeLargeBanner;
    else if (sizeIndex == 2) size = ADFBannerSizeMediumRectangle;
    else if (sizeIndex == 3) size = ADFBannerSizeLeaderboard;
    id old = Objects()[@(handle)];
    if ([old isKindOfClass:[AdfactenUnityBanner class]]) [old destroy];
    Objects()[@(handle)] = [[AdfactenUnityBanner alloc] initWithHandle:handle placementId:placement size:size];
}

void AdfactenUnity_BannerSetPosition(int handle, int position, float x, float y, float screenWidth, float screenHeight)
{
    AdfactenUnityBanner *b = Objects()[@(handle)];
    if ([b isKindOfClass:[AdfactenUnityBanner class]]) [b setPosition:position x:x y:y screenWidth:screenWidth];
}

void AdfactenUnity_BannerLoad(int handle)
{
    AdfactenUnityBanner *b = Objects()[@(handle)];
    if ([b isKindOfClass:[AdfactenUnityBanner class]]) [b.view load];
}

void AdfactenUnity_BannerSetVisible(int handle, int visible)
{
    AdfactenUnityBanner *b = Objects()[@(handle)];
    if ([b isKindOfClass:[AdfactenUnityBanner class]]) b.view.hidden = visible == 0;
}

void AdfactenUnity_BannerDestroy(int handle)
{
    AdfactenUnityBanner *b = Objects()[@(handle)];
    if (![b isKindOfClass:[AdfactenUnityBanner class]]) return;
    [b destroy];
    [Objects() removeObjectForKey:@(handle)];
}

int AdfactenUnity_BannerSize(int handle, float screenWidth, float screenHeight, float *width, float *height)
{
    AdfactenUnityBanner *b = Objects()[@(handle)];
    if (![b isKindOfClass:[AdfactenUnityBanner class]]) return 0;
    CGSize size = b.view.intrinsicContentSize;
    CGFloat toUnity = 1 / Scale(screenWidth);
    if (width) *width = (float)(size.width * toUnity);
    if (height) *height = (float)(size.height * toUnity);
    return 1;
}

void AdfactenUnity_FullScreenLoad(int handle, int format, const char *placementId, const char *customData)
{
    NSString *placement = ToNSString(placementId) ?: @"";
    NSString *custom = NonEmpty(customData);
    AdfactenUnityFullScreen *holder = [[AdfactenUnityFullScreen alloc] initWithHandle:handle];
    holder.loading = YES;
    Objects()[@(handle)] = holder;
    void (^done)(ADFFullScreenAd *, NSError *) = ^(ADFFullScreenAd *ad, NSError *error) {
        // Destroyed from C# while loading: drop the result.
        if (Objects()[@(handle)] != holder || holder.destroyed) return;
        holder.loading = NO;
        if (ad == nil) {
            [Objects() removeObjectForKey:@(handle)];
            Emit(handle, @"failed", error ? ErrorPayload(error) : SimpleError(@"internal_error", @"unknown error"));
            return;
        }
        holder.ad = ad;
        ad.delegate = holder;
        NSMutableDictionary *payload = [NSMutableDictionary dictionary];
        if ([ad isKindOfClass:[ADFRewardedAd class]]) {
            ADFReward *reward = ((ADFRewardedAd *)ad).reward;
            if (reward) {
                payload[@"rewardType"] = reward.type;
                payload[@"rewardAmount"] = @(reward.amount);
            }
        }
        Emit(handle, @"loaded", payload);
    };
    if (format == 1) {
        [ADFRewardedAd loadWithPlacementId:placement customData:custom completion:^(ADFRewardedAd *ad, NSError *error) { done(ad, error); }];
    } else if (format == 2) {
        [ADFAppOpenAd loadWithPlacementId:placement completion:^(ADFAppOpenAd *ad, NSError *error) { done(ad, error); }];
    } else {
        [ADFInterstitialAd loadWithPlacementId:placement completion:^(ADFInterstitialAd *ad, NSError *error) { done(ad, error); }];
    }
}

void AdfactenUnity_FullScreenShow(int handle)
{
    AdfactenUnityFullScreen *holder = Objects()[@(handle)];
    if (![holder isKindOfClass:[AdfactenUnityFullScreen class]] || holder.ad == nil) {
        Emit(handle, @"failedToShow", SimpleError(@"not_ready", @"the ad is not loaded"));
        return;
    }
    UIViewController *controller = UnityGetGLViewController();
    holder.presenting = YES;
    // Pause the game (and its audio) while the ad is on screen, like Android does.
    [holder pauseUnity];
    if ([holder.ad isKindOfClass:[ADFRewardedAd class]]) {
        [(ADFRewardedAd *)holder.ad presentFromViewController:controller userDidEarnRewardHandler:^(ADFReward *reward) {
            Emit(handle, @"reward", @{ @"type": reward.type ?: @"", @"amount": @(reward.amount) });
        }];
    } else {
        [holder.ad presentFromViewController:controller];
    }
}

int AdfactenUnity_FullScreenIsReady(int handle)
{
    AdfactenUnityFullScreen *holder = Objects()[@(handle)];
    if (![holder isKindOfClass:[AdfactenUnityFullScreen class]]) return 0;
    return holder.ad != nil && holder.ad.isReady && !holder.presenting ? 1 : 0;
}

void AdfactenUnity_FullScreenDestroy(int handle)
{
    AdfactenUnityFullScreen *holder = Objects()[@(handle)];
    if (![holder isKindOfClass:[AdfactenUnityFullScreen class]]) return;
    // An ad on screen keeps running (and keeps the game paused) until the user closes it.
    if (holder.presenting) return;
    holder.destroyed = YES;
    holder.ad.delegate = nil;
    [Objects() removeObjectForKey:@(handle)];
}

void AdfactenUnity_NativeLoad(int handle, const char *placementId)
{
    NSString *placement = ToNSString(placementId) ?: @"";
    AdfactenUnityNative *holder = [[AdfactenUnityNative alloc] initWithHandle:handle];
    holder.loading = YES;
    Objects()[@(handle)] = holder;
    [ADFNativeAd loadWithPlacementId:placement completion:^(ADFNativeAd *ad, NSError *error) {
        if (Objects()[@(handle)] != holder) return;
        holder.loading = NO;
        if (ad == nil) {
            [Objects() removeObjectForKey:@(handle)];
            Emit(handle, @"failed", error ? ErrorPayload(error) : SimpleError(@"internal_error", @"unknown error"));
            return;
        }
        holder.ad = ad;
        ad.delegate = holder;
        NSMutableDictionary *p = [NSMutableDictionary dictionary];
        if (ad.title) p[@"title"] = ad.title;
        if (ad.body) p[@"body"] = ad.body;
        if (ad.brand) p[@"brand"] = ad.brand;
        if (ad.callToAction) p[@"callToAction"] = ad.callToAction;
        p[@"adLabel"] = ad.adLabel ?: @"Ad";
        p[@"hasImage"] = @(ad.image != nil);
        p[@"hasIcon"] = @(ad.icon != nil);
        p[@"imageWidth"] = @(ad.imageSize.width);
        p[@"imageHeight"] = @(ad.imageSize.height);
        Emit(handle, @"loaded", p);
    }];
}

void AdfactenUnity_NativeShow(int handle, int templateKind, float x, float y, float width, float height,
                              float screenWidth, float screenHeight, const char *styleJson)
{
    AdfactenUnityNative *holder = Objects()[@(handle)];
    if (![holder isKindOfClass:[AdfactenUnityNative class]]) return;
    CGFloat s = Scale(screenWidth);
    [holder showTemplate:templateKind frame:CGRectMake(x * s, y * s, width * s, height * s) styleJson:ToNSString(styleJson)];
}

void AdfactenUnity_NativeHide(int handle)
{
    AdfactenUnityNative *holder = Objects()[@(handle)];
    if ([holder isKindOfClass:[AdfactenUnityNative class]]) [holder hide];
}

void AdfactenUnity_NativeDestroy(int handle)
{
    AdfactenUnityNative *holder = Objects()[@(handle)];
    if (![holder isKindOfClass:[AdfactenUnityNative class]]) return;
    [holder destroy];
    [Objects() removeObjectForKey:@(handle)];
}

int AdfactenUnity_TrackingStatus(void)
{
    if (@available(iOS 14, *)) return (int)ATTrackingManager.trackingAuthorizationStatus;
    return -1;
}

void AdfactenUnity_RequestTracking(int handle)
{
    if (@available(iOS 14, *)) {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            RunOnMain(^{ Emit(handle, @"tracking", @{ @"status": @((int)status) }); });
        }];
    } else {
        Emit(handle, @"tracking", @{ @"status": @(-1) });
    }
}

} // extern "C"
