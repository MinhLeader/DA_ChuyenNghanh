using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteBanHang.Models;
using FaceRecognitionDotNet;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data.Entity.Validation;
using System.Text;
using Newtonsoft.Json;

namespace WebsiteBanHang.Controllers
{
    [Authorize(Roles = "QuanTriWeb")]
    public class QuanLyThanhVienController : Controller
    {
        private QuanLyBanHangEntities db = new QuanLyBanHangEntities();
        private static readonly FaceRecognition _faceRecognition = FaceRecognition.Create(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models"));

        // GET: QuanLyThanhVien
        public ActionResult Index()
        {
            // Lấy danh sách thành viên và sắp xếp theo MaThanhVien
            var model = db.ThanhViens.OrderBy(n => n.MaThanhVien).ToList();
            return View(model);
        }

        // Tạo mới thành viên - GET
        [HttpGet]
        public ActionResult TaoMoi()
        {
            ViewBag.CauHoi = LoadCauHoi();
            ViewBag.MaLoaiTV = new SelectList(db.LoaiThanhViens.OrderBy(n => n.MaLoaiTV), "MaLoaiTV", "TenLoai");

            return View(new ThanhVien());
        }

        // Tạo mới thành viên - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TaoMoi(ThanhVien tv, HttpPostedFileBase FaceImage)
        {
            ViewBag.CauHoi = new SelectList(LoadCauHoi());
            ViewBag.MaLoaiTV = new SelectList(db.LoaiThanhViens.OrderBy(n => n.MaLoaiTV), "MaLoaiTV", "TenLoai");

            // Kiểm tra ModelState
            if (!ModelState.IsValid)
            {
                return View(tv);
            }

            // Kiểm tra nếu FaceImage được tải lên
            if (FaceImage == null || FaceImage.ContentLength <= 0)
            {
                ModelState.AddModelError("FaceImage", "Hãy chọn ảnh khuôn mặt.");
                return View(tv);
            }

            if (FaceImage != null && FaceImage.ContentLength > 0)
            {
                var tempFolderPath = Server.MapPath("~/Temp");
                Directory.CreateDirectory(tempFolderPath);
                var fileName = Path.GetFileName(FaceImage.FileName);
                var tempImagePath = Path.Combine(tempFolderPath, fileName);

                FaceImage.SaveAs(tempImagePath);

                // Thêm log để kiểm tra đường dẫn ảnh
                Console.WriteLine($"Đường dẫn ảnh tạm: {tempImagePath}");

                tv.FaceEncoding = GetFaceEncodingFromImage(tempImagePath);

                // Thêm log để kiểm tra FaceEncoding
                Console.WriteLine($"FaceEncoding: {tv.FaceEncoding ?? "null"}");

                if (tv.FaceEncoding == null)
                {
                    ModelState.AddModelError("FaceImage", "Không nhận diện được khuôn mặt trong ảnh.");
                    DeleteTempImage(tempImagePath); //Xóa ảnh tạm
                    return View(tv);
                }
            }

            try
            {
                db.ThanhViens.Add(tv);
                int result = db.SaveChanges();

                // Thêm log để kiểm tra số bản ghi được lưu
                Console.WriteLine($"Số bản ghi được lưu: {result}");
            }
            catch (DbEntityValidationException ex)
            {
                // Log chi tiết các lỗi validation
                foreach (var entityValidationError in ex.EntityValidationErrors)
                {
                    foreach (var validationError in entityValidationError.ValidationErrors)
                    {
                        Console.WriteLine($"Lỗi validation - Thuộc tính: {validationError.PropertyName}, Lỗi: {validationError.ErrorMessage}");
                    }
                }
                // Thêm ViewBag và return View(tv) để hiển thị thông báo lỗi
                ViewBag.CauHoi = new SelectList(LoadCauHoi());
                ViewBag.MaLoaiTV = new SelectList(db.LoaiThanhViens.OrderBy(n => n.MaLoaiTV), "MaLoaiTV", "TenLoai");
                return View(tv);
            }

            return RedirectToAction("Index");
        }

        // Chỉnh sửa thành viên - GET
        [HttpGet]
        public ActionResult ChinhSua(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            ThanhVien tv = db.ThanhViens.Find(id);
            if (tv == null)
            {
                return HttpNotFound();
            }

            ViewBag.CauHoi = LoadCauHoi();
            ViewBag.MaLoaiTV = TaoDanhSachLoaiTV(tv.MaLoaiTV ?? 0);

            return View(tv);
        }

        // Chỉnh sửa thành viên - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChinhSua(ThanhVien tv, HttpPostedFileBase FaceImage)
        {
            if (ModelState.IsValid)
            {
                // Tìm nạp thông tin thành viên hiện tại từ cơ sở dữ liệu
                var existingThanhVien = db.ThanhViens.Find(tv.MaThanhVien);

                if (existingThanhVien == null)
                {
                    return HttpNotFound();
                }

                // Giữ nguyên FaceEncoding nếu không có ảnh mới được tải lên
                if (FaceImage == null || FaceImage.ContentLength == 0)
                {
                    tv.FaceEncoding = existingThanhVien.FaceEncoding;
                }
                else
                {
                    // Xử lý ảnh mới tải lên
                    var tempFolderPath = Server.MapPath("~/Temp");
                    Directory.CreateDirectory(tempFolderPath);

                    var fileName = Path.GetFileName(FaceImage.FileName);
                    var fileExtension = Path.GetExtension(fileName).ToLower();
                    var tempImagePath = Path.Combine(tempFolderPath, fileName);

                    if (!IsValidImageExtension(fileExtension))
                    {
                        ModelState.AddModelError("FaceImage", "Định dạng ảnh không hợp lệ.");
                        ViewBag.CauHoi = new SelectList(LoadCauHoi(), selectedValue: tv.CauHoi);
                        ViewBag.MaLoaiTV = TaoDanhSachLoaiTV(tv.MaLoaiTV ?? 0);
                        return View(tv);
                    }

                    FaceImage.SaveAs(tempImagePath);
                    tv.FaceEncoding = GetFaceEncodingFromImage(tempImagePath);

                    if (tv.FaceEncoding == null)
                    {
                        ModelState.AddModelError("FaceImage", "Không nhận diện được khuôn mặt trong ảnh. Hãy thử ảnh khác.");
                        DeleteTempImage(tempImagePath);
                        ViewBag.CauHoi = new SelectList(LoadCauHoi(), selectedValue: tv.CauHoi);
                        ViewBag.MaLoaiTV = TaoDanhSachLoaiTV(tv.MaLoaiTV ?? 0);
                        return View(tv);
                    }

                    tv.FaceImagePath = Url.Content(Path.Combine("~/Temp", fileName));
                    DeleteTempImage(tempImagePath);
                }

                // Cập nhật thông tin thành viên với dữ liệu từ form
                existingThanhVien.TaiKhoan = tv.TaiKhoan;
                existingThanhVien.MatKhau = tv.MatKhau;
                existingThanhVien.HoTen = tv.HoTen;
                existingThanhVien.DiaChi = tv.DiaChi;
                existingThanhVien.Email = tv.Email;
                existingThanhVien.SoDienThoai = tv.SoDienThoai;
                existingThanhVien.CauHoi = tv.CauHoi;
                existingThanhVien.CauTraLoi = tv.CauTraLoi;
                existingThanhVien.MaLoaiTV = tv.MaLoaiTV;
                existingThanhVien.FaceEncoding = tv.FaceEncoding;

                try
                {
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var entityValidationError in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in entityValidationError.ValidationErrors)
                        {
                            ModelState.AddModelError(validationError.PropertyName, validationError.ErrorMessage);
                        }
                    }
                }
            }

            ViewBag.CauHoi = new SelectList(LoadCauHoi(), selectedValue: tv.CauHoi);
            ViewBag.MaLoaiTV = TaoDanhSachLoaiTV(tv.MaLoaiTV ?? 0);
            return View(tv);
        }

        // Xóa thành viên - GET
        [HttpGet]
        public ActionResult Xoa(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            ThanhVien tv = db.ThanhViens.Find(id);
            if (tv == null)
            {
                return HttpNotFound();
            }
            ViewBag.CauHoi = LoadCauHoi();
            ViewBag.MaLoaiTV = TaoDanhSachLoaiTV(tv.MaLoaiTV.Value);
            return View(tv);
        }

        // Xóa thành viên - POST
        [HttpPost, ActionName("Xoa")]
        [ValidateAntiForgeryToken]
        public ActionResult XoaConfirmed(int id)
        {
            ThanhVien tv = db.ThanhViens.Find(id);
            if (tv == null)
            {
                return HttpNotFound();
            }

            db.ThanhViens.Remove(tv);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // Tạo danh sách loại thành viên
        private SelectList TaoDanhSachLoaiTV(int IDChon = 0)
        {
            var items = db.LoaiThanhViens.Select(p => new { p.MaLoaiTV, ThongTin = p.TenLoai }).ToList();
            var result = new SelectList(items, "MaLoaiTV", "ThongTin", selectedValue: IDChon);
            return result;
        }

        // Tải danh sách câu hỏi
        public SelectList LoadCauHoi()
        {
            var items = new SelectList(new[]
            {
                "Họ tên người cha bạn là gì?",
                "Ca sĩ mà bạn yêu thích là ai?",
                "Vật nuôi mà bạn yêu thích là gì?",
                "Sở thích của bạn là gì",
                "Hiện tại bạn đang làm công việc gì?",
                "Trường cấp ba bạn học là gì?",
                "Năm sinh của mẹ bạn là gì?",
                "Bộ phim mà bạn yêu thích là gì?",
                "Bài nhạc mà bạn yêu thích là gì?",
             });

            return items;
        }

        // Nhận diện khuôn mặt - POST
        [HttpPost]
        public ActionResult NhanDienKhuonMat(HttpPostedFileBase image)
        {
            if (image == null || image.ContentLength == 0)
            {
                return Json(new { success = false, message = "Không nhận được ảnh." });
            }

            try
            {
                // Tạo thư mục tạm nếu chưa có
                var tempFolderPath = Server.MapPath("~/Temp");
                Directory.CreateDirectory(tempFolderPath); // Đảm bảo thư mục tồn tại

                // Lưu ảnh vào thư mục tạm
                var fileName = Path.GetFileName(image.FileName);
                var tempImagePath = Path.Combine(tempFolderPath, fileName);
                image.SaveAs(tempImagePath);

                // Trích xuất encoding
                var encoding = GetFaceEncodingFromImage(tempImagePath);

                // Xóa ảnh tạm
                DeleteTempImage(tempImagePath);

                if (string.IsNullOrEmpty(encoding))
                {
                    return Json(new { success = false, message = "Không nhận diện được khuôn mặt." });
                }

                // So sánh encoding với các thành viên trong database
                var thanhVien = TimThanhVienTheoEncoding(encoding);

                if (thanhVien != null)
                {
                    // Tìm thấy thành viên
                    return Json(new { success = true, thanhVienId = thanhVien.MaThanhVien, encoding });
                }
                else
                {
                    // Không tìm thấy thành viên
                    return Json(new { success = false, message = "Không tìm thấy thành viên khớp với khuôn mặt." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // Chuyển đổi chuỗi encoding thành mảng double
        private double[] ConvertStringToDoubleArray(string encodingString)
        {
            if (string.IsNullOrEmpty(encodingString))
                return new double[0]; // Hoặc null, tùy thuộc vào cách bạn muốn xử lý

            try
            {
                return encodingString.Split(',').Select(double.Parse).ToArray();
            }
            catch (FormatException ex)
            {
                // Xử lý lỗi định dạng ở đây, ví dụ:
                Console.WriteLine($"Lỗi định dạng khi chuyển đổi encoding: {ex.Message}");
                // Có thể trả về null hoặc ném ra một ngoại lệ tùy chỉnh
                return new double[0]; // Hoặc null, nếu bạn chọn trả về null trong trường hợp lỗi
            }
        }

        // Tìm thành viên dựa trên FaceEncoding
        private ThanhVien TimThanhVienTheoEncoding(string encoding)
        {
            // Chuyển đổi encoding từ string sang mảng double
            var encodingArray = ConvertStringToDoubleArray(encoding);

            // Lấy danh sách tất cả thành viên
            var thanhViens = db.ThanhViens.ToList();

            foreach (var tv in thanhViens)
            {
                if (!string.IsNullOrEmpty(tv.FaceEncoding))
                {
                    // Chuyển đổi FaceEncoding của thành viên từ string sang mảng double
                    var tvEncodingArray = ConvertStringToDoubleArray(tv.FaceEncoding);

                    // So sánh hai mảng encoding
                    if (AreEncodingsSimilar(encodingArray, tvEncodingArray))
                    {
                        return tv; // Trả về thành viên nếu encoding tương tự
                    }
                }
            }

            return null; // Không tìm thấy thành viên nào có encoding tương tự
        }

        // So sánh hai mảng encoding
        private bool AreEncodingsSimilar(double[] encoding1, double[] encoding2, double tolerance = 0.6)
        {
            if (encoding1.Length != encoding2.Length)
            {
                return false;
            }

            // Tính khoảng cách Euclidean giữa hai mảng encoding
            var distance = Math.Sqrt(encoding1.Zip(encoding2, (a, b) => (a - b) * (a - b)).Sum());

            return distance < tolerance;
        }

        // Hàm kiểm tra định dạng ảnh
        private bool IsValidImageExtension(string fileExtension)
        {
            string[] validExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
            return validExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        // Trích xuất đặc trưng khuôn mặt
        private string GetFaceEncodingFromImage(string imagePath)
        {
            try
            {
                using (var image = FaceRecognition.LoadImageFile(imagePath))
                {
                    var faceLocations = _faceRecognition.FaceLocations(image).ToArray();

                    // Thêm log để kiểm tra số lượng khuôn mặt
                    Console.WriteLine($"Số khuôn mặt tìm thấy: {faceLocations.Length}");

                    if (faceLocations.Length == 0)
                    {
                        // Thêm log chi tiết
                        Console.WriteLine("Không tìm thấy khuôn mặt trong ảnh");
                        return null;
                    }

                    var faceEncodings = _faceRecognition.FaceEncodings(image, faceLocations).ToArray();

                    // Thêm log để kiểm tra số lượng encoding
                    Console.WriteLine($"Số encoding tìm thấy: {faceEncodings.Length}");

                    if (faceEncodings.Length == 0)
                    {
                        Console.WriteLine("Không thể trích xuất đặc trưng khuôn mặt");
                        return null;
                    }

                    var encoding = faceEncodings.FirstOrDefault();

                    if (encoding == null)
                    {
                        Console.WriteLine("Encoding là null");
                        return null;
                    }

                    // Chuyển đổi encoding sang base64
                    var rawEncoding = encoding.GetRawEncoding();
                    var stringEncoding = string.Join(",", rawEncoding);
                    return stringEncoding;
                }
            }
            catch (Exception ex)
            {
                // Ghi log đầy đủ thông tin lỗi
                Console.WriteLine($"Lỗi nhận diện khuôn mặt: {ex.Message}");
                Console.WriteLine($"Chi tiết lỗi: {ex.StackTrace}");
                return null;
            }
        }        // Xóa ảnh tạm
        private void DeleteTempImage(string imagePath)
        {
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }
        }

        // Giải phóng tài nguyên
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (db != null)
                {
                    db.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}