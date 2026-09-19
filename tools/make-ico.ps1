# Generates src/SmsNudge/assets/app-icon.ico from the favicon PNG
# (or any other source PNG) using System.Drawing.
param(
    [string]$Source = (Join-Path (Split-Path $PSScriptRoot) "tools\assets\favicon.png"),
    [string]$Output = (Join-Path (Split-Path $PSScriptRoot) "src\SmsNudge\assets\app-icon.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile($Source)
try
{
    $sizes = @(16, 32, 48, 64, 256)
    $entries = New-Object System.Collections.Generic.List[byte[]]
    foreach ($s in $sizes)
    {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        try
        {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            try
            {
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.DrawImage($src, 0, 0, $s, $s)
            }
            finally { $g.Dispose() }

            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $entries.Add($ms.ToArray())
        }
        finally { $bmp.Dispose() }
    }

    # ICO container: header + directory + image data
    $out = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($out)
    try
    {
        $bw.Write([UInt16]0)      # reserved
        $bw.Write([UInt16]1)      # type: icon
        $bw.Write([UInt16]$sizes.Count)

        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++)
        {
            $s = $sizes[$i]
            $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
            $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
            $bw.Write([Byte]0)    # palette
            $bw.Write([Byte]0)    # reserved
            $bw.Write([UInt16]1)  # color planes
            $bw.Write([UInt16]32) # bpp
            $bw.Write([UInt32]$entries[$i].Length)
            $bw.Write([UInt32]$offset)
            $offset += $entries[$i].Length
        }
        foreach ($data in $entries) { $bw.Write($data) }
    }
    finally { $bw.Dispose() }

    New-Item (Split-Path $Output) -ItemType Directory -Force | Out-Null
    [System.IO.File]::WriteAllBytes($Output, $out.ToArray())
    Write-Host "Icon written: $Output ($($out.Length) bytes)"
}
finally { $src.Dispose() }
