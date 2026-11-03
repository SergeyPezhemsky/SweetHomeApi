using SweetHomeApi.Controllers.Wardrobe;

namespace SweetHomeApi.Tests.Wardrobe;

public class WardrobeImageDetectionTests
{
    [Fact]
    public void DetectsSupportedFormatsByMagicBytes()
    {
        Assert.Equal("image/jpeg", WardrobeController.DetectImageContentType([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]));
        Assert.Equal("image/png", WardrobeController.DetectImageContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.Equal("image/webp", WardrobeController.DetectImageContentType("RIFF\0\0\0\0WEBPVP8 "u8.ToArray()));
    }

    [Fact]
    public void RejectsOtherFiles()
    {
        Assert.Null(WardrobeController.DetectImageContentType("GIF89a"u8.ToArray()));
        Assert.Null(WardrobeController.DetectImageContentType("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray()));
        Assert.Null(WardrobeController.DetectImageContentType([]));
    }
}
