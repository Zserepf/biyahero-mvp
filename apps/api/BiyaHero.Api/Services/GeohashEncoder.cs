namespace BiyaHero.Api.Services;

/// <summary>
/// Pure utility for encoding latitude/longitude coordinates into geohash strings.
/// Precision 5 (~5 km cells) is used for DynamoDB partition key routing.
/// Precision 7 (~150 m cells) is used for heatmap tile response granularity.
/// No I/O — all operations are deterministic and side-effect free.
/// </summary>
public static class GeohashEncoder
{
    /// <summary>Geohash precision for DynamoDB partition routing (~5 km cells).</summary>
    public const int PartitionPrecision = 5;

    /// <summary>Geohash precision for heatmap response tiles (~150 m cells).</summary>
    public const int TilePrecision = 7;

    private const string Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Encode a coordinate to a geohash string at the specified precision.
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180).</param>
    /// <param name="precision">Number of characters in the resulting geohash (1–12).</param>
    /// <returns>A geohash string of the specified length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude, longitude, or precision is out of valid range.
    /// </exception>
    public static string Encode(double latitude, double longitude, int precision)
    {
        if (latitude < -90.0 || latitude > 90.0)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        if (longitude < -180.0 || longitude > 180.0)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        if (precision < 1 || precision > 12)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be between 1 and 12.");

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;

        var hash = new char[precision];
        int bit = 0;
        int charIndex = 0;
        int currentValue = 0;
        bool isLongitude = true;

        while (charIndex < precision)
        {
            if (isLongitude)
            {
                double mid = (lngMin + lngMax) / 2.0;
                if (longitude >= mid)
                {
                    currentValue = (currentValue << 1) | 1;
                    lngMin = mid;
                }
                else
                {
                    currentValue <<= 1;
                    lngMax = mid;
                }
            }
            else
            {
                double mid = (latMin + latMax) / 2.0;
                if (latitude >= mid)
                {
                    currentValue = (currentValue << 1) | 1;
                    latMin = mid;
                }
                else
                {
                    currentValue <<= 1;
                    latMax = mid;
                }
            }

            isLongitude = !isLongitude;
            bit++;

            if (bit == 5)
            {
                hash[charIndex] = Base32Chars[currentValue];
                charIndex++;
                bit = 0;
                currentValue = 0;
            }
        }

        return new string(hash);
    }

    /// <summary>
    /// Encode a coordinate to geohash precision 5 for DynamoDB partition routing.
    /// </summary>
    public static string EncodeForPartition(double latitude, double longitude)
    {
        return Encode(latitude, longitude, PartitionPrecision);
    }

    /// <summary>
    /// Encode a coordinate to geohash precision 7 for heatmap tile resolution.
    /// </summary>
    public static string EncodeForTile(double latitude, double longitude)
    {
        return Encode(latitude, longitude, TilePrecision);
    }

    /// <summary>
    /// Decode a geohash string back to its approximate center coordinate.
    /// Returns the center point of the geohash cell.
    /// </summary>
    /// <param name="geohash">A valid geohash string.</param>
    /// <returns>A tuple of (latitude, longitude) representing the cell center.</returns>
    /// <exception cref="ArgumentException">Thrown when the geohash is null, empty, or contains invalid characters.</exception>
    public static (double Latitude, double Longitude) Decode(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
            throw new ArgumentException("Geohash cannot be null or empty.", nameof(geohash));

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;
        bool isLongitude = true;

        foreach (char c in geohash)
        {
            int charValue = Base32Chars.IndexOf(c);
            if (charValue < 0)
                throw new ArgumentException($"Invalid geohash character: '{c}'.", nameof(geohash));

            for (int bit = 4; bit >= 0; bit--)
            {
                int mask = 1 << bit;
                if (isLongitude)
                {
                    double mid = (lngMin + lngMax) / 2.0;
                    if ((charValue & mask) != 0)
                        lngMin = mid;
                    else
                        lngMax = mid;
                }
                else
                {
                    double mid = (latMin + latMax) / 2.0;
                    if ((charValue & mask) != 0)
                        latMin = mid;
                    else
                        latMax = mid;
                }
                isLongitude = !isLongitude;
            }
        }

        return ((latMin + latMax) / 2.0, (lngMin + lngMax) / 2.0);
    }

    /// <summary>
    /// Get the bounding box of a geohash cell.
    /// </summary>
    /// <param name="geohash">A valid geohash string.</param>
    /// <returns>A tuple of (minLat, minLng, maxLat, maxLng).</returns>
    public static (double MinLat, double MinLng, double MaxLat, double MaxLng) DecodeBbox(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
            throw new ArgumentException("Geohash cannot be null or empty.", nameof(geohash));

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;
        bool isLongitude = true;

        foreach (char c in geohash)
        {
            int charValue = Base32Chars.IndexOf(c);
            if (charValue < 0)
                throw new ArgumentException($"Invalid geohash character: '{c}'.", nameof(geohash));

            for (int bit = 4; bit >= 0; bit--)
            {
                int mask = 1 << bit;
                if (isLongitude)
                {
                    double mid = (lngMin + lngMax) / 2.0;
                    if ((charValue & mask) != 0)
                        lngMin = mid;
                    else
                        lngMax = mid;
                }
                else
                {
                    double mid = (latMin + latMax) / 2.0;
                    if ((charValue & mask) != 0)
                        latMin = mid;
                    else
                        latMax = mid;
                }
                isLongitude = !isLongitude;
            }
        }

        return (latMin, lngMin, latMax, lngMax);
    }
}
