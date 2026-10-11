using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace GroupAnnouncementApp.Helpers;

// Makes a photo small enough to store: tries smaller sizes and lower JPEG quality until it fits.
public static class ImageShrinker
{
    // Returns JPEG bytes no bigger than maxBytes, or null when the image cannot be made small enough.
    public static byte[]? Shrink(byte[] original, int maxBytes)
    {
        float[] sizes = new float[] { 1600f, 1280f, 1024f, 800f, 640f, 480f };
        float[] qualities = new float[] { 0.8f, 0.6f, 0.45f };

        try
        {
            using MemoryStream input = new MemoryStream(original);
            using Microsoft.Maui.Graphics.IImage image = PlatformImage.FromStream(input);

            for (int i = 0; i < sizes.Length; i++)
            {
                for (int j = 0; j < qualities.Length; j++)
                {
                    using Microsoft.Maui.Graphics.IImage small = image.Downsize(sizes[i], false);
                    byte[] jpeg = small.AsBytes(ImageFormat.Jpeg, qualities[j]);
                    if (jpeg.Length <= maxBytes)
                    {
                        return jpeg;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SHRINK IMAGE ERROR: " + ex);
        }

        return null;
    }
}
