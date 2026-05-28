namespace BiyaHero.Api.Features.Heatmap;

/// <summary>
/// Represents a geographic bounding box defined by latitude and longitude ranges.
/// </summary>
public readonly struct GeoBoundingBox
{
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLng { get; }
    public double MaxLng { get; }

    public GeoBoundingBox(double minLat, double maxLat, double minLng, double maxLng)
    {
        MinLat = minLat;
        MaxLat = maxLat;
        MinLng = minLng;
        MaxLng = maxLng;
    }

    /// <summary>
    /// Returns true if this bounding box intersects with another bounding box.
    /// </summary>
    public bool Intersects(GeoBoundingBox other)
    {
        return MinLat <= other.MaxLat
            && MaxLat >= other.MinLat
            && MinLng <= other.MaxLng
            && MaxLng >= other.MinLng;
    }
}

/// <summary>
/// AOT-safe geohash encoder/decoder and bounding-box aggregation utility.
/// Uses standard geohash base32 encoding (characters: 0123456789bcdefghjkmnpqrstuvwxyz).
/// 
/// Precision 5 (~5 km cells) is used for DynamoDB partition routing.
/// Precision 7 (~150 m cells) is used for heatmap response tiles.
/// </summary>
public static class GeohashUtil
{
    /// <summary>Geohash precision for DynamoDB partition routing (~5 km cells).</summary>
    public const int PartitionPrecision = 5;

    /// <summary>Geohash precision for heatmap response tiles (~150 m cells).</summary>
    public const int TilePrecision = 7;

    private const string Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz";

    // Lookup table for decoding: character → index (0–31).
    // Indexed by char value; -1 means invalid.
    private static readonly int[] CharToIndex = BuildCharToIndex();

    private static int[] BuildCharToIndex()
    {
        var table = new int[128];
        Array.Fill(table, -1);
        for (int i = 0; i < Base32Chars.Length; i++)
        {
            table[Base32Chars[i]] = i;
        }
        return table;
    }

    /// <summary>
    /// Encode a (latitude, longitude) pair into a geohash string at the given precision.
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180).</param>
    /// <param name="precision">Number of characters in the resulting geohash (typically 5 or 7).</param>
    /// <returns>A geohash string of the specified length.</returns>
    public static string Encode(double latitude, double longitude, int precision)
    {
        if (precision <= 0)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be positive.");

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;

        Span<char> result = stackalloc char[precision];
        int bit = 0;
        int charIndex = 0;
        int currentChar = 0;
        bool isLng = true; // longitude bit comes first in geohash

        while (charIndex < precision)
        {
            if (isLng)
            {
                double mid = (lngMin + lngMax) / 2.0;
                if (longitude >= mid)
                {
                    currentChar |= (1 << (4 - bit));
                    lngMin = mid;
                }
                else
                {
                    lngMax = mid;
                }
            }
            else
            {
                double mid = (latMin + latMax) / 2.0;
                if (latitude >= mid)
                {
                    currentChar |= (1 << (4 - bit));
                    latMin = mid;
                }
                else
                {
                    latMax = mid;
                }
            }

            isLng = !isLng;
            bit++;

            if (bit == 5)
            {
                result[charIndex] = Base32Chars[currentChar];
                charIndex++;
                bit = 0;
                currentChar = 0;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Encode a coordinate to geohash precision 5 (~5 km cell) for DynamoDB partition routing.
    /// </summary>
    public static string EncodeForPartition(double latitude, double longitude)
    {
        return Encode(latitude, longitude, PartitionPrecision);
    }

    /// <summary>
    /// Encode a coordinate to geohash precision 7 (~150 m cell) for heatmap tile resolution.
    /// </summary>
    public static string EncodeForTile(double latitude, double longitude)
    {
        return Encode(latitude, longitude, TilePrecision);
    }

    /// <summary>
    /// Decode a geohash string into its bounding box.
    /// </summary>
    /// <param name="geohash">A valid geohash string.</param>
    /// <returns>The bounding box that the geohash cell represents.</returns>
    public static GeoBoundingBox Decode(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
            throw new ArgumentException("Geohash must not be null or empty.", nameof(geohash));

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;
        bool isLng = true;

        for (int i = 0; i < geohash.Length; i++)
        {
            char c = geohash[i];
            if (c >= 128 || CharToIndex[c] < 0)
                throw new ArgumentException($"Invalid geohash character: '{c}'.", nameof(geohash));

            int charValue = CharToIndex[c];

            for (int bit = 4; bit >= 0; bit--)
            {
                if (isLng)
                {
                    double mid = (lngMin + lngMax) / 2.0;
                    if ((charValue & (1 << bit)) != 0)
                        lngMin = mid;
                    else
                        lngMax = mid;
                }
                else
                {
                    double mid = (latMin + latMax) / 2.0;
                    if ((charValue & (1 << bit)) != 0)
                        latMin = mid;
                    else
                        latMax = mid;
                }

                isLng = !isLng;
            }
        }

        return new GeoBoundingBox(latMin, latMax, lngMin, lngMax);
    }

    /// <summary>
    /// Returns all geohash cells at the given precision that intersect the specified bounding box.
    /// Uses an iterative expansion approach: starts from corner geohashes and expands to cover the bbox.
    /// </summary>
    /// <param name="bbox">The bounding box to cover.</param>
    /// <param name="precision">The geohash precision (typically 5 or 7).</param>
    /// <returns>A list of geohash strings that intersect the bounding box.</returns>
    public static List<string> GetGeohashesInBbox(GeoBoundingBox bbox, int precision)
    {
        if (precision <= 0)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be positive.");

        var result = new List<string>();
        var visited = new HashSet<string>();

        // Compute the approximate cell size at this precision to step through the bbox
        // Each geohash character adds 2.5 bits of longitude and 2.5 bits of latitude on average.
        // Precision p: lat bits = floor(5p/2), lng bits = ceil(5p/2)
        int totalBits = 5 * precision;
        int lngBits = (totalBits + 1) / 2; // longitude gets the first bit
        int latBits = totalBits / 2;

        double latCellSize = 180.0 / (1 << latBits);
        double lngCellSize = 360.0 / (1 << lngBits);

        // Step through the bbox with steps smaller than cell size to ensure we don't miss cells.
        // Using half the cell size as step ensures coverage.
        double latStep = latCellSize * 0.5;
        double lngStep = lngCellSize * 0.5;

        // Clamp the bbox to valid ranges
        double minLat = Math.Max(bbox.MinLat, -90.0);
        double maxLat = Math.Min(bbox.MaxLat, 90.0);
        double minLng = Math.Max(bbox.MinLng, -180.0);
        double maxLng = Math.Min(bbox.MaxLng, 180.0);

        double lat = minLat;
        while (lat <= maxLat)
        {
            double lng = minLng;
            while (lng <= maxLng)
            {
                // Clamp to valid encoding range
                double clampedLat = Math.Min(lat, 89.99999999);
                double clampedLng = Math.Min(lng, 179.99999999);

                string hash = Encode(clampedLat, clampedLng, precision);
                if (visited.Add(hash))
                {
                    // Verify the cell actually intersects the bbox
                    var cellBbox = Decode(hash);
                    if (cellBbox.Intersects(bbox))
                    {
                        result.Add(hash);
                    }
                }

                lng += lngStep;
            }
            lat += latStep;
        }

        // Also check the corners and edges to ensure we don't miss boundary cells
        AddCornerHash(bbox.MinLat, bbox.MinLng, precision, bbox, visited, result);
        AddCornerHash(bbox.MinLat, bbox.MaxLng, precision, bbox, visited, result);
        AddCornerHash(bbox.MaxLat, bbox.MinLng, precision, bbox, visited, result);
        AddCornerHash(bbox.MaxLat, bbox.MaxLng, precision, bbox, visited, result);

        return result;
    }

    private static void AddCornerHash(
        double lat, double lng, int precision,
        GeoBoundingBox bbox, HashSet<string> visited, List<string> result)
    {
        double clampedLat = Math.Clamp(lat, -90.0, 89.99999999);
        double clampedLng = Math.Clamp(lng, -180.0, 179.99999999);

        string hash = Encode(clampedLat, clampedLng, precision);
        if (visited.Add(hash))
        {
            var cellBbox = Decode(hash);
            if (cellBbox.Intersects(bbox))
            {
                result.Add(hash);
            }
        }
    }
}
