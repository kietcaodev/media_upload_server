namespace MediaUpload.Application.Services;

/// <summary>
/// Validates uploaded file content by inspecting its magic bytes (file
/// signature), so a malicious upload can't bypass the extension allow-list by
/// simply renaming e.g. an executable to ".mp4". Ported from the legacy
/// Node.js prototype's validateFileType().
/// </summary>
public static class FileSignatureValidator
{
    public static async Task<bool> IsValidAsync(string filePath)
    {
        try
        {
            var buffer = new byte[24];
            await using var fs = File.OpenRead(filePath);
            var read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read < 4) return false;

            var hex = Convert.ToHexString(buffer, 0, read);

            // ===== Images =====
            if (hex.StartsWith("FFD8FF")) return true; // JPEG
            if (hex.StartsWith("89504E47")) return true; // PNG
            if (hex.StartsWith("474946383761") || hex.StartsWith("474946383961")) return true; // GIF
            if (hex.StartsWith("424D")) return true; // BMP
            if (hex.StartsWith("49492A00") || hex.StartsWith("4D4D002A")) return true; // TIFF
            if (hex.StartsWith("00000100")) return true; // ICO

            // RIFF containers: WebP / WAV / AVI
            if (hex.StartsWith("52494646") && read >= 12)
            {
                var riffType = System.Text.Encoding.ASCII.GetString(buffer, 8, 4);
                if (riffType is "WEBP" or "WAVE" || riffType.StartsWith("AVI")) return true;
            }

            // ISO base media containers (MP4 / MOV / HEIC / HEIF) – "ftyp" box at offset 4
            if (read >= 8 && System.Text.Encoding.ASCII.GetString(buffer, 4, 4) == "ftyp")
                return true;

            // Matroska / WebM
            if (hex.StartsWith("1A45DFA3")) return true;
            // FLV
            if (hex.StartsWith("464C56")) return true;
            // ASF (WMV/WMA) container
            if (hex.StartsWith("3026B275")) return true;

            // ===== Audio =====
            string[] audioSignatures = ["FFFB", "FFF3", "FFF2", "494433", "664C6143", "4F676753"];
            if (audioSignatures.Any(hex.StartsWith)) return true;

            return false;
        }
        catch
        {
            return false;
        }
    }
}
