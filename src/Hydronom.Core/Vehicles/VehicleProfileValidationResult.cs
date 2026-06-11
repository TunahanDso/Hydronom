using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Vehicles
{
    /// <summary>
    /// Vehicle Profile Package doğrulama sonucudur.
    ///
    /// Loader, registry, scenario runtime ve ground station bu sonucu kullanarak
    /// profil paketinin güvenle kullanılabilir olup olmadığını anlayabilir.
    /// </summary>
    public sealed record VehicleProfileValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings)
    {
        public static VehicleProfileValidationResult Success { get; } = new(
            IsValid: true,
            Errors: Array.Empty<string>(),
            Warnings: Array.Empty<string>());

        public bool HasWarnings => Warnings.Count > 0;
        public bool HasErrors => Errors.Count > 0;

        public static VehicleProfileValidationResult From(
            IEnumerable<string>? errors,
            IEnumerable<string>? warnings = null)
        {
            var errorList = Normalize(errors);
            var warningList = Normalize(warnings);

            return new VehicleProfileValidationResult(
                IsValid: errorList.Count == 0,
                Errors: errorList,
                Warnings: warningList);
        }

        public VehicleProfileValidationResult Merge(VehicleProfileValidationResult? other)
        {
            if (other is null)
                return this;

            var errors = Errors
                .Concat(other.Errors)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var warnings = Warnings
                .Concat(other.Warnings)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new VehicleProfileValidationResult(
                IsValid: errors.Length == 0,
                Errors: errors,
                Warnings: warnings);
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string>? values)
        {
            return values?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }
    }
}