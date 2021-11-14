using AzureBlobExplorer.Helper;
using AzureBlobExplorer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace AzureBlobExplorer.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public IConfiguration Configuration { get; }

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _logger = logger;
            Configuration = configuration;
        }

        public IActionResult Index()
        {
            try
            {
                SASHelper sASHelper = new SASHelper(Configuration);
                ViewBag.SASes = sASHelper.GetSASes4User(User);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occured in Index, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return StatusCode(500, ex.Message);
            }
        }

        public IActionResult ListContainers(string sASName)
        {
            try
            {
                SASHelper sASHelper = new SASHelper(Configuration);
                string sasUri = sASHelper.GetSASURL(sASName);

                if (string.IsNullOrWhiteSpace(sasUri)) return NoContent();

                SessionExtensions.SetString(HttpContext.Session, "SasUri", sasUri);
                SessionExtensions.SetInt32(HttpContext.Session, "Access", sASHelper.GetAccessType(sASName));

                return Json(ContainerHelper.ListContainers(sasUri));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occured in ListContainers, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return StatusCode(500, ex.Message);
            }
        }

        public async Task<IActionResult> ListBlobs(string prefix)
        {
            try
            {
                string sasUri = SessionExtensions.GetString(HttpContext.Session, "SasUri");
                if (string.IsNullOrWhiteSpace(sasUri)) return NoContent();

                int access = SessionExtensions.GetInt32(HttpContext.Session, "Access") ?? 0;
                if (access < 1) return Forbid();

                return Json(await ContainerHelper.ListBlobsAsync(prefix, sasUri));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occured in ListBlobs, " + (ex.InnerException == null ? ex.Message : ex.InnerException.Message));
                return StatusCode(500, ex.Message);
            }
        }

        public async Task<IActionResult> DownloadBlob(string blobUri)
        {
            try
            {
                if (string.IsNullOrEmpty(blobUri))
                    return BadRequest();

                Startup.DownProgress.Remove(blobUri);
                string sasUri = SessionExtensions.GetString(HttpContext.Session, "SasUri");
                if (string.IsNullOrWhiteSpace(sasUri)) return NoContent();

                int access = SessionExtensions.GetInt32(HttpContext.Session, "Access") ?? 0;
                if (access < 2) return Forbid();

                string fileName = blobUri.Substring(blobUri.LastIndexOf("/") + 1);

                string blobPath = await ContainerHelper.DownloadBlobAsync(sasUri, blobUri);

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(blobPath);

                return File(fileBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                Startup.DownProgress.Remove(blobUri);
                _logger.LogError(ex, $"An error occured in DownloadBlob, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                Response.StatusCode = 200;
                return File(new UTF8Encoding(true).GetBytes(ex.Message), "application/octet-stream", $"||Error|{ex.Message}|Error||");
            }
        }

        public async Task<IActionResult> DeleteBlob(string blobUri)
        {
            try
            {
                if (string.IsNullOrEmpty(blobUri))
                    return BadRequest();

                string sasUri = SessionExtensions.GetString(HttpContext.Session, "SasUri");
                if (string.IsNullOrWhiteSpace(sasUri)) return NoContent();

                int access = SessionExtensions.GetInt32(HttpContext.Session, "Access") ?? 0;
                if (access < 4) return Forbid();

                await ContainerHelper.DeleteBlobAsync(sasUri, blobUri);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occured in DeleteBlob, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return StatusCode(500, ex.Message);
            }
        }

        public async Task<IActionResult> ExistsBlob(string blobUri)
        {
            try
            {
                if (string.IsNullOrEmpty(blobUri))
                    return BadRequest();

                string sasUri = SessionExtensions.GetString(HttpContext.Session, "SasUri");
                if (string.IsNullOrWhiteSpace(sasUri)) return NoContent();

                int access = SessionExtensions.GetInt32(HttpContext.Session, "Access") ?? 0;
                if (access < 1) return Forbid();

                return Json(await ContainerHelper.ExistsBlobAsync(sasUri, blobUri));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occured in ExistsBlob, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return StatusCode(500, ex.Message);
            }
        }

        [DisableFormValueModelBinding]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadBlob(List<IFormFile> files)
        {
            try
            {
                string sasUri = SessionExtensions.GetString(HttpContext.Session, "SasUri");
                if (string.IsNullOrWhiteSpace(sasUri)) return NoContent();

                int access = SessionExtensions.GetInt32(HttpContext.Session, "Access") ?? 0;
                if (access < 1) return Forbid();


                foreach (IFormFile file in files)
                {
                    Startup.UpProgress.Remove(file.FileName);
                    await ContainerHelper.CreateBlobAsync(sasUri, file.FileName, file.OpenReadStream());
                }
                return Ok();
            }
            catch (Exception ex)
            {
                Startup.UpProgress.Remove(files[0].FileName);
                _logger.LogError(ex, $"An error occured in UploadBlob, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return StatusCode(500, ex.Message);
            }
        }

        public IActionResult Progress(bool isUp, string fileId)
        {
            try
            {
                if (isUp)
                {
                    if (Startup.UpProgress != null && Startup.UpProgress.TryGetValue(fileId, out int result))
                    {
                        if (result == 100) Startup.UpProgress.Remove(fileId);
                        return this.Content(result.ToString());
                    }
                }
                else
                {
                    if (Startup.DownProgress != null && Startup.DownProgress.TryGetValue(fileId, out int result))
                    {
                        if (result == 100) Startup.DownProgress.Remove(fileId);
                        return this.Content(result.ToString());
                    }
                }
                return this.Content("0");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occured in Progress, " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return StatusCode(500, ex.Message);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}