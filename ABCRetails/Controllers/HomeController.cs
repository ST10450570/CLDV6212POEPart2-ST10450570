using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using ABCRetails.Models;
using ABCRetails.Models.ViewModels;
using ABCRetails.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetails.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // Single constructor with all required dependencies for DI
        public HomeController(
            IAzureStorageService storageService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _storageService = storageService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            // Prepare data holders
            IEnumerable<Person>? people = Enumerable.Empty<Person>();
            IEnumerable<Product> products = Enumerable.Empty<Product>();
            IEnumerable<Customer> customers = Enumerable.Empty<Customer>();
            IEnumerable<Order> orders = Enumerable.Empty<Order>();

            // 1) Try to get people from function API (if configured)
            var httpClient = _httpClientFactory.CreateClient();
            var apiBaseUrl = _configuration["FunctionApi:BaseUrl"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                try
                {
                    // Ensure proper interpolation and a trailing slash if needed
                    var url = apiBaseUrl.EndsWith("/") ? $"{apiBaseUrl}people" : $"{apiBaseUrl}/people";
                    var httpResponseMessage = await httpClient.GetAsync(url);

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var deserialized = await JsonSerializer.DeserializeAsync<IEnumerable<Person>>(contentStream, options);
                        if (deserialized != null)
                        {
                            people = deserialized;
                        }
                    }
                    else
                    {
                        // Non-success status — show message but continue to load storage items
                        ViewBag.ApiStatus = $"API returned {(int)httpResponseMessage.StatusCode} {httpResponseMessage.ReasonPhrase}";
                    }
                }
                catch (HttpRequestException)
                {
                    ViewBag.ErrorMessage = "Could not connect to the API. Please ensure the Azure Function is running.";
                }
                catch (JsonException)
                {
                    ViewBag.ErrorMessage = "Failed to parse API response.";
                }
            }
            else
            {
                ViewBag.ErrorMessage = "API base URL not configured.";
            }

            // 2) Get storage entities (Azure Table / Cosmos / whichever implementation of IAzureStorageService)
            try
            {
                products = await _storageService.GetAllEntitiesAsync<Product>() ?? Enumerable.Empty<Product>();
                customers = await _storageService.GetAllEntitiesAsync<Customer>() ?? Enumerable.Empty<Customer>();
                orders = await _storageService.GetAllEntitiesAsync<Order>() ?? Enumerable.Empty<Order>();
            }
            catch (Exception ex)
            {
                // Log or surface a message; do not throw so UI can render whatever data was obtained.
                ViewBag.StorageError = $"Failed to load storage entities: {ex.Message}";
            }

            // 3) Build and return the ViewModel — include summary counts and put people in ViewBag if HomeViewModel doesn't have People
            var viewModel = new HomeViewModel
            {
                FeaturedProducts = products.Take(5).ToList(),
                ProductCount = products.Count(),
                CustomerCount = customers.Count(),
                OrderCount = orders.Count()
            };

            // If your view expects people, either add a People property to HomeViewModel
            // or use ViewBag / ViewData to pass the people list:
            ViewBag.People = people.ToList();

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult AddWithImage()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddWithImage(AddPersonWithImage model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var httpClient = _httpClientFactory.CreateClient();
            var apiBaseUrl = _configuration["FunctionApi:BaseUrl"] ?? string.Empty;
            var url = apiBaseUrl.EndsWith("/") ? $"{apiBaseUrl}people-with-image" : $"{apiBaseUrl}/people-with-image";

            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(model.Name ?? string.Empty), "Name");
            formData.Add(new StringContent(model.Email ?? string.Empty), "Email");

            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                var streamContent = new StreamContent(model.ProfileImage.OpenReadStream());
                // Set the content-type for the uploaded file
                if (!string.IsNullOrWhiteSpace(model.ProfileImage.ContentType))
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(model.ProfileImage.ContentType);
                }
                formData.Add(streamContent, "ProfileImage", model.ProfileImage.FileName);
            }

            var httpResponseMessage = await httpClient.PostAsync(url, formData);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Successfully added {model.Name} with an image";
                return RedirectToAction("Index");
            }

            ModelState.AddModelError(string.Empty, "An error occurred while calling the API.");
            return View(model);
        }

        public IActionResult Privacy() => View();

        public IActionResult Contact() => View();

        [HttpPost]
        public async Task<IActionResult> InitializeStorage()
        {
            try
            {
                // Calling and discarding result is fine if storage service handles initialization internally
                await _storageService.GetAllEntitiesAsync<Customer>();
                TempData["Success"] = "Azure Storage initialized successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to initialize storage: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
