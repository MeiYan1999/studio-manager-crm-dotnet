using System.Globalization;
using MayAlbum.Models;

namespace MayAlbum.Services;

internal static class DemoBoard
{
    public static TaskBoard Create()
    {
        var now = DateTimeOffset.UtcNow;
        var labels = new[]
        {
            new TaskLabel { Id = "label-uu-tien", Name = "Ưu tiên", Color = "#b25f46" },
            new TaskLabel { Id = "label-khach-hang", Name = "Khách hàng", Color = "#b56a64" },
            new TaskLabel { Id = "label-hau-ky", Name = "Hậu kỳ", Color = "#6d7353" },
            new TaskLabel { Id = "label-giao-anh", Name = "Giao ảnh", Color = "#4e7275" },
            new TaskLabel { Id = "label-y-tuong", Name = "Ý tưởng", Color = "#9a6b78" }
        };

        var board = new TaskBoard
        {
            Id = "studio-board",
            Title = "Nhịp làm việc Mây",
            Description = "Theo dõi từng album từ lúc nhận yêu cầu đến khi trao ảnh cho khách. Kéo thẻ giữa các danh sách để cập nhật tiến độ.",
            Theme = "ink",
            CreatedAt = now.AddDays(-90),
            UpdatedAt = now,
            Labels = labels.ToList()
        };

        board.Lists =
        [
            new TaskList
            {
                Id = "list-y-tuong",
                Title = "Ý tưởng",
                Color = "#9a6b78",
                Position = 0,
                CreatedAt = now.AddDays(-90),
                UpdatedAt = now.AddDays(-10),
                Cards =
                [
                    Card(
                        "card-concept-da-lat",
                        "Lên concept “Một ngày ở Đà Lạt”",
                        "Sương sớm, vườn rau, một chiếc xe cũ và nắng cuối ngày. Ghép thành một câu chuyện thật nhẹ.",
                        labels: ["label-y-tuong", "label-uu-tien"],
                        due: Date(10),
                        checklist:
                        [
                            ("Tìm 12 ảnh tham khảo", true),
                            ("Chọn bảng màu chủ đạo", true),
                            ("Gửi khách xem trước", false)
                        ],
                        createdAt: now.AddDays(-8)),
                    Card(
                        "card-dia-diem",
                        "Gợi ý địa điểm cho buổi chụp ngoài trời",
                        "Ưu tiên những nơi có thể di chuyển trong 20 phút và có phương án dự phòng nếu trời mưa.",
                        labels: ["label-y-tuong"],
                        due: Date(14),
                        createdAt: now.AddDays(-6))
                ]
            },
            new TaskList
            {
                Id = "list-chuan-bi",
                Title = "Chuẩn bị",
                Color = "#c3a47a",
                Position = 1,
                CreatedAt = now.AddDays(-90),
                UpdatedAt = now.AddDays(-1),
                Cards =
                [
                    Card(
                        "card-timeline-linh-minh",
                        "Gửi timeline chuẩn bị cho Linh & Minh",
                        "Kèm danh sách đồ cần chuẩn bị, giờ có mặt và số điện thoại của ekip.",
                        labels: ["label-khach-hang"],
                        due: Date(2),
                        albumId: "demo-ngay-chung-doi",
                        checklist:
                        [
                            ("Chốt giờ make-up", true),
                            ("Xác nhận địa điểm đón", true),
                            ("Gửi timeline qua Zalo", true),
                            ("Nhắc khách mang giày thoải mái", false)
                        ],
                        createdAt: now.AddDays(-5)),
                    Card(
                        "card-ve-sinh-thiet-bi",
                        "Vệ sinh thiết bị & sạc pin dự phòng",
                        "",
                        due: Date(1),
                        checklist:
                        [
                            ("Lau sensor và lens", true),
                            ("Sạc 4 pin dự phòng", false),
                            ("Định dạng lại thẻ nhớ", false),
                            ("Kiểm tra đèn flash", false)
                        ],
                        createdAt: now.AddDays(-3)),
                    Card(
                        "card-moodboard",
                        "Chốt moodboard với khách hàng",
                        "Khách muốn tông màu ấm hơn bản đầu tiên. Cần xác nhận lại trước buổi chụp.",
                        labels: ["label-uu-tien", "label-khach-hang"],
                        due: Date(-1),
                        albumId: "demo-ngay-chung-doi",
                        createdAt: now.AddDays(-7))
                ]
            },
            new TaskList
            {
                Id = "list-dang-chup",
                Title = "Đang chụp",
                Color = "#b25f46",
                Position = 2,
                CreatedAt = now.AddDays(-90),
                UpdatedAt = now.AddDays(-1),
                Cards =
                [
                    Card(
                        "card-chup-da-lat",
                        "Buổi chụp cưới tại Đà Lạt",
                        "Ekip 4 người · 06:00 – 17:30. Địa điểm thay đổi theo thời tiết, ưu tiên đồi cỏ đầu tiên.",
                        labels: ["label-khach-hang", "label-uu-tien"],
                        due: Date(5),
                        albumId: "demo-ngay-chung-doi",
                        checklist:
                        [
                            ("Kiểm tra thời tiết buổi sáng", true),
                            ("Chuẩn bị 2 thẻ nhớ", true),
                            ("Backup ảnh ngay sau khi về", false)
                        ],
                        createdAt: now.AddDays(-4)),
                    Card(
                        "card-lookbook-thu",
                        "Chụp lookbook bộ sưu tập Thu",
                        "Lịch dự kiến tại studio, bối cảnh giấy kraft và hoa khô.",
                        labels: ["label-y-tuong"],
                        due: Date(8),
                        createdAt: now.AddDays(-2))
                ]
            },
            new TaskList
            {
                Id = "list-hau-ky",
                Title = "Hậu kỳ",
                Color = "#6d7353",
                Position = 3,
                CreatedAt = now.AddDays(-90),
                UpdatedAt = now,
                Cards =
                [
                    Card(
                        "card-chinh-anh-dot-1",
                        "Chọn và chỉnh màu ảnh đợt 1",
                        "Khoảng 180 ảnh. Giữ da tự nhiên, đẩy nhẹ tông xanh của vườn và ánh nắng cuối ngày.",
                        labels: ["label-hau-ky"],
                        due: Date(3),
                        albumId: "demo-ngay-chung-doi",
                        checklist:
                        [
                            ("Backup bộ ảnh gốc", true),
                            ("Chọn 180 ảnh", true),
                            ("Chỉnh màu 50%", false),
                            ("Xuất JPG chất lượng cao", false)
                        ],
                        createdAt: now.AddDays(-3)),
                    Card(
                        "card-album-online",
                        "Dựng album online & kiểm tra liên kết",
                        "Kiểm tra lại ảnh bìa, thứ tự ảnh và quyền tải xuống trước khi gửi khách.",
                        labels: ["label-hau-ky", "label-giao-anh"],
                        due: Date(7),
                        albumId: "demo-bien-nho",
                        checklist:
                        [
                            ("Chọn ảnh bìa", true),
                            ("Kiểm tra mã ảnh", false),
                            ("Bật quyền tải xuống", false),
                            ("Mở thử trên điện thoại", false)
                        ],
                        createdAt: now.AddDays(-2))
                ]
            },
            new TaskList
            {
                Id = "list-da-giao",
                Title = "Đã giao khách",
                Color = "#4e7275",
                Position = 4,
                CreatedAt = now.AddDays(-90),
                UpdatedAt = now.AddDays(-2),
                Cards =
                [
                    Card(
                        "card-giao-bien-nho",
                        "Giao album Biển Nhớ cho Hà & Nam",
                        "Đã gửi liên kết riêng và nhận phản hồi tích cực từ khách.",
                        labels: ["label-giao-anh", "label-khach-hang"],
                        due: Date(-4),
                        dueComplete: true,
                        albumId: "demo-bien-nho",
                        checklist:
                        [
                            ("Kiểm tra lần cuối", true),
                            ("Gửi liên kết riêng", true),
                            ("Lưu xác nhận của khách", true)
                        ],
                        createdAt: now.AddDays(-15)),
                    Card(
                        "card-phan-hoi",
                        "Nhận phản hồi sau khi khách xem album",
                        "Hỏi khách về những tấm ảnh yêu thích và xin phép chia sẻ một vài khung hình lên portfolio.",
                        labels: ["label-khach-hang"],
                        due: Date(2),
                        albumId: "demo-bien-nho",
                        createdAt: now.AddDays(-2))
                ]
            }
        ];

        return board;
    }

    private static TaskCard Card(
        string id,
        string title,
        string description,
        string[]? labels = null,
        string due = "",
        bool dueComplete = false,
        string albumId = "",
        (string Text, bool Done)[]? checklist = null,
        DateTimeOffset? createdAt = null)
    {
        return new TaskCard
        {
            Id = id,
            Title = title,
            Description = description,
            DueDate = due,
            IsDueComplete = dueComplete,
            AlbumId = albumId,
            LabelIds = labels?.ToList() ?? [],
            Checklist = checklist?
                .Select(item => new TaskChecklistItem
                {
                    Id = $"{id}-check-{Guid.NewGuid().ToString("N")[..6]}",
                    Text = item.Text,
                    Done = item.Done
                })
                .ToList() ?? [],
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-2),
            UpdatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-2)
        };
    }

    private static string Date(int offsetDays) =>
        DateTimeOffset.UtcNow
            .ToOffset(TimeSpan.FromHours(7))
            .Date
            .AddDays(offsetDays)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
