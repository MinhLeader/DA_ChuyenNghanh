using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteBanHang.Models;
using WebsiteBanHang.Common; // Thêm namespace cho VnPayLibrary

namespace WebsiteBanHang.Controllers
{
    public class GioHangController : Controller
    {
        QuanLyBanHangEntities db = new QuanLyBanHangEntities();

        //Hiển thị icon giỏ hàng lên phần headerr
        public ActionResult GioHangPartial()
        {
            if (TinhTongSoLuong() == 0) //ktra soluong giỏ hàng
            {
                ViewBag.TongSoLuong = 0;
                ViewBag.TongTien = 0;
                return PartialView();
            }
            //Hiển thị tổng tiền và sl sp lên trên icon giỏ hàng
            ViewBag.TongSoLuong = TinhTongSoLuong();
            ViewBag.TongTien = TinhTongTien();

            return PartialView();
        }

        //Thêm giỏ hàng thông thường ko ajax
        public ActionResult ThemGioHang(int MaSP, string strURL)
        {
            //ktra sp có tồn tại trong csdl ko
            SanPham sp = db.SanPhams.SingleOrDefault(n => n.MaSP == MaSP);  //lấy sp với masp tương ứng
            if(sp == null)  //nếu lấy sai masp
            {
                //Trang đường dẫn ko hợp lệ
                Response.StatusCode = 404;
                return null;
            }

            //Lấy giỏ hàng
            List<ItemGioHang> lstGioHang = LayGioHang();

            //trường hợp nếu 1 sp đã tồn tại trong giỏ hàng
            ItemGioHang spCheck = lstGioHang.SingleOrDefault(n => n.MaSP == MaSP);  //ktra sp có trong lst đã tạo hay ko
            if(spCheck != null)
            {
                //ktra số lg tồn trc khi cho kh mua hàng
                if (sp.SoLuongTon < spCheck.SoLuong)
                    return View("ThongBao");
                //nếu sp đã có trong list thì khi thêm vào giỏ hàng sẽ tăng số lượng lên
                spCheck.SoLuong++;
                //và đơn giá sẽ tăng theo giá sp * sl tương ứng
                spCheck.ThanhTien = spCheck.SoLuong * spCheck.DonGia;
                return Redirect(strURL);
            }
            
            //nếu chưa tồn tại thì thêm vào list
            ItemGioHang itemGH = new ItemGioHang(MaSP);
            if (sp.SoLuongTon < itemGH.SoLuong) //ktra số lg tồn trc khi cho kh mua hàng
                return View("ThongBao");
            lstGioHang.Add(itemGH);
            return Redirect(strURL);
        }
        
        // GET: GioHang
        //Trang xem giỏ hàng
        public ActionResult XemGioHang()
        {
            //lấy giỏ hàng đã đc tạo
            List<ItemGioHang> lstGioHang = LayGioHang();
            ViewBag.TongSoLuong = TinhTongSoLuong();
            ViewBag.TongTien = TinhTongTien();

            return View(lstGioHang);    //đưa list vào view
        }

        //chỉnh sửa giỏ hàng
        [HttpGet]
        public ActionResult SuaGioHang(int MaSP)
        {
            //ktra session giỏ hàng có tồn tại ko
            if(Session["GioHang"] == null)
            {
                return RedirectToAction("Index", "Home");   //quay về trang chủ
            }
            //ktra sp có tồn tại trong csdl ko
            SanPham sp = db.SanPhams.SingleOrDefault(n => n.MaSP == MaSP);
            if (sp == null)
            {
                //Trang đường dẫn ko hợp lệ
                Response.StatusCode = 404;
                return null;
            }

            //Lấy list giỏ hàng từ session
            List<ItemGioHang> lstGioHang = LayGioHang();

            //ktra xem sp đó có tồn tại trong giỏ hàng hay ko
            ItemGioHang spCheck = lstGioHang.SingleOrDefault(n => n.MaSP == MaSP);
            if(spCheck == null)
            {
                return RedirectToAction("Index", "Home");
            }

            //Lấy list giỏ hàng tạo giao diện
            ViewBag.GioHang = lstGioHang;

            //nếu tồn tại thì
            return View(spCheck);
        }

        //Cập nhật giỏ hàng
        [HttpPost]
        public ActionResult CapNhatGioHang(ItemGioHang itemGH)
        {
            //ktra số lượng tồn sau khi sửa
            SanPham spCheck = db.SanPhams.Single(n => n.MaSP == itemGH.MaSP);
            if(spCheck.SoLuongTon < itemGH.SoLuong)
            {
                return View("ThongBao");
            }

            //update số lg trong session giỏ hàng
            //bc1: Lấy list giỏ hàng từ sesssion giỏ hàng
            List<ItemGioHang> lstGH = LayGioHang();
            //bc2: lấy sp cần update từ trong list giỏ hàng
            ItemGioHang itemGHUpdate = lstGH.Find(n => n.MaSP == itemGH.MaSP);  //pt find dùng để tìm các trường mong muốn
            //bc3: update lại số lg và thành tiền
            itemGHUpdate.SoLuong = itemGH.SoLuong;
            itemGHUpdate.ThanhTien = itemGHUpdate.SoLuong * itemGHUpdate.DonGia;

            return RedirectToAction("XemGioHang");
        }

        //Xóa giỏ hàng
        public ActionResult XoaGioHang(int MaSP)
        {
            //ktra session giỏ hàng có tồn tại ko
            if (Session["GioHang"] == null)
            {
                return RedirectToAction("Index", "Home");
            }
            //ktra sp có tồn tại trong csdl ko
            SanPham sp = db.SanPhams.SingleOrDefault(n => n.MaSP == MaSP);
            if (sp == null)
            {
                //Trang đường dẫn ko hợp lệ
                Response.StatusCode = 404;
                return null;
            }
            //Lấy list giỏ hàng từ session
            List<ItemGioHang> lstGioHang = LayGioHang();
            //ktra xem sp đó có tồn tại trong giỏ hàng hay ko
            ItemGioHang spCheck = lstGioHang.SingleOrDefault(n => n.MaSP == MaSP);
            if (spCheck == null)
            {
                return RedirectToAction("Index", "Home");
            }
            //Xóa item trong giỏ hàng
            lstGioHang.Remove(spCheck);

            return RedirectToAction("XemGioHang");
        }

        //chức năng đặt hàng
        public ActionResult DatHang(KhachHang kh)
        {
            System.Diagnostics.Debug.WriteLine("========== BẮT ĐẦU QUY TRÌNH ĐẶT HÀNG ==========");

            //kiểm tra session giỏ hàng
            if (Session["GioHang"] == null)
            {
                System.Diagnostics.Debug.WriteLine("Không tìm thấy giỏ hàng trong session");
                return RedirectToAction("Index", "Home");
            }

            // Log thông tin giỏ hàng trước khi xử lý
            List<ItemGioHang> currentCart = Session["GioHang"] as List<ItemGioHang>;
            System.Diagnostics.Debug.WriteLine($"Số sản phẩm trong giỏ: {currentCart?.Count ?? 0}");

            //xử lý khách hàng
            KhachHang khach = new KhachHang();
            if (Session["TaiKhoan"] == null)
            {
                System.Diagnostics.Debug.WriteLine("Khách hàng: Vãng lai");
                khach = kh;
                db.KhachHangs.Add(khach);
                db.SaveChanges();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Khách hàng: Thành viên");
                ThanhVien tv = (ThanhVien)Session["TaiKhoan"];
                khach.TenKH = tv.HoTen;
                khach.DiaChi = tv.DiaChi;
                khach.Email = tv.Email;
                khach.SoDienThoai = tv.SoDienThoai;
                db.KhachHangs.Add(khach);
                db.SaveChanges();
            }
            System.Diagnostics.Debug.WriteLine($"Mã khách hàng: {khach.MaKH}, Tên: {khach.TenKH}");

            //tạo đơn hàng
            DonDatHang ddh = new DonDatHang();
            ddh.MaKH = khach.MaKH;
            ddh.NgayDat = DateTime.Now;
            ddh.TinhTrangGiaoHang = false;
            ddh.DaThanhToan = false;
            ddh.UuDai = 0;
            ddh.DaHuy = false;
            ddh.DaXoa = false;
            db.DonDatHangs.Add(ddh);
            db.SaveChanges();
            System.Diagnostics.Debug.WriteLine($"Đã tạo đơn hàng mới - Mã đơn: {ddh.MaDDH}");

            //thêm chi tiết đơn hàng
            decimal tongTienChiTiet = 0;
            List<ItemGioHang> lstGH = LayGioHang();
            foreach (var item in lstGH)
            {
                ChiTietDonDatHang ctdh = new ChiTietDonDatHang();
                ctdh.MaDDH = ddh.MaDDH;
                ctdh.MaSP = item.MaSP;
                ctdh.TenSP = item.TenSP;
                ctdh.SoLuong = item.SoLuong;
                ctdh.Dongia = item.DonGia;
                tongTienChiTiet += item.ThanhTien;
                db.ChiTietDonDatHangs.Add(ctdh);

                System.Diagnostics.Debug.WriteLine($"Chi tiết đơn hàng: SP={item.TenSP}, SL={item.SoLuong}, Đơn giá={item.DonGia}, Thành tiền={item.ThanhTien}");
            }
            db.SaveChanges();
            System.Diagnostics.Debug.WriteLine($"Tổng tiền tính từ chi tiết: {tongTienChiTiet}");

            // Xử lý thanh toán VNPay
            decimal totalAmount = TinhTongTien();
            System.Diagnostics.Debug.WriteLine($"Tổng tiền từ TinhTongTien(): {totalAmount}");

            // Kiểm tra tổng tiền có khớp không
            if (totalAmount != tongTienChiTiet)
            {
                System.Diagnostics.Debug.WriteLine("CẢNH BÁO: Không khớp giữa tổng tiền và chi tiết!");
            }

            string orderId = ddh.MaDDH.ToString();

            // Convert to VNPay amount (nhân 100)
            long vnpayAmount = (long)(totalAmount * 100);
            System.Diagnostics.Debug.WriteLine($"Số tiền gửi đến VNPay (đã nhân 100): {vnpayAmount}");

            Models.Payment.PaymentInformation paymentInfo = new Models.Payment.PaymentInformation()
            {
                Amount = (double)totalAmount,
                OrderDescription = "Thanh toán đơn hàng #" + orderId,
                OrderType = "other",
                OrderId = orderId,
                Name = khach.TenKH
            };
            System.Diagnostics.Debug.WriteLine($"Payment Info - Amount: {paymentInfo.Amount}, OrderId: {paymentInfo.OrderId}");

            VnPayLibrary vnpay = new VnPayLibrary();

            // Log tất cả các tham số VNPay
            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", System.Configuration.ConfigurationManager.AppSettings["Vnpay_TmnCode"]);
            vnpay.AddRequestData("vnp_Amount", vnpayAmount.ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", GetIpAddress());
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", paymentInfo.OrderDescription);
            vnpay.AddRequestData("vnp_ReturnUrl", System.Configuration.ConfigurationManager.AppSettings["Vnpay_ReturnUrl"]);
            vnpay.AddRequestData("vnp_TxnRef", paymentInfo.OrderId);
            vnpay.AddRequestData("vnp_OrderType", paymentInfo.OrderType);
            vnpay.AddRequestData("vnp_Inv_Customer", paymentInfo.Name);

            // Log các tham số quan trọng
            System.Diagnostics.Debug.WriteLine($"TmnCode: {System.Configuration.ConfigurationManager.AppSettings["Vnpay_TmnCode"]}");
            System.Diagnostics.Debug.WriteLine($"ReturnUrl: {System.Configuration.ConfigurationManager.AppSettings["Vnpay_ReturnUrl"]}");
            System.Diagnostics.Debug.WriteLine($"IP Address: {GetIpAddress()}");

            // Tạo URL thanh toán và log
            string paymentUrl = vnpay.CreateRequestUrl(
                System.Configuration.ConfigurationManager.AppSettings["Vnpay_Url"],
                System.Configuration.ConfigurationManager.AppSettings["Vnpay_HashSecret"]
            );
            System.Diagnostics.Debug.WriteLine($"Payment URL: {paymentUrl}");

            // Log hash data để kiểm tra
            

            System.Diagnostics.Debug.WriteLine("========== KẾT THÚC QUY TRÌNH ĐẶT HÀNG ==========");

            // Xóa giỏ hàng
            Session["GioHang"] = null;

            return Redirect(paymentUrl);
        }

        private string GetIpAddress()
        {
            string ipAddress = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (!string.IsNullOrEmpty(ipAddress))
            {
                string[] addresses = ipAddress.Split(',');
                if (addresses.Length != 0)
                {
                    return addresses[0];
                }
            }

            return Request.ServerVariables["REMOTE_ADDR"];
        }


        #region Methods
        //Lấy giỏ hàng
        public List<ItemGioHang> LayGioHang()
        {
            //nếu giỏ hàng đã tồn tại
            List<ItemGioHang> lstGioHang = Session["GioHang"] as List<ItemGioHang>; //lưu giỏ hàng vào session giohang để quản lý
            if (lstGioHang == null)
            {
                //Nếu session giỏ hàng ko tồn tại thì khởi tạo giỏ hàng
                lstGioHang = new List<ItemGioHang>();
                Session["GioHang"] = lstGioHang;
            }
            return lstGioHang;
        }

        // tính tổng số lg
        public double TinhTongSoLuong()
        {
            //lấy giỏ hàng
            List<ItemGioHang> lstGioHang = Session["GioHang"] as List<ItemGioHang>;
            if(lstGioHang == null)  //nếu chưa có list giỏ hàng thì trả về gtri = 0
            {
                return 0;
            }
            return lstGioHang.Sum(n => n.SoLuong); //trả về tổng số lượng của list giỏ hàng
        }

        //tính tổng tiền
        public decimal TinhTongTien()
        {
            //lấy giỏ hàng
            List<ItemGioHang> lstGioHang = Session["GioHang"] as List<ItemGioHang>;
            if (lstGioHang == null) //nếu chưa có list giỏ hàng thì trả về gtri = 0
            {
                return 0;
            }
            return lstGioHang.Sum(n => n.ThanhTien);    //trả về tổng tiền của list giỏ hàng
        }
        #endregion

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

        // thông báo đặt hàng thành công
        public ActionResult DatHangThanhCong()
        {
            return View();
        }
    }
}