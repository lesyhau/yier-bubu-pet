using System.Collections.Generic;
using System.Globalization;

namespace YierBubuPet;

/// <summary>
/// User interface text in English and Vietnamese. To add a language, add a column to each entry
/// and a case to <see cref="Apply"/>.
/// </summary>
public static class Strings
{
    /// <summary>"en" or "vi", the language currently shown.</summary>
    public static string Language { get; private set; } = "en";

    /// <summary>Picks the language from the setting: "en", "vi", or anything else for the Windows display language.</summary>
    public static void Apply(string? setting)
    {
        Language = setting switch
        {
            "en" or "vi" => setting,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vi" : "en",
        };
    }

    static readonly Dictionary<string, (string En, string Vi)> Text = new()
    {
        // tray menu
        ["Together"] = ("Together time 💕", "Bên nhau 💕"),
        ["Bedtime"] = ("Bedtime 💤", "Đi ngủ 💤"),
        ["WakeUp"] = ("Wake up ☀️", "Thức dậy ☀️"),
        ["Size"] = ("Size", "Kích thước"),
        ["Size.Small"] = ("Small", "Nhỏ"),
        ["Size.Medium"] = ("Medium", "Vừa"),
        ["Size.Large"] = ("Large", "Lớn"),
        ["Size.Huge"] = ("Huge", "Rất lớn"),
        ["WalkArea"] = ("Walking area", "Vùng đi lại"),
        ["WalkArea.Full"] = ("Whole screen", "Cả màn hình"),
        ["WalkArea.Left"] = ("Left half", "Nửa bên trái"),
        ["WalkArea.Right"] = ("Right half", "Nửa bên phải"),
        ["HidePets"] = ("Hide pets", "Ẩn hai bé"),
        ["ShowPets"] = ("Show pets", "Hiện hai bé"),
        ["OpenFolder"] = ("Open animations folder", "Mở thư mục hoạt ảnh"),
        ["Reload"] = ("Reload animations", "Tải lại hoạt ảnh"),
        ["DownloadStarter"] = ("Download starter stickers", "Tải bộ sticker có sẵn"),
        ["StartWithWindows"] = ("Start with Windows", "Khởi động cùng Windows"),
        ["Language"] = ("Language", "Ngôn ngữ"),
        ["Language.Auto"] = ("Automatic", "Tự động"),
        ["About"] = ("About Yier & Bubu", "Giới thiệu Yier & Bubu"),
        ["Donate"] = ("Support the developer ❤", "Ủng hộ tác giả ❤"),
        ["Exit"] = ("Exit", "Thoát"),

        // control panel
        ["Panel.Hello"] = ("Hi there! 💕", "Chào bạn! 💕"),
        ["Panel.StatusPlaying"] = ("Yier and Bubu are playing on your taskbar.", "Yier và Bubu đang chơi trên thanh tác vụ."),
        ["Panel.StatusSleeping"] = ("Shh… they're sleeping 💤", "Suỵt… hai bé đang ngủ 💤"),
        ["Panel.StatusHidden"] = ("The pets are hidden.", "Hai bé đang được ẩn."),
        ["Panel.StatusNone"] = ("No pets yet: download the starter stickers below.", "Chưa có bé nào: hãy tải bộ sticker có sẵn ở bên dưới."),
        ["Panel.Alarms"] = ("Alarms & timers", "Báo thức & hẹn giờ"),
        ["Panel.AddAlarm"] = ("Add alarm", "Thêm báo thức"),
        ["Panel.NoAlarms"] = ("No alarms yet", "Chưa có báo thức nào"),
        ["Panel.Remove"] = ("Remove", "Xóa"),
        ["Panel.Timer"] = ("Quick timer", "Hẹn giờ nhanh"),
        ["Panel.CustomTimer"] = ("Custom…", "Tùy chỉnh…"),
        ["Panel.Minutes"] = ("{0} min", "{0} phút"),
        ["Panel.Alerts"] = ("Alerts", "Thông báo"),
        ["Panel.TestAlert"] = ("Try it", "Thử"),
        ["Panel.AlertsHint"] = (
            "Yier plays the animation and sound from the animations\\alert folder.",
            "Yier sẽ phát hoạt ảnh và âm thanh trong thư mục animations\\alert."),
        ["Panel.OnNotifications"] = ("Notifications from other apps", "Thông báo từ ứng dụng khác"),
        ["Panel.Hourly"] = ("Every hour, on the hour", "Báo giờ mỗi tiếng"),
        ["Panel.BatteryLow"] = ("Battery below {0}%", "Pin dưới {0}%"),
        ["Panel.Charger"] = ("Charger plugged in / unplugged", "Cắm / rút sạc"),
        ["Panel.Unlock"] = ("When I unlock the PC", "Khi mở khóa máy"),
        ["Panel.AlertSound"] = ("Play the alert sound", "Phát âm thanh"),
        ["Panel.Pets"] = ("Settings", "Cài đặt"),
        ["Panel.Animations"] = ("Animations", "Hoạt ảnh"),
        ["Panel.OpenFolder"] = ("Open folder", "Mở thư mục"),
        ["Panel.AnimationCount"] = ("{0} animation files", "{0} tệp hoạt ảnh"),
        ["Panel.Support"] = ("Donate", "Ủng hộ"),

        // alerts
        ["Alert.Notification"] = ("🔔 You have a new notification", "🔔 Bạn có thông báo mới"),
        ["Alert.Timer"] = ("⏳ Time's up!", "⏳ Hết giờ rồi!"),
        ["Alert.Alarm"] = ("⏰ It's {0}!", "⏰ {0} rồi!"),
        ["Alert.Hourly"] = ("🕐 It's {0}", "🕐 Đã {0} rồi"),
        ["Alert.Charging"] = ("🔌 Charging. Yum!", "🔌 Đang sạc pin, ngon quá!"),
        ["Alert.Unplugged"] = ("🔋 Charger unplugged", "🔋 Đã rút sạc"),
        ["Alert.Battery"] = ("🪫 Battery is at {0}%. Plug in the charger!", "🪫 Pin còn {0}%. Cắm sạc đi bạn ơi!"),
        ["Alert.Unlock"] = ("👋 Welcome back!", "👋 Chào mừng bạn trở lại!"),
        ["Alert.Test"] = ("🔔 This is how alerts look!", "🔔 Thông báo sẽ trông như thế này!"),

        // alarm dialog
        ["Alarm.NewAlarm"] = ("New alarm", "Báo thức mới"),
        ["Alarm.NewTimer"] = ("New timer", "Hẹn giờ mới"),
        ["Alarm.Time"] = ("Time (e.g. 07:30)", "Giờ (ví dụ 07:30)"),
        ["Alarm.Minutes"] = ("Minutes", "Số phút"),
        ["Alarm.Repeat"] = ("Repeat", "Lặp lại"),
        ["Alarm.Once"] = ("Once", "Một lần"),
        ["Alarm.Daily"] = ("Every day", "Hằng ngày"),
        ["Alarm.Weekdays"] = ("Weekdays (Mon–Fri)", "Ngày thường (T2–T6)"),
        ["Alarm.Message"] = ("Message (optional)", "Lời nhắn (không bắt buộc)"),
        ["Alarm.Save"] = ("Save", "Lưu"),
        ["Alarm.Cancel"] = ("Cancel", "Hủy"),
        ["Alarm.BadTime"] = ("Enter a time like 07:30.", "Hãy nhập giờ dạng 07:30."),
        ["Alarm.BadMinutes"] = ("Enter a number of minutes (up to 1440).", "Hãy nhập số phút (tối đa 1440)."),

        // updates
        ["Update.Title"] = ("Updates", "Cập nhật"),
        ["Update.Available"] = ("Version {0} is available", "Đã có phiên bản {0}"),
        ["Update.Now"] = ("Update now", "Cập nhật ngay"),
        ["Update.OpenPage"] = ("Open the download page", "Mở trang tải về"),
        ["Update.WhatsNew"] = ("What's new", "Có gì mới"),
        ["Update.Check"] = ("Check for updates", "Kiểm tra cập nhật"),
        ["Update.Checking"] = ("Checking for updates…", "Đang kiểm tra cập nhật…"),
        ["Update.UpToDate"] = ("You have the latest version.", "Bạn đang dùng phiên bản mới nhất."),
        ["Update.Failed"] = ("Couldn't check for updates. Check your internet connection.", "Không kiểm tra được cập nhật. Hãy kiểm tra kết nối mạng."),
        ["Update.Downloading"] = ("Downloading the update… {0}%", "Đang tải bản cập nhật… {0}%"),
        ["Update.Installing"] = ("Installing… the pets will be right back!", "Đang cài đặt… hai bé sẽ quay lại ngay!"),
        ["Update.DownloadFailed"] = ("The update couldn't be downloaded. Try again later.", "Không tải được bản cập nhật. Hãy thử lại sau."),
        ["Update.Balloon"] = ("Version {0} is available. Click here to update.", "Đã có phiên bản {0}. Nhấp vào đây để cập nhật."),
        ["Update.Auto"] = ("Check for updates automatically", "Tự động kiểm tra cập nhật"),

        // starter pack
        ["Starter.Offer"] = (
            "There are no animations in\n{0}\n\nDownload the starter pack of {1} Yier & Bubu stickers from Tenor (about 5 MB)?",
            "Chưa có hoạt ảnh nào trong\n{0}\n\nTải bộ {1} sticker Yier & Bubu từ Tenor (khoảng 5 MB)?"),
        ["Starter.Downloading"] = ("Downloading stickers…", "Đang tải sticker…"),
        ["Starter.Done"] = ("{0} new sticker(s) downloaded.", "Đã tải {0} sticker mới."),
        ["Starter.Failed"] = (
            "{0} new sticker(s) downloaded, {1} failed. Try again later from the control panel.",
            "Đã tải {0} sticker mới, {1} sticker bị lỗi. Hãy thử lại sau trong bảng điều khiển."),

        // about window
        ["About.Version"] = ("Version {0}", "Phiên bản {0}"),
        ["About.Description"] = (
            "Yier and Bubu live on top of your taskbar. They walk around, react when you click them, " +
            "hug each other every now and then, and fall asleep when you're away from the PC.",
            "Yier và Bubu sống trên thanh tác vụ của bạn. Hai bé đi dạo, phản ứng khi bạn nhấp vào, " +
            "thỉnh thoảng ôm nhau và ngủ khi bạn rời máy."),
        ["About.FanProject"] = (
            "Unofficial fan project. Yier & Bubu (一二布布) belong to their creators.",
            "Dự án của người hâm mộ, không chính thức. Yier & Bubu (一二布布) thuộc về tác giả gốc."),
        ["About.License"] = ("The code is free and open source (MIT License).", "Mã nguồn mở và miễn phí (giấy phép MIT)."),
        ["About.Source"] = ("Source code and updates on GitHub", "Mã nguồn và bản cập nhật trên GitHub"),
        ["About.DonateTitle"] = ("Support the developer", "Ủng hộ tác giả"),
        ["About.DonateText"] = (
            "Yier & Bubu is free. If the pets make you smile, you can buy me a coffee ☕ Thank you!",
            "Yier & Bubu hoàn toàn miễn phí. Nếu hai bé làm bạn vui, bạn có thể mời mình một ly cà phê ☕ Cảm ơn bạn!"),
        ["About.PayPal"] = ("Donate with PayPal", "Ủng hộ qua PayPal"),
        ["About.PayPalSendTo"] = ("Send any amount to this PayPal account:", "Gửi bất kỳ số tiền nào tới tài khoản PayPal này:"),
        ["About.CopyEmail"] = ("Copy email", "Sao chép email"),
        ["About.QrCaption"] = (
            "Or scan this QR code with a Vietnamese banking app",
            "Hoặc quét mã QR bằng ứng dụng ngân hàng Việt Nam"),
        ["About.AccountName"] = ("Account name", "Chủ tài khoản"),
        ["About.AccountNumber"] = ("Account number", "Số tài khoản"),
        ["About.Bank"] = ("Bank", "Ngân hàng"),
        ["About.CopyNumber"] = ("Copy account number", "Sao chép số tài khoản"),
    };

    public static string Get(string key) =>
        Text.TryGetValue(key, out var t) ? (Language == "vi" ? t.Vi : t.En) : key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
