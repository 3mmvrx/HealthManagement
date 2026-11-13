using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoctorProfile = HealthCareManagementSystem.Models.Doctor;

namespace HealthCareManagementSystem.Areas.Admin.Controllers
{
    [Area(CD.ADMIN_AREA)]
    [Authorize(Roles = CD.STAFF_ROLES)]
    public class ClinicController : Controller
    {
        private const string ImageFolder = "Clinics";

        private readonly IRepository<Clinic> _clinicRepository;
        private readonly IRepository<DoctorProfile> _doctorRepository;
        private readonly IImageService _imageService;

        public ClinicController(
            IRepository<Clinic> clinicRepository,
            IRepository<DoctorProfile> doctorRepository,
            IImageService imageService)
        {
            _clinicRepository = clinicRepository;
            _doctorRepository = doctorRepository;
            _imageService = imageService;
        }

        #region Index

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var clinics = await _clinicRepository.GetAsync(
                tracked: false,
                orderBy: query => query.OrderBy(c => c.Name));

            return View(clinics);
        }

        #endregion

        #region Create

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ClinicVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ClinicVM clinicVM)
        {
            if (!ModelState.IsValid)
            {
                return View(clinicVM);
            }

            var name = clinicVM.Name.Trim();

            if (await _clinicRepository.AnyAsync(c => c.Name == name))
            {
                ModelState.AddModelError(nameof(ClinicVM.Name), "A clinic with this name already exists.");
                return View(clinicVM);
            }

            string? imageName = null;

            if (clinicVM.Image is { Length: > 0 })
            {
                var saved = await _imageService.SaveAsync(clinicVM.Image, ImageFolder);

                if (!saved.Succeeded)
                {
                    ModelState.AddModelError(nameof(ClinicVM.Image), saved.Error ?? "The image could not be saved.");
                    return View(clinicVM);
                }

                imageName = saved.FileName;
            }

            var clinic = new Clinic
            {
                Name = name,
                Address = clinicVM.Address.Trim(),
                PhoneNumber = clinicVM.PhoneNumber.Trim(),
                Description = clinicVM.Description?.Trim(),
                ImageName = imageName
            };

            try
            {
                await _clinicRepository.AddAsync(clinic);
                await _clinicRepository.CommitAsync();
            }
            catch (DbUpdateException)
            {
                _imageService.Delete(imageName, ImageFolder);
                ModelState.AddModelError(string.Empty, "The clinic could not be saved. Please try again.");
                return View(clinicVM);
            }

            TempData["Success-Notification"] = "Clinic created successfully.";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Update

        [HttpGet]
        public async Task<IActionResult> Update(int id)
        {
            var clinic = await _clinicRepository.GetOneAsync(c => c.Id == id, tracked: false);

            if (clinic == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            var clinicVM = new ClinicVM
            {
                Name = clinic.Name,
                Address = clinic.Address,
                PhoneNumber = clinic.PhoneNumber,
                Description = clinic.Description,
                ImageName = clinic.ImageName
            };

            return View(clinicVM);
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, ClinicVM clinicVM)
        {
            var clinic = await _clinicRepository.GetOneAsync(c => c.Id == id);

            if (clinic == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            // Keeps the current picture visible if the form has to be shown again.
            clinicVM.ImageName = clinic.ImageName;

            if (!ModelState.IsValid)
            {
                return View(clinicVM);
            }

            var name = clinicVM.Name.Trim();

            if (await _clinicRepository.AnyAsync(c => c.Name == name && c.Id != id))
            {
                ModelState.AddModelError(nameof(ClinicVM.Name), "A clinic with this name already exists.");
                return View(clinicVM);
            }

            string? newImageName = null;

            if (clinicVM.Image is { Length: > 0 })
            {
                var saved = await _imageService.SaveAsync(clinicVM.Image, ImageFolder);

                if (!saved.Succeeded)
                {
                    ModelState.AddModelError(nameof(ClinicVM.Image), saved.Error ?? "The image could not be saved.");
                    return View(clinicVM);
                }

                newImageName = saved.FileName;
            }

            var oldImageName = clinic.ImageName;

            clinic.Name = name;
            clinic.Address = clinicVM.Address.Trim();
            clinic.PhoneNumber = clinicVM.PhoneNumber.Trim();
            clinic.Description = clinicVM.Description?.Trim();

            if (newImageName != null)
            {
                clinic.ImageName = newImageName;
            }

            try
            {
                _clinicRepository.Update(clinic);
                await _clinicRepository.CommitAsync();
            }
            catch (DbUpdateException)
            {
                _imageService.Delete(newImageName, ImageFolder);
                ModelState.AddModelError(string.Empty, "The clinic could not be updated. Please try again.");
                return View(clinicVM);
            }

            if (newImageName != null)
            {
                _imageService.Delete(oldImageName, ImageFolder);
            }

            TempData["Success-Notification"] = "Clinic updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Delete

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var clinic = await _clinicRepository.GetOneAsync(c => c.Id == id, tracked: false);

            if (clinic == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            return View(clinic);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var clinic = await _clinicRepository.GetOneAsync(c => c.Id == id);

            if (clinic == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            if (await _doctorRepository.AnyAsync(d => d.ClinicId == id))
            {
                TempData["Error-Notification"] = "This clinic still has doctors. Move or delete them first.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _clinicRepository.Delete(clinic);
                await _clinicRepository.CommitAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error-Notification"] = "This clinic is still used by other records, so it cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            _imageService.Delete(clinic.ImageName, ImageFolder);

            TempData["Success-Notification"] = "Clinic deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}
