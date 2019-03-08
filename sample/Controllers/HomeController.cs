using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AspNetCoreSessionState.Models;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreSessionState.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index()
        {
            await HttpContext.Session.LoadAsync();
            var data = HttpContext.Session.GetString("data");
            if(data == null)
            {
                HttpContext.Session.SetString("data", "someValue");
            }
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
