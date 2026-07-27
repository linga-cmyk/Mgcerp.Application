using static Dapper.SqlMapper;

namespace Mgcerp.Application.Common
{
    public static class TimeZoneHelper
    {
        private static readonly Dictionary<string, string> TimeZones =
            new(StringComparer.OrdinalIgnoreCase)
            {
            { "UAE", "Arabian Standard Time" },
            { "KSA", "Arab Standard Time" },
            { "KWT", "Arab Standard Time" },
            { "IND", "India Standard Time" },
            { "BHR", "Arab Standard Time" },
            { "SYR", "Syria Standard Time" },
            { "SGP", "Singapore Standard Time" },
            { "TZA", "E. Africa Standard Time" },
            { "SDN", "Sudan Standard Time" }
            };

        /// <summary>
        /// Returns current UTC time.
        /// </summary>
        public static DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// Returns current date/time for the specified country.
        /// </summary>
        public static DateTime Now(string countryCode)
        {
            return FromUtc(DateTime.UtcNow, countryCode);
        }

        /// <summary>
        /// Converts UTC date/time to the specified country's local time.
        /// </summary>
        public static DateTime FromUtc(
            DateTime utcDateTime,
            string countryCode)
        {
            var timeZone = GetTimeZone(countryCode);

            if (utcDateTime.Kind == DateTimeKind.Local)
            {
                utcDateTime = utcDateTime.ToUniversalTime();
            }
            else if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                utcDateTime = DateTime.SpecifyKind(
                    utcDateTime,
                    DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(
                utcDateTime,
                timeZone);
        }

        /// <summary>
        /// Converts a country's local date/time to UTC.
        /// </summary>
        public static DateTime ToUtc(
            DateTime localDateTime,
            string countryCode)
        {
            var timeZone = GetTimeZone(countryCode);

            localDateTime = DateTime.SpecifyKind(
                localDateTime,
                DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(
                localDateTime,
                timeZone);
        }

        /// <summary>
        /// Gets TimeZoneInfo based on country code.
        /// </summary>
        private static TimeZoneInfo GetTimeZone(
            string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                throw new ArgumentException(
                    "Country code is required.",
                    nameof(countryCode));
            }

            if (!TimeZones.TryGetValue(
                countryCode,
                out var timeZoneId))
            {
                throw new ArgumentException(
                    $"Time zone is not configured for country: {countryCode}");
            }

            return TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneId);
        }
    }

    #region How to USE

    // Current UAE time
    //var uaeTime = TimeZoneHelper.Now("UAE");

    // Current Saudi Arabia time

    // var ksaTime = TimeZoneHelper.Now("KSA");


    // Current India time
    //var indiaTime = TimeZoneHelper.Now("IND");

    // Current Singapore time
        //var singaporeTime = TimeZoneHelper.Now("SGP");

    // DB Datetime to local
        //var localTime = TimeZoneHelper.FromUtc(entity.CreatedDate,countryCode);

    //Transaction to UTC  (ex: user entered local time, save in db UTC)
        //var utcTime = TimeZoneHelper.ToUtc(request.TransactionDate,    countryCode);
        #endregion
    }
