using System.Text;

using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Domain;

/// <summary>Перевірки тексту форм каталогу (пункти 43, 45, 75); перенесено з Python tests/test_catalog_pictures.py і test_product_edit.py.</summary>
public class CatalogTextTests
{
    public static TheoryData<byte[], string?> Pictures => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x30], "jpg" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x2E], "png" },
        { "GIF89a..."u8.ToArray(), "gif" },
        { "RIFF1234WEBPVP8 "u8.ToArray(), "webp" },
        { "<?php system($_GET['c']);"u8.ToArray(), null },
        { "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>"u8.ToArray(), null }, // у SVG виконуються скрипти
        { [(byte)'M', (byte)'Z', 0x90, 0x00], null },
        { "RIFF1234WAVEfmt "u8.ToArray(), null },
        { [], null },
    };

    [Theory]
    [MemberData(nameof(Pictures))]
    public void PictureType_ComesFromTheFirstBytes(byte[] content, string? extension) =>
        Assert.Equal(extension, CatalogText.PictureExtension(content));

    [Theory]
    [InlineData("", "")]
    [InlineData("  catalog/dresses/lion.jpg ", "catalog/dresses/lion.jpg")]
    [InlineData("catalog/Сукні/лев 2.WEBP", "catalog/Сукні/лев 2.WEBP")]
    public void ImagePath_KeepsPathsBelowCatalog(string value, string expected) => Assert.Equal(expected, CatalogText.ImagePath(value));

    [Theory]
    [InlineData("../../config.php")]
    [InlineData("catalog/a.php")]
    [InlineData("catalog/../config.jpg")]
    [InlineData("catalog//a.jpg")]
    [InlineData("image/catalog/a.jpg")]
    [InlineData("catalog/a.jpg\nx.php")]
    [InlineData("catalog/<b>.jpg")]
    public void ImagePath_RefusesEverythingElse(string value) => Assert.Throws<DomainException>(() => CatalogText.ImagePath(value));

    [Theory]
    [InlineData(" Suknia-Lev_2 ", "suknia-lev_2")]
    [InlineData("", "")]
    public void SeoKeyword_IsLowerLatin(string value, string expected) => Assert.Equal(expected, CatalogText.SeoKeyword(value));

    [Theory]
    [InlineData("Сукня лев")]
    [InlineData("a--b")]
    [InlineData("-a")]
    [InlineData("a b")]
    public void SeoKeyword_RefusesTheRest(string value) => Assert.Throws<DomainException>(() => CatalogText.SeoKeyword(value));

    [Fact]
    public void WithoutNewTag_KeepsTheOtherTagsInOrder() =>
        Assert.Equal("літо,сукні", CatalogText.WithoutNewTag("літо, Тема:new ,сукні,"));

    [Fact]
    public void ThePngSignature_IsWhatTheStandardSays() =>
        Assert.Equal("png", CatalogText.PictureExtension([0x89, .. Encoding.ASCII.GetBytes("PNG\r\n\u001a\n")]));
}
