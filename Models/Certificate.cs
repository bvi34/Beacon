using System.ComponentModel.DataAnnotations;

namespace Beacon.Models
{
    public class Certificate
    {
        public int Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Required]
        [StringLength(255)]
        public string CommonName { get; set; } = string.Empty;

        [StringLength(40)] // SHA-1 thumbprint length
        public string Thumbprint { get; set; } = string.Empty;

        [Required]
        public DateTime IssuedDate { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [StringLength(255)]
        public string Issuer { get; set; } = string.Empty;

        [StringLength(100)]
        public string Algorithm { get; set; } = string.Empty;

        public int KeySize { get; set; }

        public CertificateStatus Status { get; set; } = CertificateStatus.Unknown;

        public DateTime LastChecked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Device Device { get; set; } = null!;

        // Computed properties
        public bool IsExpired => DateTime.UtcNow > ExpiryDate;
        public bool IsExpiringSoon => DateTime.UtcNow.AddDays(30) > ExpiryDate && !IsExpired;
        public int DaysUntilExpiry => (int)(ExpiryDate - DateTime.UtcNow).TotalDays;
    }

    public enum CertificateStatus
    {
        Unknown,
        Valid,
        Expired,
        ExpiringSoon,
        Invalid,
        Revoked
    }
}