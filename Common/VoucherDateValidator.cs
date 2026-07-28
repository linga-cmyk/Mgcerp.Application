using Mgcerp.Infrastructure.Helpers;
using Mgcerp.Models;

namespace Mgcerp.Application.Common
{
    /// <summary>
    /// Voucher-date entry validation, ported from the legacy PowerBuilder
    /// back-dated entry check. Reusable across every transaction that needs to
    /// guard a voucher/document date against the financial-year entry window
    /// and the per-user back-dating restriction — inject <see cref="IVoucherDateValidator"/>
    /// wherever a date must be validated.
    ///
    /// Rules, applied in order (mirrors the original script):
    ///   1. The entry window is FYMAS.s_dt_entry .. e_dt_entry — UNLESS the user
    ///      has NO row in partila_month_user_access for that FY, in which case
    ///      the window narrows to FYMAS.partila_month_close_start .. _end.
    ///   2. The voucher date must fall inside that window.
    ///   3. If dicuser.Ent_c_int = 'Y', the date must also be no older than
    ///      today - Ent_days (the per-user back-date cap). Otherwise there is
    ///      effectively no lower cap (the script used today - 50000 days).
    /// </summary>
    public interface IVoucherDateValidator
    {
        /// <summary>
        /// Validates <paramref name="voucherDate"/> for the given financial year
        /// and user. Returns a <see cref="ResponseResult"/> with IsValid = true
        /// when the date is allowed, or IsValid = false and a descriptive
        /// ErrorMessage when it is outside the permitted period.
        /// </summary>
        /// <param name="region">Country/region routing key for the connection (same value services pass as countryCode). Null uses the default connection.</param>
        Task<ResponseResult> ValidateAsync(string fyCode, string userId, DateTime voucherDate, string? region = null);
    }

    public class VoucherDateValidator : IVoucherDateValidator
    {
        private readonly IDapperHelper _dapper;

        public VoucherDateValidator(IDapperHelper dapper) => _dapper = dapper;

        // Single-row projection of everything the rules need, fetched in one round-trip.
        private sealed class EntryWindow
        {
            public DateTime? StartEntry { get; set; }
            public DateTime? EndEntry { get; set; }
            public DateTime? PartialStart { get; set; }
            public DateTime? PartialEnd { get; set; }
            public string? EntCInt { get; set; }
            public decimal? EntDays { get; set; }
            public int PartialAccessCount { get; set; }
        }

        public async Task<ResponseResult> ValidateAsync(string fyCode, string userId, DateTime voucherDate, string? region = null)
        {
            if (string.IsNullOrWhiteSpace(fyCode))
                return Fail("Financial year is required to validate the voucher date.");
            if (string.IsNullOrWhiteSpace(userId))
                return Fail("User is required to validate the voucher date.");

            // FY entry window + the user's back-date flags + the partial-month
            // access flag, all in one query. Scalar sub-selects with aggregates
            // mirror the original MAX(...) / COUNT(*) reads and stay single-row
            // even if a lookup table ever had duplicates.
            const string sql = @"
                SELECT
                    StartEntry         = (SELECT max(s_dt_entry)                FROM fymas   WHERE fy_code = @fyCode),
                    EndEntry           = (SELECT max(e_dt_entry)                FROM fymas   WHERE fy_code = @fyCode),
                    PartialStart       = (SELECT max(partila_month_close_start) FROM fymas   WHERE fy_code = @fyCode),
                    PartialEnd         = (SELECT max(partila_month_close_end)   FROM fymas   WHERE fy_code = @fyCode),
                    EntCInt            = (SELECT max(Ent_c_int)                  FROM dicuser WHERE user_id = @userId),
                    EntDays            = (SELECT max(Ent_days)                   FROM dicuser WHERE user_id = @userId),
                    PartialAccessCount = (SELECT count(*) FROM partila_month_user_access WHERE fy_code = @fyCode AND userid = @userId)";

            var w = await _dapper.QuerySingleAsync<EntryWindow>(sql, new { fyCode, userId }, countryCode: region);

            if (w == null || w.StartEntry == null || w.EndEntry == null)
                return Fail("The entry period is not configured for the selected financial year.");

            var start = w.StartEntry.Value;
            var end = w.EndEntry.Value;

            // No partial-month access row for this user → restrict to the
            // partial-month close window instead of the full FY window.
            if (w.PartialAccessCount <= 0)
            {
                if (w.PartialStart == null || w.PartialEnd == null)
                    return Fail("The partial-month entry period is not configured for the selected financial year.");
                start = w.PartialStart.Value;
                end = w.PartialEnd.Value;
            }

            var today = ApplicationCommon.GetCurrentDateTime().Date;
            var entRestricted = string.Equals(w.EntCInt?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
            // 50000 days ≈ 137 years: effectively no lower bound, per the original.
            var backDate = entRestricted
                ? today.AddDays(-(double)(w.EntDays ?? 0m))
                : today.AddDays(-50000);

            // Compare on the date component only — the entry bounds are stored as
            // datetime (usually midnight) and the voucher date may carry a time.
            var vdate = voucherDate.Date;
            var startDate = start.Date;
            var endDate = end.Date;

            // 1. Inside the FY / partial-month window.
            if (vdate < startDate || vdate > endDate)
                return Fail($"Voucher date {vdate:dd/MM/yyyy} is outside the allowed entry period " +
                            $"({startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}).");

            // 2. Per-user back-date cap (only when Ent_c_int = 'Y').
            if (entRestricted && (vdate < backDate || vdate > endDate))
                return Fail($"Back-dated entry is restricted for this user. Allowed entry period is " +
                            $"{backDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}.");

            return new ResponseResult { IsValid = true };
        }

        private static ResponseResult Fail(string message) =>
            new() { IsValid = false, ErrorMessage = message };
    }
}
