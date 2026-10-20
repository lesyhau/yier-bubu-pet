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
        ["Sound"] = ("Sound", "Âm thanh"),
        ["Sound.Off"] = ("Off", "Tắt"),
        ["Sound.Interactions"] = ("When I click or drag them", "Khi tôi nhấp hoặc kéo hai bé"),
        ["Sound.Always"] = ("For everything they do", "Mọi lúc"),
        ["Volume.Quiet"] = ("Quiet", "Nhỏ"),
        ["Volume.Normal"] = ("Normal", "Vừa"),
        ["Volume.Loud"] = ("Loud", "To"),
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
        ["Language.Auto"] = ("Automatic (Windows language)", "Tự động (theo Windows)"),
        ["About"] = ("About Yier & Bubu", "Giới thiệu Yier & Bubu"),
        ["Donate"] = ("Support the developer ❤", "Ủng hộ tác giả ❤"),
        ["Exit"] = ("Exit", "Thoát"),

        // starter pack
        ["Starter.Offer"] = (
            "There are no animations in\n{0}\n\nDownload the starter pack of {1} Yier & Bubu stickers from Tenor (about 5 MB)?",
            "Chưa có hoạt ảnh nào trong\n{0}\n\nTải bộ {1} sticker Yier & Bubu từ Tenor (khoảng 5 MB)?"),
        ["Starter.Downloading"] = ("Downloading stickers…", "Đang tải sticker…"),
        ["Starter.Done"] = ("{0} new sticker(s) downloaded.", "Đã tải {0} sticker mới."),
        ["Starter.Failed"] = (
            "{0} new sticker(s) downloaded, {1} failed. Try again later from the tray menu.",
            "Đã tải {0} sticker mới, {1} sticker bị lỗi. Hãy thử lại sau từ menu ở khay hệ thống."),

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
        ["About.PayPalCopy"] = ("Copy PayPal email", "Sao chép email PayPal"),
        ["About.Copied"] = ("Copied!", "Đã sao chép!"),
        ["About.QrCaption"] = (
            "In Vietnam: scan with your banking app (VietQR)",
            "Tại Việt Nam: quét mã bằng ứng dụng ngân hàng (VietQR)"),
        ["About.Close"] = ("Close", "Đóng"),
    };

    public static string Get(string key) =>
        Text.TryGetValue(key, out var t) ? (Language == "vi" ? t.Vi : t.En) : key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
