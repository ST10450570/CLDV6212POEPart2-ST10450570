﻿using ABCRetails.Models;
using ABCRetails.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ABCRetails.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IAzureStorageService _storageService;

        public CustomerController(IAzureStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _storageService.GetAllEntitiesAsync<Customer>();
            return View(customers);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _storageService.AddEntityAsync(customer);
                    TempData["Success"] = "Customer created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
                }
            }
            return View(customer);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var customer = await _storageService.GetEntityAsync<Customer>("Customer", id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Customer customer)
        {
            if (id != customer.RowKey)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the existing customer to preserve system properties
                    var existingCustomer = await _storageService.GetEntityAsync<Customer>("Customer", id);
                    if (existingCustomer == null)
                    {
                        TempData["Error"] = "Customer not found. It may have been deleted.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Preserve the system properties that shouldn't be modified by the user
                    customer.PartitionKey = existingCustomer.PartitionKey;
                    customer.RowKey = existingCustomer.RowKey;
                    customer.Timestamp = existingCustomer.Timestamp;
                    customer.ETag = existingCustomer.ETag;

                    await _storageService.UpdateEntityAsync(customer);
                    TempData["Success"] = "Customer updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("modified by another user"))
                    {
                        TempData["Error"] = ex.Message;
                        return RedirectToAction(nameof(Edit), new { id });
                    }
                    ModelState.AddModelError("", $"Error updating customer: {ex.Message}");
                }
            }
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _storageService.DeleteEntityAsync<Customer>("Customer", id);
                TempData["Success"] = "Customer deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // Add this method to CustomerController
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            var customers = await _storageService.GetAllEntitiesAsync<Customer>();

            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(c =>
                    c.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    c.Surname.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    c.Username.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    c.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.SearchString = searchString;
            return View(customers);
        }
    }
}