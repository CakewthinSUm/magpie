# Builds AppIcon.ico from the one bird build/icon/gen.go draws, so the exe's
# and the tray's icon is the app's (make icons draws the rest).
#
#   pwsh desktop/Magpie.Desktop/Assets/make-ico.ps1
#
# An ICO is a header, one 16-byte entry per image, then each PNG as it is
# (Vista and later read a PNG image inside an ICO).
param([string]$Out = (Join-Path $PSScriptRoot 'AppIcon.ico'))
$ErrorActionPreference = 'Stop'

# the repo the app lives in: go.mod is the marker, not a count of folders
$root = $PSScriptRoot
while ($root -and -not (Test-Path (Join-Path $root 'go.mod'))) {
    $root = Split-Path $root -Parent
}
if (-not $root) { throw "no go.mod above $PSScriptRoot" }
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = foreach ($n in $sizes) {
    # drawn here rather than kept: one bird, every size (build/icon/gen.go)
    $tmp = Join-Path $env:TEMP "magpie-ico-$n.png"
    Push-Location $root
    try {
        & go run build/icon/gen.go app $n $tmp
        if ($LASTEXITCODE -ne 0) { throw "go run build/icon/gen.go app $n failed" }
    } finally { Pop-Location }
    , [System.IO.File]::ReadAllBytes($tmp)
    Remove-Item -LiteralPath $tmp
}

$fs = [System.IO.File]::Create($Out)
try {
    $w = [System.IO.BinaryWriter]::new($fs)
    # ICONDIR: reserved, type 1 (icon), count
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
    # each image's bytes follow every entry, in order
    $offset = 6 + 16 * $images.Count
    for ($i = 0; $i -lt $images.Count; $i++) {
        $n = $sizes[$i]
        # 256 is written as 0 in an ICO's entry
        $w.Write([byte]($n % 256)); $w.Write([byte]($n % 256))
        $w.Write([byte]0); $w.Write([byte]0)       # palette, reserved
        $w.Write([uint16]1); $w.Write([uint16]32)  # planes, bits per pixel
        $w.Write([uint32]$images[$i].Length)
        $w.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($b in $images) { $w.Write($b) }
} finally {
    $fs.Close()
}
Write-Host "  $Out"
