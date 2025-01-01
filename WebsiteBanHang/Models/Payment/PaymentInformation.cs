namespace WebsiteBanHang.Models.Payment
{
    public class PaymentInformation
    {
        public string OrderType { get; set; }
        public double Amount { get; set; }
        public string OrderDescription { get; set; }
        public string Name { get; set; }
        public string OrderId { get; set; }
    }
}