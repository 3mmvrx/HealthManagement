using System.Text;

namespace HealthCareManagementSystem.Services
{
    public record ImageSaveResult(bool Succeeded, string? FileName, string? Error)
    {
        public static ImageSaveResult Ok(string fileName) => new(true, fileName, null);

        public static ImageSaveResult Fail(string error) => new(false, null, error);
    }

    /// <summary>
    /// Stores and removes user-uploaded pictures under wwwroot/images/{folder}.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Validates and saves an uploaded image. The returned file name is a random GUID,
        /// never the name the client sent.
        /// </summary>
        Task<ImageSaveResult> SaveAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

        /// <summary>Deletes a previously saved image. Missing files are ignored.</summary>
        void Delete(string? fileName, string folder);
    }

    public class ImageService : IImageService
    {
        public const long MaxBytes = 2 * 1024 * 1024;

        private static readonly string[] AllowedFolders = { "Clinics", "Doctors", "Profiles" };
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Gif87Signature = Encoding.ASCII.GetBytes("GIF87a");
        private static readonly byte[] Gif89Signature = Encoding.ASCII.GetBytes("GIF89a");
        private static readonly byte[] RiffSignature = Encoding.ASCII.GetBytes("RIFF");
        private static readonly byte[] WebpSignature = Encoding.ASCII.GetBytes("WEBP");

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageService> _logger;

        public ImageService(IWebHostEnvironment environment, ILogger<ImageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<ImageSaveResult> SaveAsync(
            IFormFile file, string folder, CancellationToken cancellationToken = default)
        {
            if (!AllowedFolders.Contains(folder))
            {
                throw new ArgumentException("Unknown image folder.", nameof(folder));
            }

            if (file == null || file.Length == 0)
            {
                return ImageSaveResult.Fail("Please choose an image file.");
            }

            if (file.Length > MaxBytes)
            {
                return ImageSaveResult.Fail("The image must be 2 MB or smaller.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return ImageSaveResult.Fail("Only JPG, PNG, GIF or WEBP images are allowed.");
            }

            await using (var probe = file.OpenReadStream())
            {
                if (!await LooksLikeImageAsync(probe, extension, cancellationToken))
                {
                    return ImageSaveResult.Fail("The selected file is not a valid image.");
                }
            }

            var directory = Path.Combine(_environment.WebRootPath, "images", folder);
            Directory.CreateDirectory(directory);

            var fileName = Guid.NewGuid().ToString("N") + extension;
            var path = Path.Combine(directory, fileName);

            await using (var target = new FileStream(path, FileMode.CreateNew))
            {
                await file.CopyToAsync(target, cancellationToken);
            }

            return ImageSaveResult.Ok(fileName);
        }

        public void Delete(string? fileName, string folder)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !AllowedFolders.Contains(folder))
            {
                return;
            }

            // GetFileName strips any directory part, so a tampered value cannot escape the folder.
            var safeName = Path.GetFileName(fileName);
            var path = Path.Combine(_environment.WebRootPath, "images", folder, safeName);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete image {Path}.", path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Could not delete image {Path}.", path);
            }
        }

        private static async Task<bool> LooksLikeImageAsync(
            Stream stream, string extension, CancellationToken cancellationToken)
        {
            var header = new byte[12];
            var read = await stream.ReadAtLeastAsync(header, header.Length, false, cancellationToken);

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return Matches(header, read, 0, JpegSignature);

                case ".png":
                    return Matches(header, read, 0, PngSignature);

                case ".gif":
                    return Matches(header, read, 0, Gif87Signature)
                        || Matches(header, read, 0, Gif89Signature);

                case ".webp":
                    return Matches(header, read, 0, RiffSignature)
                        && Matches(header, read, 8, WebpSignature);

                default:
                    return false;
            }
        }

        private static bool Matches(byte[] data, int length, int offset, byte[] signature)
        {
            if (length < offset + signature.Length)
            {
                return false;
            }

            for (var i = 0; i < signature.Length; i++)
            {
                if (data[offset + i] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
