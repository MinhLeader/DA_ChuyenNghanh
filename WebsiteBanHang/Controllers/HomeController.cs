using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteBanHang.Models;
using System.Web.Security;
using CaptchaMvc.HtmlHelpers;
using CaptchaMvc;
using FaceRecognitionDotNet;
using System.IO;
using System.Data.Entity.Validation;

namespace WebsiteBanHang.Controllers
{
    public class HomeController : Controller
    {
        QuanLyBanHangEntities db = new QuanLyBanHangEntities();
        private static readonly string ModelsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        private static readonly FaceRecognition _faceRecognition = FaceRecognition.Create(ModelsDirectory);

        // GET: Home/Index
        public ActionResult Index()
        {
            //Lần lượt tạo các viewbag để lấy list sp từ csdl
            //List laptop mới
            var lstLTM = db.SanPhams.Where(n => n.MaLoaiSP == 1 && n.Moi == 1 && n.DaXoa == false).ToList();
            //Gán vào viewbag
            ViewBag.ListLTM = lstLTM;

            //List PC mới
            var lstPCM = db.SanPhams.Where(n => n.MaLoaiSP == 2 && n.Moi == 1 && n.DaXoa == false).ToList();
            //Gán vào viewbag
            ViewBag.ListPCM = lstPCM;

            //List dt mới
            var lstDTM = db.SanPhams.Where(n => n.MaLoaiSP == 7 && n.Moi == 1 && n.DaXoa == false).ToList();
            //Gán vào viewbag
            ViewBag.ListDTM = lstDTM;

            return View();
        }

        public ActionResult MenuPartial()
        {
            //Lấy ra 1 lst sanpham và truyền trực tiếp vào partial
            var lstSP = db.SanPhams;

            return PartialView(lstSP);
        }

        [HttpGet]
        public ActionResult DangKy()
        {
            //đặt trùng tên viewbag giống trong bảng
            ViewBag.CauHoi = new SelectList(LoadCauHoi());  //gắn các câu hỏi vào viewbag để hiển thị lên view

            return View();
        }
        [HttpPost]
        public ActionResult DangKy(ThanhVien tv)    //dùng post để truyền data lên csdl, dùng biến tv trong model thay formcollection
        {
            ViewBag.CauHoi = new SelectList(LoadCauHoi());  //lưu câu hỏi đã chọn trong dropdownlist vào csdl
            //Kiểm tra captcha hợp lệ
            if (this.IsCaptchaValid("Captcha không hợp lệ!"))   //nếu captcha hợp lệ
            {
                if (ModelState.IsValid)
                {
                    tv.MaLoaiTV = 3;
                    ViewBag.ThongBao = "Thêm thành công";
                    //Thêm khách hàng vào csdl
                    db.ThanhViens.Add(tv);  //sau khi lấy được các thuộc tính trong biến tv qua các textbox thì truyền tv vào dbset ThanhViens
                    //Lưu thay đổi
                    db.SaveChanges();   //lấy data từ dbset chuyển vào csdl
                }
                return View();
            }
            ViewBag.ThongBao = "Sai mã Captcha";
            return View();
        }

        //Load câu hỏi để đưa vào dropdownlist
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

        //xd action dang nhap
        [HttpPost]
        public ActionResult DangNhap(FormCollection f)
        {
            //ktra tên dn và pass
            string taikhoan = f["txtTenDangNhap"].ToString();   //lấy chuỗi trong txtTenDangNhap
            string matKhau = f["txtMatKhau"].ToString();    //lấy chuỗi trong txtMatKhau

            ThanhVien tv = db.ThanhViens.SingleOrDefault(n => n.TaiKhoan == taikhoan && n.MatKhau == matKhau);      //so sánh với tk và mk trong csdl
            if (tv != null)
            {
                var lstQuyen = db.LoaiThanhVien_Quyen.Where(n => n.MaLoaiTV == tv.MaLoaiTV);   //lấy ra list quyền tương ứng loaitv
                string Quyen = "";
                if (lstQuyen.Count() != 0)
                {
                    foreach (var item in lstQuyen)   //duyệt list quyền
                    {
                        Quyen += item.Quyen.MaQuyen + ",";
                    }
                    Quyen = Quyen.Substring(0, Quyen.Length - 1); //Cắt dấu ,
                    PhanQuyen(tv.TaiKhoan.ToString(), Quyen);

                    Session["TaiKhoan"] = tv;
                    return Content(@"<script>window.location.reload()</script>");   //đoạn script dùng để reload lại trang khi đăng nhập thành công
                }
            }
            return Content("Tài khoản hoặc mật khẩu không chính xác.");
        }

        // Xử lý nhận diện khuôn mặt để đăng nhập
        [HttpPost]
        public ActionResult DangNhapFaceRecognition(HttpPostedFileBase image)
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
                    // Kiểm tra quyền của thành viên
                    var lstQuyen = db.LoaiThanhVien_Quyen.Where(n => n.MaLoaiTV == thanhVien.MaLoaiTV);
                    string Quyen = "";
                    if (lstQuyen.Count() != 0)
                    {
                        foreach (var item in lstQuyen)
                        {
                            Quyen += item.Quyen.MaQuyen + ",";
                        }
                        Quyen = Quyen.Substring(0, Quyen.Length - 1);
                        PhanQuyen(thanhVien.TaiKhoan.ToString(), Quyen);

                        Session["TaiKhoan"] = thanhVien;
                        // Trả về thông báo thành công
                        return Json(new { success = true, message = "Đăng nhập thành công." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Tài khoản không có quyền truy cập." });
                    }
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

        public ActionResult DangXuat()
        {
            Session["TaiKhoan"] = null;

            FormsAuthentication.SignOut();  //Xóa Forms Authentication cookie

            return RedirectToAction("Index");
        }

        public ActionResult LoiPhanquyen()
        {
            return View();
        }

        public void PhanQuyen(string TaiKhoan, string Quyen)
        {
            FormsAuthentication.Initialize();

            var ticket = new FormsAuthenticationTicket(1,
                                            TaiKhoan,   //đặt tên ticket theo tên tk 
                                            DateTime.Now,   //lấy tgian bắt đầu
                                            DateTime.Now.AddHours(3),   //thời gian 3 tiếng out ra
                                            false,  //ko lưu
                                            Quyen,  //Lấy chuỗi phân quyền
                                            FormsAuthentication.FormsCookiePath);   //Lấy đg dẫn cookie thay vì name
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket));  //tạo cookie(tự tạo name, mã hóa thông tin ticket add vào cookie)
            if (ticket.IsPersistent) cookie.Expires = ticket.Expiration;    //ktra cookie có chưa
            Response.Cookies.Add(cookie);     //
        }

        //bổ sung hàm giống với code ở QuanLyThanhVien
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
        }

        private ThanhVien TimThanhVienTheoEncoding(string encoding)
        {
            var encodingArray = ConvertStringToDoubleArray(encoding);

            var thanhViens = db.ThanhViens.ToList();

            foreach (var tv in thanhViens)
            {
                if (!string.IsNullOrEmpty(tv.FaceEncoding))
                {
                    var tvEncodingArray = ConvertStringToDoubleArray(tv.FaceEncoding);

                    if (AreEncodingsSimilar(encodingArray, tvEncodingArray))
                    {
                        return tv;
                    }
                }
            }

            return null;
        }

        private double[] ConvertStringToDoubleArray(string encodingString)
        {
            if (string.IsNullOrEmpty(encodingString))
                return new double[0];

            try
            {
                return encodingString.Split(',').Select(double.Parse).ToArray();
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Lỗi định dạng khi chuyển đổi encoding: {ex.Message}");
                return new double[0];
            }
        }

        private bool AreEncodingsSimilar(double[] encoding1, double[] encoding2, double tolerance = 0.4)
        {
            if (encoding1.Length != encoding2.Length)
            {
                return false;
            }

            // Tính khoảng cách Euclidean
            var distance = Math.Sqrt(encoding1.Zip(encoding2, (a, b) => (a - b) * (a - b)).Sum());

            // Giảm ngưỡng tolerance để tăng độ chính xác
            return distance < tolerance;
        }

        private bool IsValidFaceImage(string imagePath)
        {
            using (var image = FaceRecognition.LoadImageFile(imagePath))
            {
                var faceLocations = _faceRecognition.FaceLocations(image).ToArray();

                // Chỉ chấp nhận ảnh có duy nhất 1 khuôn mặt
                return faceLocations.Length == 1;
            }
        }

        private void DeleteTempImage(string imagePath)
        {
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }
        }

        //Giải phóng dung lượng biến db, để ở cuối controller
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (db != null)
                    db.Dispose();
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
