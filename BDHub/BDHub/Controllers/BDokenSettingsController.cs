﻿using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Windows.Forms;
using BDHub.Models;

namespace BDHub.Controllers
{
    public class BDokenSettingsController : Controller
    {
        private BDEntities db = new BDEntities();
        private BDokenControl BDC = new BDokenControl();
        private static decimal userBalance = -1;
        private static int preventResend = 0;

        public async Task<ActionResult> Index(int mssg = 0)
        {
            try
            {
                int sid = (int)Session["userID"];
                switch (mssg)
                {
                    case 1:
                        ViewBag.Message = "Account loaded successfully.";
                        break;
                    case 2:
                        ViewBag.Message = "Error occured during account save.";
                        break;
                    case 3:
                        ViewBag.Message = "Account created successfully.";
                        break;
                    case 4:
                        ViewBag.Message = "Passphrase required.";
                        break;
                    case 5:
                        ViewBag.Message = "Passphrase incorrect.";
                        break;
                    case 6:
                        ViewBag.Message = "Amount set to 0, transaction cancelled.";
                        break;
                    case 7:
                        ViewBag.Message = "Incorrect input, number is required.";
                        break;
                    case 0:
                    default:
                        ViewBag.Message = "";
                        break;
                }
                CertUser result = (from r in db.CertUsers
                                   where r.certUserID == sid
                                   select r).SingleOrDefault();
                await CheckBDokenBalance();
                result.balance = userBalance;
                BigInteger sellPrice = await BDC.GetSellPrice(result.beternumAddress);
                BigInteger buyPrice = await BDC.GetBuyPrice(result.beternumAddress);
                result.sellPrice = 1 / (decimal)sellPrice;
                result.buyPrice = (decimal)buyPrice / 1000000000000000000;
                return View(result);
            }
            catch
            {
                return Redirect("~/Login/Index");
            }
        }

        public async Task<ActionResult> LoadBDokenAccount()
        {
            int id = (int)Session["userID"];

            CertUser addingNewAddress = (from n in db.CertUsers
                                         where n.certUserID == id
                                         select n).SingleOrDefault();

            string filepath = GetFilePath();

            if (filepath == "")
            {
                return RedirectToAction("Index");
            }

            addingNewAddress.beternumAddress = BDC.LoadFromKeystore(filepath);
            try
            {
                db.SaveChanges();
                await CheckBDokenBalance();
            }
            catch
            {
                return RedirectToAction("Index", new { mssg = 2 });
            }

            return RedirectToAction("Index", new { mssg = 1 });
        }

        public ActionResult CreateBDokenAcc(int bdokenAccountFailed = 0)
        {
            switch (bdokenAccountFailed)
            {
                case 2:
                    ViewBag.Message = "Passwords to not match.";
                    break;
                case 3:
                    ViewBag.Message = "BDoken account creation failed.";
                    break;
                case 0:
                case 1:
                default:
                    ViewBag.Message = "";
                    break;
            }
            return View();
        }
        [HttpPost]
        public async Task<ActionResult> CreateBDokenAcc(System.Web.Mvc.FormCollection collection, int bdokenAccountFailed = 0)
        {
            try
            {
                int id = (int)Session["userID"];
                CertUser addingNewAddress = (from n in db.CertUsers
                                             where n.certUserID == id
                                             select n).SingleOrDefault();
                string path = "";

                try
                {
                    string password = collection[2];
                    string retyped = collection[3];
                    if (password == retyped)
                    {
                        path = GetDirPath();

                        if (path == "")
                            return RedirectToAction("Index");

                        addingNewAddress.beternumAddress = BDC.CreateNew(path, password);
                        try
                        {
                            db.SaveChanges();
                            await CheckBDokenBalance();
                        }
                        catch
                        {
                            return RedirectToAction("Index", new { mssg = 2 });
                        }
                        return RedirectToAction("Index", new { mssg = 3 });
                    }
                    else
                    {
                        return RedirectToAction("CreateBDokenAcc", new { bdokenAccountFailed = 2 });
                    }
                }
                catch
                {
                    return RedirectToAction("CreateBDokenAcc", new { bdokenAccountFailed = 3 });
                }
            }
            catch
            {
                return Redirect("~/Login/Index");
            }
        }

        public ActionResult BDokenDetails()
        {
            BDokenDetail bdDetails = (from b in db.BDokenDetails
                                      select b).SingleOrDefault();
            return View(bdDetails);
        }

        [HttpPost]
        public async Task<ActionResult> BuySell(System.Web.Mvc.FormCollection collection)
        {
            if (preventResend != 1)
            {
                try
                {
                    int sid = (int)Session["userID"];
                    CertUser result = (from r in db.CertUsers
                                       where r.certUserID == sid
                                       select r).SingleOrDefault();
                    result.sellAmount = 0;
                    result.buyAmount = 0;
                    result.bdokenPass = "";


                    string clickedButton = collection["hiddenClickedButton"];
                    result.bdokenPass = collection["passphrase"];
                    if (result.bdokenPass == "")
                        return RedirectToAction("Index", new { mssg = 4 });
                    if (!Decimal.TryParse(collection["hiddenAmount"], out decimal amount))
                    {
                        return RedirectToAction("Index", new { mssg = 7 });
                    }

                    if (clickedButton.Equals("Buy"))
                    {
                        result.buyAmount = amount;
                        if (result.buyAmount != 0)
                            try
                            {
                                await BDC.Buy(result.beternumAddress, result.bdokenPass, (BigInteger)result.buyAmount * 1000000000000000000);

                            }
                            catch
                            {
                                return RedirectToAction("Index", new { mssg = 5 });
                            }
                        else
                            return RedirectToAction("Index", new { mssg = 6 });
                    }
                    else if (clickedButton.Equals("Sell"))
                    {
                        result.sellAmount = amount;
                        if (result.sellAmount != 0)
                            try
                            {
                                await BDC.Sell(result.beternumAddress, result.bdokenPass, (BigInteger)(result.sellAmount * 1000000000000000000));
                            }
                            catch
                            {
                                return RedirectToAction("Index", new { mssg = 5 });
                            }
                        else
                            return RedirectToAction("Index", new { mssg = 6 });
                    }
                    else if (clickedButton.Equals("Donate"))
                    {
                        result.donateAmount = amount;
                        if (result.donateAmount != 0)
                            try
                            {
                                await BDC.BloodForTheBloodGod(result.beternumAddress, result.bdokenPass, (BigInteger)(result.donateAmount * 1000000000000000000));
                            }
                            catch
                            {
                                return RedirectToAction("Index", new { mssg = 5 });
                            }
                        else
                            return RedirectToAction("Index", new { mssg = 6 });
                    }

                    preventResend = 1;
                    result.sellAmount = 0;
                    result.buyAmount = 0;
                    BigInteger sellPrice = await BDC.GetSellPrice(result.beternumAddress);
                    BigInteger buyPrice = await BDC.GetBuyPrice(result.beternumAddress);
                    result.sellPrice = 1 / (decimal)sellPrice;
                    result.buyPrice = (decimal)buyPrice / 1000000000000000000;
                    await CheckBDokenBalance();
                    return View("Index", result);
                }
                catch
                {
                    return Redirect("~/Login/Index");
                }
            }
            else
            {
                int sid = (int)Session["userID"];
                CertUser result = (from r in db.CertUsers
                                   where r.certUserID == sid
                                   select r).SingleOrDefault();
                result.bdokenPass = "";
                result.sellAmount = 0;
                result.buyAmount = 0;
                preventResend = 0;
                BigInteger sellPrice = await BDC.GetSellPrice(result.beternumAddress);
                BigInteger buyPrice = await BDC.GetBuyPrice(result.beternumAddress);
                result.sellPrice = 1 / (decimal)sellPrice;
                result.buyPrice = (decimal)buyPrice / 1000000000000000000;
                await CheckBDokenBalance();
                return View("Index", result);
            }
        }

        public async Task<ActionResult> CheckBDokenBalance()
        {
            try
            {
                int sid = (int)Session["userID"];
                CertUser result = (from r in db.CertUsers
                                   where r.certUserID == sid
                                   select r).SingleOrDefault();
                try
                {
                    BigInteger BigBalance = await BDC.CheckBalance(result.beternumAddress, "");
                    userBalance = (decimal)BigBalance / 1000000000000000000;
                    result.balance = userBalance;
                }
                catch
                {
                    userBalance = -1;
                    return RedirectToAction("Index", new { mssg = 5 });
                }

                return View("Index", result);
            }
            catch
            {
                return Redirect("~/Login/Index");
            }
        }

        public string GetDirPath()
        {
            string selectedPath = "";
            var t = new Thread(() =>
            {

                FolderBrowserDialog fbd = new FolderBrowserDialog
                {
                    ShowNewFolderButton = false,
                    RootFolder = System.Environment.SpecialFolder.MyComputer
                };



                DialogResult result = fbd.ShowDialog();
                if (result == DialogResult.OK)
                {
                    // the code here will be executed if the user presses Open in
                    // the dialog.
                }
                selectedPath = fbd.SelectedPath;


            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            return selectedPath;

        }

        public string GetFilePath()
        {
            string filePath = "";
            string[] arrAllFiles;
            var t = new Thread(() =>
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = "All Files (*.*)|*.*",
                    FilterIndex = 1,
                    Multiselect = false
                };

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    filePath = ofd.FileName;
                    arrAllFiles = ofd.FileNames; //used when Multiselect = true           
                }

            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            return filePath;

        }
    }
}