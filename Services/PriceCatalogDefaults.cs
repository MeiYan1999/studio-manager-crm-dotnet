using System.Text.Json;
using StudioCore.Models;
namespace StudioCore.Services;
public static class PriceCatalogDefaults
{
    public static List<PriceGroup> Create() => JsonSerializer.Deserialize<List<PriceGroup>>(Data, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    private const string Data = """
[
  {
    "id": "family",
    "title": "Chụp ảnh gia đình - đại gia đình",
    "description": "Lưu giữ khoảnh khắc sum vầy.",
    "items": [
      {
        "id": "family-1",
        "name": "Gói ảnh đơn gia đình",
        "subtitle": "Tối đa 5 thành viên",
        "priceText": "Thứ 2 - Thứ 6: 1.800.000đ\nThứ 7, Chủ nhật, ngày lễ: 1.900.000đ",
        "benefits": "2 Concept (1 concept Gia đình, 1 concept Trẻ em)\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\n10 file chỉnh sửa resize và in size 15x21cm\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 35x50cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "family-2",
        "name": "Gói album đại gia đình",
        "subtitle": "Từ 6 - 10 thành viên. Tính các chi phí phát sinh từ Gói chụp ảnh Gia đình 5 thành viên",
        "priceText": "Liên hệ để nhận báo giá chi tiết",
        "benefits": "Thành viên phát sinh (từ thành viên thứ 6 trở đi): 200.000đ/người (đã bao gồm trang phục)\nMakeup Cơ Bản phát sinh: 300.000đ/người lớn\nMakeup Nâng Cao phát sinh: 500.000đ/người lớn\nMakeup Nghệ Thuật cho bé (2-12 tuổi) phát sinh: 250.000đ/bé\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "family-3",
        "name": "Gói album gia đình size 20x25 cm",
        "subtitle": "Tối đa 5 thành viên",
        "priceText": "3.800.000đ",
        "benefits": "Album thiết kế 20 trang (21 ảnh)\n3 Concept (2 concept Gia đình, 1 concept Trẻ em)\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 35x50cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "family-4",
        "name": "Gói album gia đình size 20x30 cm",
        "subtitle": "Tối đa 5 thành viên",
        "priceText": "4.550.000đ",
        "benefits": "Album thiết kế 20 trang (30 ảnh)\n4 Concept (2 concept Gia đình, 2 concept Trẻ em)\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 40x60cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "family-5",
        "name": "Gói album gia đình size 25x38 cm",
        "subtitle": "Tối đa 5 thành viên",
        "priceText": "5.550.000đ",
        "benefits": "Album thiết kế 20 trang (35 ảnh)\n5 Concept (2 concept Gia đình, 3 concept Trẻ em)\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 50x75cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "family-6",
        "name": "Gói album gia đình size 30x45 cm",
        "subtitle": "Tối đa 5 thành viên",
        "priceText": "6.500.000đ",
        "benefits": "Album thiết kế 20 trang (40 ảnh)\n5 Concept (2 concept Gia đình, 3 concept Trẻ em)\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 60x90cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      }
    ]
  },
  {
    "id": "children",
    "title": "Chụp ảnh trẻ em",
    "description": "Ghi dấu từng giai đoạn trưởng thành của con yêu.",
    "items": [
      {
        "id": "children-1",
        "name": "Gói ảnh đơn cho 1 bé",
        "subtitle": "Bé từ 5 tháng - 12 tuổi",
        "priceText": "Thứ 2 - Thứ 6: 1.150.000đ\nThứ 7, Chủ nhật, ngày lễ: 1.250.000đ",
        "benefits": "2 Concept Trẻ em\nHỗ trợ trang phục theo Concept\n10 file chỉnh sửa resize và 10 ảnh in size 15x21cm\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng gỗ size 25x38cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "children-2",
        "name": "Gói album cho bé size 20x20 cm",
        "subtitle": "Bé từ 5 tháng - 12 tuổi",
        "priceText": "2.250.000đ",
        "benefits": "Album thiết kế 20 trang (21 ảnh)\n3 Concept Trẻ em\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 25x38cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "children-3",
        "name": "Gói album cho bé size 20x30 cm",
        "subtitle": "Bé từ 5 tháng - 12 tuổi",
        "priceText": "3.350.000đ",
        "benefits": "Album thiết kế 20 trang (30 ảnh)\n4 Concept Trẻ em\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 35x50cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "children-4",
        "name": "Gói album cho bé size 25x38 cm",
        "subtitle": "Bé từ 5 tháng - 12 tuổi",
        "priceText": "4.550.000đ",
        "benefits": "Album thiết kế 20 trang (35 ảnh)\n5 Concept Trẻ em\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 40x60cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "children-5",
        "name": "Gói album cho bé size 30x40 cm",
        "subtitle": "Bé từ 5 tháng - 12 tuổi",
        "priceText": "5.600.000đ",
        "benefits": "Album thiết kế 20 trang (35 ảnh)\n5 Concept Trẻ em\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 50x75cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      }
    ]
  },
  {
    "id": "newborn",
    "title": "Chụp ảnh sơ sinh",
    "description": "Ôm trọn những yêu thương đầu đời.",
    "items": [
      {
        "id": "newborn-1",
        "name": "Gói ảnh đơn sơ sinh tại studio",
        "subtitle": "Bé sơ sinh 0 - 5 tháng tuổi",
        "priceText": "Thứ 2 - Thứ 6: 1.850.000đ\nThứ 7, Chủ nhật & ngày lễ: 2.550.000 VNĐ",
        "benefits": "2 Concept Sơ sinh\nHỗ trợ trang phục theo Concept\n10 file chỉnh sửa resize và 10 ảnh in size 15x21cm\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nEkip hỗ trợ chăm bé xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "newborn-2",
        "name": "Gói ảnh đơn sơ sinh tại nhà",
        "subtitle": "Bé sơ sinh 0 - 5 tháng tuổi. Chỉ nhận lịch Thứ 2 - Thứ 6",
        "priceText": "Hà Nội - Bắc Ninh: 1.850.000đ\nSài Gòn: 2.300.000đ",
        "benefits": "2 Concept Sơ sinh\nHỗ trợ trang phục theo Concept\n10 file chỉnh sửa resize và 10 ảnh in size 15x21cm\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nEkip hỗ trợ chăm bé xuyên suốt buổi chụp\nChưa bao gồm chi phí di chuyển cho ekip theo giá Grab hoặc Taxi",
        "visible": true
      },
      {
        "id": "newborn-3",
        "name": "Gói album sơ sinh",
        "subtitle": "Bé từ 0 - 5 tháng tuổi",
        "priceText": "3.800.000đ",
        "benefits": "Album thiết kế 20 trang (25 ảnh)\n3 Concept Sơ sinh\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 25x38cm\nEkip hỗ trợ chăm bé xuyên suốt buổi chụp\nChưa bao gồm chi phí di chuyển cho ekip theo giá Grab hoặc Taxi nếu chụp tại nhà",
        "visible": true
      },
      {
        "id": "newborn-4",
        "name": "Gói album sơ sinh size 25x38cm",
        "subtitle": "Bé từ 0 - 5 tháng tuổi",
        "priceText": "4.900.000đ",
        "benefits": "Album thiết kế 20 trang (30 ảnh)\n4 Concept Sơ sinh\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 35x50cm\nEkip hỗ trợ chăm bé xuyên suốt buổi chụp\nChưa bao gồm chi phí di chuyển cho ekip theo giá Grab hoặc Taxi nếu chụp tại nhà",
        "visible": true
      },
      {
        "id": "newborn-5",
        "name": "Gói album sơ sinh size 30x45cm",
        "subtitle": "Bé từ 0 - 5 tháng tuổi",
        "priceText": "5.950.000đ",
        "benefits": "Album thiết kế 20 trang (35 ảnh)\n4 Concept Sơ sinh\nHỗ trợ trang phục theo Concept\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 40x60cm\nEkip hỗ trợ chăm bé xuyên suốt buổi chụp\nChưa bao gồm chi phí di chuyển cho ekip theo giá Grab hoặc Taxi nếu chụp tại nhà",
        "visible": true
      }
    ]
  },
  {
    "id": "maternity",
    "title": "Chụp ảnh mẹ bầu",
    "description": "Khi tình yêu thương nảy mầm trước cả lần gặp gỡ đầu tiên.",
    "items": [
      {
        "id": "maternity-1",
        "name": "Gói ảnh đơn mẹ bầu",
        "subtitle": "",
        "priceText": "Thứ 2 - Thứ 6: 1.400.000đ\nThứ 7, Chủ nhật, ngày lễ: 1.700.000đ",
        "benefits": "2 Concept Mẹ bầu\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\n10 file chỉnh sửa resize 2500px\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "maternity-2",
        "name": "Gói album mẹ bầu size 20x20 cm",
        "subtitle": "",
        "priceText": "2.550.000đ",
        "benefits": "Album thiết kế 20 trang (21 ảnh)\n3 Concept Mẹ Bầu\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh để bàn chất liệu gỗ size 15x21cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "maternity-3",
        "name": "Gói album mẹ bầu size 20x30 cm",
        "subtitle": "",
        "priceText": "3.350.000đ",
        "benefits": "Album thiết kế 20 trang (25 ảnh)\n3 Concept Mẹ Bầu\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 20x30cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "maternity-4",
        "name": "Gói album mẹ bầu size 25x38 cm",
        "subtitle": "",
        "priceText": "4.400.000đ",
        "benefits": "Album thiết kế 20 trang (30 ảnh)\n4 Concept Mẹ Bầu\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 25x38cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "maternity-5",
        "name": "Gói album mẹ bầu size 30x45 cm",
        "subtitle": "",
        "priceText": "5.650.000đ",
        "benefits": "Album thiết kế 20 trang (35 ảnh)\n4 Concept Mẹ Bầu\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 30x45cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      }
    ]
  },
  {
    "id": "beauty",
    "title": "Chụp ảnh beauty - couple",
    "description": "Tự tin, rạng rỡ trong phiên bản đẹp nhất của chính bạn.",
    "items": [
      {
        "id": "beauty-1",
        "name": "Gói ảnh đơn Beauty",
        "subtitle": "",
        "priceText": "Thứ 2 - Thứ 6: 1.400.000đ\nThứ 7, Chủ nhật, ngày lễ: 1.700.000đ",
        "benefits": "2 Concept Beauty\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\n10 file chỉnh sửa resize 2500px\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "beauty-2",
        "name": "Gói ảnh đơn Couple",
        "subtitle": "",
        "priceText": "Thứ 2 - Thứ 6: 1.850.000đ\nThứ 7, Chủ nhật, ngày lễ: 2.100.000đ",
        "benefits": "2 Concept Couple\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\n10 file chỉnh sửa resize 2500px\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "beauty-3",
        "name": "Gói album beauty size 20x20 cm",
        "subtitle": "",
        "priceText": "2.550.000đ",
        "benefits": "Album thiết kế 20 trang (21 ảnh)\n3 Concept Beauty\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh để bàn chất liệu gỗ size 15x21cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "beauty-4",
        "name": "Gói album beauty size 20x30 cm",
        "subtitle": "",
        "priceText": "3.350.000đ",
        "benefits": "Album thiết kế 20 trang (25 ảnh)\n3 Concept Beauty\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 20x30cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "beauty-5",
        "name": "Gói album beauty size 25x38 cm",
        "subtitle": "",
        "priceText": "4.400.000đ",
        "benefits": "Album thiết kế 20 trang (30 ảnh)\n4 Concept Beauty\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 25x38cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      },
      {
        "id": "beauty-6",
        "name": "Gói album beauty size 30x45 cm",
        "subtitle": "",
        "priceText": "5.650.000đ",
        "benefits": "Album thiết kế 20 trang (35 ảnh)\n4 Concept Beauty\nHỗ trợ trang phục theo Concept\nMakeup Cơ Bản 1 người lớn\nTặng toàn bộ file resize buổi chụp JPEG 2500px\nTặng 1 ảnh phóng chất liệu gỗ size 30x45cm\nEkip hỗ trợ tạo dáng xuyên suốt buổi chụp",
        "visible": true
      }
    ]
  }
]
""";
}
