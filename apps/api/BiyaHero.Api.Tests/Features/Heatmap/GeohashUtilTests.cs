using BiyaHero.Api.Features.Heatmap;
using Xunit;

namespace BiyaHero.Api.Tests.Features.Heatmap;

public class GeohashUtilTests
{
    // ─── Encode Tests ─────────────────────────────────────────────────────

    [Fact]
    public void Encode_KnownLocation_ReturnsExpectedGeohash()
    {
        // Manila, Philippines (approx 14.5995, 120.9842)
        string hash = GeohashUtil.Encode(14.5995, 120.9842, 7);
        Assert.Equal(7, hash.Length);
        // The geohash for Manila at precision 7 should start with "wdw5"
        Assert.StartsWith("wdw5", hash);
    }

    [Fact]
    public void Encode_Precision5_Returns5Characters()
    {
        string hash = GeohashUtil.Encode(14.5995, 120.9842, 5);
        Assert.Equal(5, hash.Length);
    }

    [Fact]
    public void Encode_Precision7_Returns7Characters()
    {
        string hash = GeohashUtil.Encode(14.5995, 120.9842, 7);
        Assert.Equal(7, hash.Length);
    }

    [Fact]
    public void Encode_OriginPoint_ReturnsKnownGeohash()
    {
        // (0, 0) should encode to "s0000..." 
        string hash = GeohashUtil.Encode(0.0, 0.0, 5);
        Assert.Equal("s0000", hash);
    }

    [Fact]
    public void Encode_NegativeCoordinates_Works()
    {
        // Should not throw for valid negative coordinates
        string hash = GeohashUtil.Encode(-45.0, -90.0, 5);
        Assert.Equal(5, hash.Length);
        Assert.All(hash, c => Assert.Contains(c, "0123456789bcdefghjkmnpqrstuvwxyz"));
    }

    [Fact]
    public void Encode_InvalidPrecision_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeohashUtil.Encode(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeohashUtil.Encode(0, 0, -1));
    }

    [Fact]
    public void Encode_UsesOnlyValidBase32Characters()
    {
        const string validChars = "0123456789bcdefghjkmnpqrstuvwxyz";
        string hash = GeohashUtil.Encode(14.5995, 120.9842, 12);
        Assert.All(hash, c => Assert.Contains(c, validChars));
    }

    // ─── Decode Tests ─────────────────────────────────────────────────────

    [Fact]
    public void Decode_ValidGeohash_ReturnsBoundingBox()
    {
        var bbox = GeohashUtil.Decode("wdw5e");
        Assert.True(bbox.MinLat < bbox.MaxLat);
        Assert.True(bbox.MinLng < bbox.MaxLng);
    }

    [Fact]
    public void Decode_OriginGeohash_ContainsOrigin()
    {
        var bbox = GeohashUtil.Decode("s0000");
        Assert.True(bbox.MinLat <= 0.0 && bbox.MaxLat >= 0.0);
        Assert.True(bbox.MinLng <= 0.0 && bbox.MaxLng >= 0.0);
    }

    [Fact]
    public void Decode_NullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeohashUtil.Decode(null!));
        Assert.Throws<ArgumentException>(() => GeohashUtil.Decode(""));
    }

    [Fact]
    public void Decode_InvalidCharacter_Throws()
    {
        // 'a', 'i', 'l', 'o' are not in geohash base32
        Assert.Throws<ArgumentException>(() => GeohashUtil.Decode("abcde"));
    }

    // ─── Round-trip Tests ─────────────────────────────────────────────────

    [Theory]
    [InlineData(14.5995, 120.9842, 5)]
    [InlineData(14.5995, 120.9842, 7)]
    [InlineData(-33.8688, 151.2093, 5)]  // Sydney
    [InlineData(51.5074, -0.1278, 7)]    // London
    [InlineData(0.0, 0.0, 5)]
    [InlineData(-89.9, -179.9, 5)]
    [InlineData(89.9, 179.9, 5)]
    public void Encode_ThenDecode_PointInsideBbox(double lat, double lng, int precision)
    {
        string hash = GeohashUtil.Encode(lat, lng, precision);
        var bbox = GeohashUtil.Decode(hash);

        Assert.True(lat >= bbox.MinLat && lat <= bbox.MaxLat,
            $"Latitude {lat} not in [{bbox.MinLat}, {bbox.MaxLat}]");
        Assert.True(lng >= bbox.MinLng && lng <= bbox.MaxLng,
            $"Longitude {lng} not in [{bbox.MinLng}, {bbox.MaxLng}]");
    }

    [Fact]
    public void Encode_SameInput_SameOutput()
    {
        string hash1 = GeohashUtil.Encode(14.5995, 120.9842, 7);
        string hash2 = GeohashUtil.Encode(14.5995, 120.9842, 7);
        Assert.Equal(hash1, hash2);
    }

    // ─── Bbox Aggregation Tests ───────────────────────────────────────────

    [Fact]
    public void GetGeohashesInBbox_SmallArea_ReturnsNonEmpty()
    {
        // Small area around Manila
        var bbox = new GeoBoundingBox(14.59, 14.61, 120.97, 120.99);
        var hashes = GeohashUtil.GetGeohashesInBbox(bbox, 7);

        Assert.NotEmpty(hashes);
        Assert.All(hashes, h => Assert.Equal(7, h.Length));
    }

    [Fact]
    public void GetGeohashesInBbox_AllCellsIntersectBbox()
    {
        var bbox = new GeoBoundingBox(14.5, 14.7, 120.9, 121.1);
        var hashes = GeohashUtil.GetGeohashesInBbox(bbox, 5);

        foreach (var hash in hashes)
        {
            var cellBbox = GeohashUtil.Decode(hash);
            Assert.True(cellBbox.Intersects(bbox),
                $"Cell {hash} does not intersect the query bbox");
        }
    }

    [Fact]
    public void GetGeohashesInBbox_ContainsHashOfPointInBbox()
    {
        // A point inside the bbox should have its geohash in the result
        double lat = 14.6;
        double lng = 121.0;
        var bbox = new GeoBoundingBox(14.5, 14.7, 120.9, 121.1);

        string pointHash = GeohashUtil.Encode(lat, lng, 5);
        var hashes = GeohashUtil.GetGeohashesInBbox(bbox, 5);

        Assert.Contains(pointHash, hashes);
    }

    [Fact]
    public void GetGeohashesInBbox_Precision5_ForPartitionRouting()
    {
        // Metro Manila approximate bbox
        var bbox = new GeoBoundingBox(14.35, 14.75, 120.90, 121.10);
        var hashes = GeohashUtil.GetGeohashesInBbox(bbox, 5);

        Assert.NotEmpty(hashes);
        Assert.All(hashes, h => Assert.Equal(5, h.Length));
        // At precision 5 (~5km cells), Metro Manila should have a manageable number of cells
        Assert.True(hashes.Count > 0 && hashes.Count < 100,
            $"Expected reasonable cell count for Metro Manila at precision 5, got {hashes.Count}");
    }

    [Fact]
    public void GetGeohashesInBbox_Precision7_ForResponseTiles()
    {
        // Small area for precision 7 (~150m cells)
        var bbox = new GeoBoundingBox(14.59, 14.60, 120.98, 120.99);
        var hashes = GeohashUtil.GetGeohashesInBbox(bbox, 7);

        Assert.NotEmpty(hashes);
        Assert.All(hashes, h => Assert.Equal(7, h.Length));
    }

    [Fact]
    public void GetGeohashesInBbox_NoDuplicates()
    {
        var bbox = new GeoBoundingBox(14.5, 14.7, 120.9, 121.1);
        var hashes = GeohashUtil.GetGeohashesInBbox(bbox, 5);

        Assert.Equal(hashes.Count, hashes.Distinct().Count());
    }

    [Fact]
    public void GetGeohashesInBbox_InvalidPrecision_Throws()
    {
        var bbox = new GeoBoundingBox(14.5, 14.7, 120.9, 121.1);
        Assert.Throws<ArgumentOutOfRangeException>(() => GeohashUtil.GetGeohashesInBbox(bbox, 0));
    }

    // ─── GeoBoundingBox Tests ─────────────────────────────────────────────

    [Fact]
    public void GeoBoundingBox_Intersects_OverlappingBoxes_ReturnsTrue()
    {
        var a = new GeoBoundingBox(10, 20, 100, 110);
        var b = new GeoBoundingBox(15, 25, 105, 115);
        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void GeoBoundingBox_Intersects_NonOverlapping_ReturnsFalse()
    {
        var a = new GeoBoundingBox(10, 20, 100, 110);
        var b = new GeoBoundingBox(25, 35, 115, 125);
        Assert.False(a.Intersects(b));
        Assert.False(b.Intersects(a));
    }

    [Fact]
    public void GeoBoundingBox_Intersects_TouchingEdge_ReturnsTrue()
    {
        var a = new GeoBoundingBox(10, 20, 100, 110);
        var b = new GeoBoundingBox(20, 30, 100, 110);
        Assert.True(a.Intersects(b));
    }
}
