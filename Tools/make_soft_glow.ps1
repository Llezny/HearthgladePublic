# Writes Assets/Arts/Textures/VFX/SoftGlow.png: a white dot with a soft falloff, used (tinted by the particle colour) for fireflies.
Add-Type -AssemblyName System.Drawing
$size = 64
$bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $size; $y++) {
    for ($x = 0; $x -lt $size; $x++) {
        $dx = ($x + 0.5 - $size / 2) / ($size / 2)
        $dy = ($y + 0.5 - $size / 2) / ($size / 2)
        $r = [Math]::Sqrt($dx * $dx + $dy * $dy)
        $halo = [Math]::Pow([Math]::Max(0.0, 1 - $r), 2.2)
        $core = [Math]::Max(0.0, 1 - $r / 0.18)
        $alpha = [Math]::Min(1.0, $halo + $core)
        $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int](255 * $alpha), 255, 255, 255))
    }
}
$path = Join-Path $PSScriptRoot "..\Assets\Arts\Textures\VFX\SoftGlow.png"
$bitmap.Save((Resolve-Path (Split-Path $path)).Path + "\SoftGlow.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
