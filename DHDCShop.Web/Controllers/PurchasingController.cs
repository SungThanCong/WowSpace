using Common.Util;
using DHDCShop.Common.Util;
using DHDCShop.Models;
using DHDCShop.Models.Model;
using DHDCShop.Models.ViewModel;
using PayPal.Api;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Order = DHDCShop.Models.Model.Order;

namespace DHDCShop.Web.Controllers
{
    [Authorize(Roles ="User")]
    public class PurchasingController : Controller
    {
        // GET: Purchasing
        DHDCShopDbContext db = new DHDCShopDbContext();

        private const string SESSIONCART = "cart";
        // GET: Purchasing
        public ActionResult Index()
        {
            var username = User.Identity.Name;
            var customer = db.Customers.Find(username);
            return View(customer);
        }



        public ActionResult Payment(FormCollection form)
        {
            if (User.Identity.IsAuthenticated)
            {
                ViewBag.form = form;
                PaymentViewModel statePay = new PaymentViewModel();
                statePay.Phone = form["phone"];
                statePay.Email = form["email"];
                statePay.Fullname = form["fullname"];
                statePay.Country = form["country"];
                statePay.City = form["city"];
                statePay.Address = form["address"];
                statePay.Apartment = form["apartment"];
                statePay.ZipCode = form["zip"];
                Session["payment"] = statePay;

                return View();
            }
            else
            {
                return RedirectToAction("SignInUp", "User");
            }

        }
        //Cart
        [AllowAnonymous]
        public PartialViewResult AddNewItemToCart(int productId, string size, int quantity)
        {
            var cart = Session[SESSIONCART];
            if (cart != null)
            {
                var list = (List<CartItemViewModel>)cart;
                if (list.Exists(x => (x.Product.ProductId == productId && x.Size == size)))
                {
                    var update = list.Find(x => x.Product.ProductId == productId && x.Size == size);
                    update.Quantity += quantity;
                }
                else
                {
                    CartItemViewModel item = new CartItemViewModel();
                    item.Product = db.Products.Find(productId);
                    item.Quantity = quantity;
                    item.Size = size;
                    list.Add(item);
                }
                Session[SESSIONCART] = list;
            }
            else
            {
                CartItemViewModel item = new CartItemViewModel();
                item.Product = db.Products.Find(productId);
                item.Quantity = quantity;
                item.Size = size;

                List<CartItemViewModel> listItem = new List<CartItemViewModel>();
                listItem.Add(item);
                Session[SESSIONCART] = listItem;
            }

            //return RedirectToAction("Index", "Collection");
            return PartialView("_Cart");
        }

        [HttpPost]
        [AllowAnonymous]
        public void AddItemToCartItem(int productId, string size, int quantity)
        {
            var cart = Session[SESSIONCART];
            if (cart != null)
            {
                var list = (List<CartItemViewModel>)cart;
                if (list.Exists(x => x.Product.ProductId == productId && x.Size == size))
                {
                    var update = list.Find(x => x.Product.ProductId == productId && x.Size == size);
                    if (update.Quantity + quantity >= 0)
                        update.Quantity += quantity;
                    else
                    {
                        update.Quantity = 0;
                    }
                }
                Session[SESSIONCART] = list;
            }

        }


        [HttpPost]
        [AllowAnonymous]

        public JsonResult RemoveItemFromCart(int productId, string size)
        {
            var cart = Session[SESSIONCART];
            var list = new List<CartItemViewModel>();

            int index_item = -1;
            decimal giatong = 0;


            if (cart != null)
            {
                list = (List<CartItemViewModel>)cart;
                if (list.Exists(x => x.Product.ProductId == productId && x.Size == size))
                {
                    var update = list.Find(x => x.Product.ProductId == productId && x.Size == size);
                    index_item = list.IndexOf(update);

                    if (update != null)
                    {
                        list.Remove(update);
                    }
                }

                Session[SESSIONCART] = list;
            }
            foreach (var i in list)
            {
                giatong += i.Product.Price * i.Quantity;
            }

            return Json(new
            {
                danhsach = index_item,
                giatong = giatong
            });
        }

        private decimal GetTotalMoneyFromCart()
        {
            List<CartItemViewModel> listItemPay = (List<CartItemViewModel>)Session["cart"];
            decimal totalMoney = 0;
            foreach (var item in listItemPay)
            {
                totalMoney += item.Product.Price * item.Quantity;
            }
            return totalMoney;
        }

        [HttpPost]
        public ActionResult PaymentLaterAction()
        {
            if (User.Identity.IsAuthenticated)
            {
                Order newOrder = new Order();
                PaymentViewModel payment = (PaymentViewModel)Session["payment"];
                List<CartItemViewModel> listItemPay = (List<CartItemViewModel>)Session["cart"];

                if (payment != null)
                {
                    newOrder.NumberPhoneRev = payment.Phone;
                    newOrder.EmailRev = payment.Email;
                    newOrder.NameOfReceiver = payment.Fullname;
                    newOrder.AddressReceive = payment.Apartment + " - " + payment.Address + " - " + payment.City + " - " + payment.Country;
                    newOrder.StatusId = 1;
                    newOrder.IsPaid = false;
                    newOrder.ZipCode = payment.ZipCode;
                    newOrder.CustomerId = User.Identity.Name;
                    newOrder.TotalMoney = 0;
                    newOrder.CreateDate = DateTime.Now;
                    db.Orders.Add(newOrder);
                    db.SaveChanges();

                    foreach (var item in listItemPay)
                    {
                        OrderDetail orderDetail = new OrderDetail();
                        orderDetail.OrderId = newOrder.OrderId;
                        orderDetail.ProductId = item.Product.ProductId;
                        orderDetail.Size = item.Size;
                        orderDetail.Quantity = item.Quantity;
                        orderDetail.Price = item.Product.Price * item.Quantity;
                        newOrder.TotalMoney += orderDetail.Price;

                        db.OrderDetails.Add(orderDetail);
                        db.SaveChanges();


                        ProductSize bangSize = db.ProductSizes.Where(s => s.ProductId == orderDetail.ProductId && s.Size == orderDetail.Size).FirstOrDefault();
                        Product product = db.Products.Find(orderDetail.ProductId);
                        if (product != null)
                        {
                            bangSize.Quantity -= orderDetail.Quantity;
                            product.Quantity -= orderDetail.Quantity;
                        }
                          

                        db.SaveChanges();
                    }

                    Session["cart"] = null;
                    string status = "completed";
                    return RedirectToAction("PaymentResult", new { status = status });

                }
                else
                {
                    string status = "failed";
                    return RedirectToAction("PaymentResult", new { status = status });
                }
            }
            else
            {
                return RedirectToAction("SignInUp", "User");
            }


        }

        //[HttpPost]
        //public ActionResult PaymentOnline()
        //{
        //    if (User.Identity.IsAuthenticated)
        //    {
        //        string url = ConfigurationManager.AppSettings["Url"];
        //        string returnUrl = ConfigurationManager.AppSettings["ReturnUrl"];
        //        string tmnCode = ConfigurationManager.AppSettings["TmnCode"];
        //        string hashSecret = ConfigurationManager.AppSettings["HashSecret"];

        //        VnPayLibrary pay = new VnPayLibrary();
        //        string amount = Convert.ToInt64(GetTotalMoneyFromCart() * 100 * 23600) + "";

        //        pay.AddRequestData("vnp_Version", "2.1.0"); //Phiên bản api mà merchant kết nối. Phiên bản hiện tại là 2.1.0
        //        pay.AddRequestData("vnp_Command", "pay"); //Mã API sử dụng, mã cho giao dịch thanh toán là 'pay'
        //        pay.AddRequestData("vnp_TmnCode", tmnCode); //Mã website của merchant trên hệ thống của VNPAY (khi đăng ký tài khoản sẽ có trong mail VNPAY gửi về)
        //        pay.AddRequestData("vnp_Amount", amount); //số tiền cần thanh toán, công thức: số tiền * 100 - ví dụ 10.000 (mười nghìn đồng) --> 1000000
        //        pay.AddRequestData("vnp_BankCode", ""); //Mã Ngân hàng thanh toán (tham khảo: https://sandbox.vnpayment.vn/apis/danh-sach-ngan-hang/), có thể để trống, người dùng có thể chọn trên cổng thanh toán VNPAY
        //        pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")); //ngày thanh toán theo định dạng yyyyMMddHHmmss
        //        pay.AddRequestData("vnp_CurrCode", "VND"); //Đơn vị tiền tệ sử dụng thanh toán. Hiện tại chỉ hỗ trợ VND
        //        pay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress()); //Địa chỉ IP của khách hàng thực hiện giao dịch
        //        pay.AddRequestData("vnp_Locale", "en"); //Ngôn ngữ giao diện hiển thị - Tiếng Việt (vn), Tiếng Anh (en)
        //        pay.AddRequestData("vnp_OrderInfo", User.Identity.Name); //Thông tin mô tả nội dung thanh toán
        //        pay.AddRequestData("vnp_OrderType", "other"); //topup: Nạp tiền điện thoại - billpayment: Thanh toán hóa đơn - fashion: Thời trang - other: Thanh toán trực tuyến
        //        pay.AddRequestData("vnp_ReturnUrl", returnUrl); //URL thông báo kết quả giao dịch khi Khách hàng kết thúc thanh toán
        //        pay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString()); //mã hóa đơn

        //        string paymentUrl = pay.CreateRequestUrl(url, hashSecret);

        //        return Redirect(paymentUrl);
        //    }
        //    return View("Error");
        //}

        [HttpGet]
        public ActionResult PaymentConfirm()
        {
            string status = "failed";
            if (Request.QueryString.Count > 0)
            {
                string hashSecret = ConfigurationManager.AppSettings["HashSecret"]; //Chuỗi bí mật
                var vnpayData = Request.QueryString;
                VnPayLibrary pay = new VnPayLibrary();

                //lấy toàn bộ dữ liệu được trả về
                foreach (string s in vnpayData)
                {
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        pay.AddResponseData(s, vnpayData[s]);
                    }
                }

                long orderId = Convert.ToInt64(pay.GetResponseData("vnp_TxnRef")); //mã hóa đơn
                long vnpayTranId = Convert.ToInt64(pay.GetResponseData("vnp_TransactionNo")); //mã giao dịch tại hệ thống VNPAY
                string OrganizationID = Request.QueryString["vnp_OrderInfo"];
                string vnp_ResponseCode = pay.GetResponseData("vnp_ResponseCode"); //response code: 00 - thành công, khác 00 - xem thêm https://sandbox.vnpayment.vn/apis/docs/bang-ma-loi/
                string vnp_SecureHash = Request.QueryString["vnp_SecureHash"]; //hash của dữ liệu trả về
                string vnp_BankCode = Request.QueryString["vnp_BankCode"];
                decimal vnp_Amount = decimal.Parse(Request.QueryString["vnp_Amount"]) / 100;
                DateTime vnp_PayDate = DateTime.ParseExact(Request.QueryString["vnp_PayDate"], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);

                bool checkSignature = pay.ValidateSignature(vnp_SecureHash, hashSecret); //check chữ ký đúng hay không?

                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00")
                    {
                        //Thanh toán thành công
                        status = "completed";

                        Order newOrder = new Order();
                        PaymentViewModel payment = (PaymentViewModel)Session["payment"];
                        List<CartItemViewModel> listItemPay = (List<CartItemViewModel>)Session["cart"];

                        if (payment != null)
                        {
                            newOrder.NumberPhoneRev = payment.Phone;
                            newOrder.EmailRev = payment.Email;
                            newOrder.NameOfReceiver = payment.Fullname;
                            newOrder.AddressReceive = payment.Apartment + " - " + payment.Address + " - " + payment.City + " - " + payment.Country;
                            newOrder.StatusId = 1;
                            newOrder.IsPaid = true;
                            newOrder.ZipCode = payment.ZipCode;
                            newOrder.CustomerId = User.Identity.Name;
                            newOrder.TotalMoney = 0;
                            newOrder.CreateDate = DateTime.Now;
                            db.Orders.Add(newOrder);
                            db.SaveChanges();

                            foreach (var item in listItemPay)
                            {
                                OrderDetail orderDetail = new OrderDetail();
                                orderDetail.OrderId = newOrder.OrderId;
                                orderDetail.ProductId = item.Product.ProductId;
                                orderDetail.Size = item.Size;
                                orderDetail.Quantity = item.Quantity;
                                orderDetail.Price = item.Product.Price * item.Quantity;
                                newOrder.TotalMoney += orderDetail.Price;

                                db.OrderDetails.Add(orderDetail);
                                db.SaveChanges();


                                ProductSize bangSize = db.ProductSizes.Where(s => s.ProductId == orderDetail.ProductId && s.Size == orderDetail.Size).FirstOrDefault();
                                Product product = db.Products.Find(orderDetail.ProductId);
                                if (product != null)
                                {
                                    bangSize.Quantity -= orderDetail.Quantity;
                                    product.Quantity -= orderDetail.Quantity;
                                }
                                db.SaveChanges();
                            }
                            var customer = db.Customers.Find(User.Identity.Name);
                            if (customer != null)
                                customer.TotalSpent += newOrder.TotalMoney;
                            db.SaveChanges();

                            Session["cart"] = null;
                        }
                    }
               
                    else
                    {
                        //Thanh toán không thành công. Mã lỗi: vnp_ResponseCode
                        status = "failed";
                    }
                }
                else
                {
                    status = "failed";
                   
                }
            }
            return RedirectToAction("PaymentResult", new {status = status });
        }

        public ActionResult PaymentResult(string status)
        {
            ViewBag.status = status;
            return View();
        }


        public ActionResult PaymentOnline(string Cancel = null)
        {
            //getting the apiContext  
            string status = "completed";
            APIContext apiContext = PaypalConfiguration.GetAPIContext();
            try
            {
                //A resource representing a Payer that funds a payment Payment Method as paypal  
                //Payer Id will be returned when payment proceeds or click to pay  
                string payerId = Request.Params["PayerID"];
                if (string.IsNullOrEmpty(payerId))
                {
                    //this section will be executed first because PayerID doesn't exist  
                    //it is returned by the create function call of the payment class  
                    // Creating a payment  
                    // baseURL is the url on which paypal sendsback the data.  
                    string baseURI = Request.Url.Scheme + "://" + Request.Url.Authority + "/Purchasing/PaymentOnline?";
                    //here we are generating guid for storing the paymentID received in session  
                    //which will be used in the payment execution  
                    var guid = Convert.ToString((new Random()).Next(100000));
                    //CreatePayment function gives us the payment approval url  
                    //on which payer is redirected for paypal account payment  
                    var createdPayment = this.CreatePayment(apiContext, baseURI + "guid=" + guid);
                    //get links returned from paypal in response to Create function call  
                    var links = createdPayment.links.GetEnumerator();
                    string paypalRedirectUrl = null;
                    while (links.MoveNext())
                    {
                        Links lnk = links.Current;
                        if (lnk.rel.ToLower().Trim().Equals("approval_url"))
                        {
                            //saving the payapalredirect URL to which user will be redirected for payment  
                            paypalRedirectUrl = lnk.href;
                        }
                    }
                    // saving the paymentID in the key guid  
                    Session.Add(guid, createdPayment.id);
                    return Redirect(paypalRedirectUrl);
                }
                else
                {
                    // This function exectues after receving all parameters for the payment  
                    var guid = Request.Params["guid"];
                    var executedPayment = ExecutePayment(apiContext, payerId, Session[guid] as string);
                    //If executed payment failed then we will show payment failure message to user  
                    if (executedPayment.state.ToLower() != "approved")
                    {
                        status = "failed";
                    }
                }
            }
            catch (Exception ex)
            {
                status = "failed";
            }
            if(!status.Equals("failed")) {
                status = "completed";
                Order newOrder = new Order();
                PaymentViewModel payment = (PaymentViewModel)Session["payment"];
                List<CartItemViewModel> listItemPay = (List<CartItemViewModel>)Session["cart"];

                if (payment != null)
                {

                    newOrder.NumberPhoneRev = payment.Phone;
                    newOrder.EmailRev = payment.Email;
                    newOrder.NameOfReceiver = payment.Fullname;
                    newOrder.AddressReceive = payment.Apartment + " - " + payment.Address + " - " + payment.City + " - " + payment.Country;
                    newOrder.StatusId = 1;
                    newOrder.IsPaid = true;
                    newOrder.ZipCode = payment.ZipCode;
                    newOrder.CustomerId = User.Identity.Name;
                    newOrder.TotalMoney = 0;
                    newOrder.CreateDate = DateTime.Now;
                    db.Orders.Add(newOrder);
                    db.SaveChanges();

                    foreach (var item in listItemPay)
                    {
                        OrderDetail orderDetail = new OrderDetail();
                        orderDetail.OrderId = newOrder.OrderId;
                        orderDetail.ProductId = item.Product.ProductId;
                        orderDetail.Size = item.Size;
                        orderDetail.Quantity = item.Quantity;
                        orderDetail.Price = item.Product.Price * item.Quantity;
                        newOrder.TotalMoney += orderDetail.Price;

                        db.OrderDetails.Add(orderDetail);
                        db.SaveChanges();


                        ProductSize bangSize = db.ProductSizes.Where(s => s.ProductId == orderDetail.ProductId && s.Size == orderDetail.Size).FirstOrDefault();
                        Product product = db.Products.Find(orderDetail.ProductId);
                        if (product != null)
                        {
                            bangSize.Quantity -= orderDetail.Quantity;
                            product.Quantity -= orderDetail.Quantity;
                        }
                        db.SaveChanges();
                    }
                    var customer = db.Customers.Find(User.Identity.Name);
                    if (customer != null)
                        customer.TotalSpent += newOrder.TotalMoney;
                    db.SaveChanges();

                    Session["cart"] = null;
                }
            }

            return RedirectToAction("PaymentResult", new { status = status });
        }
        private PayPal.Api.Payment payment;
        private Payment ExecutePayment(APIContext apiContext, string payerId, string paymentId)
        {
            var paymentExecution = new PaymentExecution()
            {
                payer_id = payerId
            };
            this.payment = new Payment()
            {
                id = paymentId
            };
            return this.payment.Execute(apiContext, paymentExecution);
        }
        private Payment CreatePayment(APIContext apiContext, string redirectUrl)
        {
            //create itemlist and add item objects to it  
            var itemList = new ItemList()
            {
                items = new List<Item>()
            };
            //Adding Item Details like name, currency, price etc  
            itemList.items.Add(new Item()
            {
                name = "Item Name comes here",
                currency = "USD",
                price = "1",
                quantity = "1",
                sku = "sku"
            });
            var payer = new Payer()
            {
                payment_method = "paypal"
            };
            // Configure Redirect Urls here with RedirectUrls object  
            var redirUrls = new RedirectUrls()
            {
                cancel_url = redirectUrl + "&Cancel=true",
                return_url = redirectUrl
            };
            // Adding Tax, shipping and Subtotal details  
            var details = new Details()
            {
                tax = "1",
                shipping = "1",
                subtotal = "1"
            };
            //Final amount with details  
            var amount = new Amount()
            {
                currency = "USD",
                total = "3", // Total must be equal to sum of tax, shipping and subtotal.  
                details = details
            };
            var transactionList = new List<Transaction>();
            // Adding description about the transaction  
            transactionList.Add(new Transaction()
            {
                description = "Transaction description",
                invoice_number = Guid.NewGuid().ToString(), //Generate an Invoice No  
                amount = amount,
                item_list = itemList
            });
            this.payment = new Payment()
            {
                intent = "sale",
                payer = payer,
                transactions = transactionList,
                redirect_urls = redirUrls
            };
            // Create a payment using a APIContext  
            return this.payment.Create(apiContext);
        }
        //private Payment CreatePayment(APIContext apiContext, string redirectUrl)
        //{
        //    decimal totalMoney = 0;
        //    var itemList = new ItemList()
        //    {
        //        items = new List<Item>()
        //    };
        //    //Adding Item Details like name, currency, price etc  
        //    List<CartItemViewModel> listItemPay = (List<CartItemViewModel>)Session["cart"];
        //    foreach (var item in listItemPay)
        //    {
        //        itemList.items.Add(new Item()
        //        {
        //            name = item.Product.Name,
        //            currency = "USD",
        //            price = item.Product.Price+"",
        //            quantity = item.Quantity+"",
        //            sku = "sku"
        //        });
        //        totalMoney += item.Product.Price * item.Quantity;
        //    }

        //    var payer = new Payer()
        //    {
        //        payment_method = "paypal"
        //    };
        //    // Configure Redirect Urls here with RedirectUrls object  
        //    var redirUrls = new RedirectUrls()
        //    {
        //        cancel_url = redirectUrl + "&Cancel=true",
        //        return_url = redirectUrl
        //    };
        //    // Adding Tax, shipping and Subtotal details  
        //    var details = new Details()
        //    {
        //        tax = "0",
        //        shipping = "0",
        //        subtotal = totalMoney+""
        //    };
        //    //Final amount with details  
        //    var amount = new Amount()
        //    {
        //        currency = "USD",
        //        total = totalMoney+"", // Total must be equal to sum of tax, shipping and subtotal.  
        //        details = details
        //    };
        //    var transactionList = new List<Transaction>();
        //    // Adding description about the transaction  
        //    transactionList.Add(new Transaction()
        //    {
        //        description = "Transaction description",
        //        invoice_number = "your generated invoice number", //Generate an Invoice No  
        //        amount = amount,
        //        item_list = itemList
        //    });
        //    this.payment = new Payment()
        //    {
        //        intent = "sale",
        //        payer = payer,
        //        transactions = transactionList,
        //        redirect_urls = redirUrls
        //    };
        //    // Create a payment using a APIContext  
        //    return this.payment.Create(apiContext);
        //}
    }
}