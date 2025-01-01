using System;
using System.Web.Mvc;
using WebsiteBanHang.Common;
using WebsiteBanHang.Models;
using System.Linq;

namespace WebsiteBanHang.Controllers
{
    public class PaymentController : Controller
    {
        QuanLyBanHangEntities db = new QuanLyBanHangEntities();

        public ActionResult PaymentCallback()
        {
            VnPayLibrary vnpay = new VnPayLibrary();

            // Lấy dữ liệu trả về từ VNPay
            foreach (string key in Request.QueryString.AllKeys)
            {
                vnpay.AddResponseData(key, Request.QueryString[key]);
            }

            // Lấy mã đơn hàng và mã giao dịch VNPay
            string orderId = vnpay.GetResponseData("vnp_TxnRef");
            string vnpayTranId = vnpay.GetResponseData("vnp_TransactionNo");
            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];
            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, System.Configuration.ConfigurationManager.AppSettings["Vnpay_HashSecret"]);
            if (checkSignature)
            {
                if (vnp_ResponseCode == "00") // Thanh toán thành công
                {
                    DonDatHang ddh = db.DonDatHangs.SingleOrDefault(n => n.MaDDH.ToString() == orderId);
                    if (ddh != null)
                    {
                        ddh.DaThanhToan = true;
                        // ddh.MaGiaoDichVNPAY = vnpayTranId; // Lưu mã giao dịch VNPay
                        db.SaveChanges();
                    }

                    ViewBag.Message = "Thanh toán thành công!";
                    return View("PaymentSuccess");
                }
                else
                {
                    ViewBag.Message = "Thanh toán thất bại. Mã lỗi: " + vnp_ResponseCode;
                    DonDatHang ddh = db.DonDatHangs.SingleOrDefault(n => n.MaDDH.ToString() == orderId);
                    if (ddh != null)
                    {
                        ddh.DaThanhToan = false;
                        db.SaveChanges();
                    }
                    return View("PaymentFail");
                }
            }
            else
            {
                ViewBag.Message = "Chữ ký không hợp lệ!";
                return View("PaymentFail");
            }
        }
        // Tạo view PaymentSuccess
        public ActionResult PaymentSuccess()
        {
            return View();
        }

        // Tạo view PaymentFail
        public ActionResult PaymentFail()
        {
            return View();
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